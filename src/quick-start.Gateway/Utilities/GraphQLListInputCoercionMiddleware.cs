using System.Buffers;
using System.Text.Json;
using HotChocolate.Execution;
using HotChocolate.Language;

namespace quick_start.Gateway.Utilities;

// NOTE:
// This workaround middleware must be removed once the issue in Fusion (where it doesn't automatically coerce single values into lists for variables) is fixed.
// -----------------------------------------------------------------------------
// There is a known issue where Fusion doesn't coerce single values into lists for variables, even if the query's variable definitions expect a list.
// This middleware is a workaround to detect such cases and rewrite the request body on the fly before it reaches Fusion.
// It only applies to JSON requests with a "query" field, and it tries to parse the query to determine which variables are expected to be lists and how deeply nested they are.
// If it finds any variables that are provided as single values but should be wrapped in arrays, it rewrites the JSON body accordingly.
// Any parsing errors are silently ignored, leaving the original request intact for Fusion to handle (which will produce its own error response if the input is invalid).

public sealed class GraphQLListInputCoercionMiddleware(
    Microsoft.AspNetCore.Http.RequestDelegate next,
    IRequestExecutor requestExecutor)
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private InputTypeMap? _typeMap;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldIntercept(context))
        {
            await next(context);
            return;
        }

        InputTypeMap typeMap;
        try
        {
            typeMap = await GetTypeMapAsync(context.RequestAborted);
        }
        catch
        {
            await next(context);
            return;
        }

        context.Request.EnableBuffering();

        byte[]? rewritten = null;
        try
        {
            using var doc = await JsonDocument.ParseAsync(
                context.Request.Body, default, context.RequestAborted);

            if (NeedsCoercion(doc.RootElement, typeMap))
            {
                rewritten = Rewrite(
                    doc.RootElement, typeMap,
                    (int)(context.Request.ContentLength ?? 0));
            }
        }
        catch (JsonException) { }
        finally
        {
            if (context.Request.Body.CanSeek)
                context.Request.Body.Position = 0;
        }

        if (rewritten is not null)
        {
            context.Request.Body = new MemoryStream(rewritten);
            context.Request.ContentLength = rewritten.Length;
        }

        await next(context);
    }

    private async Task<InputTypeMap> GetTypeMapAsync(CancellationToken ct)
    {
        if (_typeMap is not null) return _typeMap;
        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_typeMap is not null) return _typeMap;

            var schema = requestExecutor.Schema;
            var sdl = schema.ToString();

            var document = Utf8GraphQLParser.Parse(sdl);
            _typeMap = InputTypeMap.Build(document);
            return _typeMap;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static bool ShouldIntercept(HttpContext ctx)
    {
        if (!HttpMethods.IsPost(ctx.Request.Method)) return false;
        var ct = ctx.Request.ContentType;
        return ct is not null && (
            ct.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) ||
            ct.StartsWith("application/graphql+json", StringComparison.OrdinalIgnoreCase) ||
            ct.StartsWith("application/graphql-response+json", StringComparison.OrdinalIgnoreCase));
    }

    // ───── dry-run ─────

    private static bool NeedsCoercion(JsonElement root, InputTypeMap map) => root.ValueKind switch
    {
        JsonValueKind.Object => OpNeedsCoercion(root, map),
        JsonValueKind.Array => BatchNeedsCoercion(root, map),
        _ => false
    };

    private static bool BatchNeedsCoercion(JsonElement batch, InputTypeMap map)
    {
        foreach (var op in batch.EnumerateArray())
            if (op.ValueKind == JsonValueKind.Object && OpNeedsCoercion(op, map))
                return true;
        return false;
    }

    private static bool OpNeedsCoercion(JsonElement op, InputTypeMap map)
    {
        if (!TryGetVariableTypes(op, out var types, out var varsEl))
            return false;

        foreach (var varProp in varsEl.EnumerateObject())
        {
            if (types.TryGetValue(varProp.Name, out var type)
                && ValueNeedsCoercion(varProp.Value, type, map))
            {
                return true;
            }
        }
        return false;
    }

    private static bool ValueNeedsCoercion(JsonElement value, ITypeNode type, InputTypeMap map)
    {
        type = Unwrap(type);
        if (value.ValueKind == JsonValueKind.Null) return false;

        if (type is ListTypeNode listType)
        {
            if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                    if (ValueNeedsCoercion(item, listType.Type, map)) return true;
                return false;
            }
            return true;
        }

        if (type is NamedTypeNode named
            && map.InputTypes.TryGetValue(named.Name.Value, out var fields)
            && value.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in value.EnumerateObject())
            {
                if (fields.TryGetValue(prop.Name, out var fieldType)
                    && ValueNeedsCoercion(prop.Value, fieldType, map))
                {
                    return true;
                }
            }
        }
        return false;
    }

    // ───── rewrite ─────

    private static byte[] Rewrite(JsonElement root, InputTypeMap map, int sizeHint)
    {
        var bw = new ArrayBufferWriter<byte>(Math.Max(256, sizeHint));
        using (var writer = new Utf8JsonWriter(bw))
        {
            if (root.ValueKind == JsonValueKind.Array)
            {
                writer.WriteStartArray();
                foreach (var op in root.EnumerateArray())
                {
                    if (op.ValueKind == JsonValueKind.Object) WriteOp(op, map, writer);
                    else op.WriteTo(writer);
                }
                writer.WriteEndArray();
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                WriteOp(root, map, writer);
            }
            else
            {
                root.WriteTo(writer);
            }
        }
        return bw.WrittenSpan.ToArray();
    }

    private static void WriteOp(JsonElement op, InputTypeMap map, Utf8JsonWriter writer)
    {
        if (!TryGetVariableTypes(op, out var types, out _))
        {
            op.WriteTo(writer);
            return;
        }

        writer.WriteStartObject();
        foreach (var prop in op.EnumerateObject())
        {
            if (prop.NameEquals("variables") && prop.Value.ValueKind == JsonValueKind.Object)
            {
                writer.WritePropertyName(prop.Name);
                writer.WriteStartObject();
                foreach (var v in prop.Value.EnumerateObject())
                {
                    writer.WritePropertyName(v.Name);
                    if (types.TryGetValue(v.Name, out var t))
                        CoerceValue(v.Value, t, map, writer);
                    else
                        v.Value.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            else
            {
                prop.WriteTo(writer);
            }
        }
        writer.WriteEndObject();
    }

    private static void CoerceValue(
        JsonElement value, ITypeNode type, InputTypeMap map, Utf8JsonWriter writer)
    {
        type = Unwrap(type);

        if (value.ValueKind == JsonValueKind.Null)
        {
            writer.WriteNullValue();
            return;
        }

        if (type is ListTypeNode listType)
        {
            if (value.ValueKind == JsonValueKind.Array)
            {
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                    CoerceValue(item, listType.Type, map, writer);
                writer.WriteEndArray();
            }
            else
            {
                writer.WriteStartArray();
                CoerceValue(value, listType.Type, map, writer);
                writer.WriteEndArray();
            }
            return;
        }

        if (type is NamedTypeNode named
            && map.InputTypes.TryGetValue(named.Name.Value, out var fields)
            && value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var prop in value.EnumerateObject())
            {
                writer.WritePropertyName(prop.Name);
                if (fields.TryGetValue(prop.Name, out var fieldType))
                    CoerceValue(prop.Value, fieldType, map, writer);
                else
                    prop.Value.WriteTo(writer);
            }
            writer.WriteEndObject();
            return;
        }

        value.WriteTo(writer);
    }

    // ───── helpers ─────

    private static bool TryGetVariableTypes(
        JsonElement op,
        out Dictionary<string, ITypeNode> variableTypes,
        out JsonElement variablesElement)
    {
        variableTypes = null!;
        variablesElement = default;

        if (!op.TryGetProperty("variables", out var varsEl)
            || varsEl.ValueKind != JsonValueKind.Object
            || !op.TryGetProperty("query", out var queryEl)
            || queryEl.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var query = queryEl.GetString();
        if (string.IsNullOrWhiteSpace(query)) return false;

        DocumentNode document;
        try { document = Utf8GraphQLParser.Parse(query); }
        catch (SyntaxException) { return false; }

        string? opName = op.TryGetProperty("operationName", out var n)
            && n.ValueKind == JsonValueKind.String ? n.GetString() : null;

        OperationDefinitionNode? operation = null;
        int count = 0;
        foreach (var def in document.Definitions)
        {
            if (def is not OperationDefinitionNode opDef) continue;
            count++;
            if (opName is null) operation = opDef;
            else if (opDef.Name?.Value == opName) { operation = opDef; break; }
        }
        if (opName is null && count != 1) operation = null;
        if (operation is null) return false;

        Dictionary<string, ITypeNode>? map = null;
        foreach (var varDef in operation.VariableDefinitions)
        {
            map ??= new Dictionary<string, ITypeNode>(StringComparer.Ordinal);
            map[varDef.Variable.Name.Value] = varDef.Type;
        }

        if (map is null) return false;
        variableTypes = map;
        variablesElement = varsEl;
        return true;
    }

    private static ITypeNode Unwrap(ITypeNode type)
        => type is NonNullTypeNode nn ? nn.Type : type;
}

internal sealed class InputTypeMap
{
    public Dictionary<string, Dictionary<string, ITypeNode>> InputTypes { get; }
        = new(StringComparer.Ordinal);

    public static InputTypeMap Build(DocumentNode schemaDocument)
    {
        var map = new InputTypeMap();

        foreach (var def in schemaDocument.Definitions)
        {
            if (def is InputObjectTypeDefinitionNode input)
            {
                var fields = new Dictionary<string, ITypeNode>(StringComparer.Ordinal);
                foreach (var field in input.Fields)
                    fields[field.Name.Value] = field.Type;
                map.InputTypes[input.Name.Value] = fields;
            }
            else if (def is InputObjectTypeExtensionNode extension)
            {
                if (!map.InputTypes.TryGetValue(extension.Name.Value, out var existing))
                {
                    existing = new Dictionary<string, ITypeNode>(StringComparer.Ordinal);
                    map.InputTypes[extension.Name.Value] = existing;
                }
                foreach (var field in extension.Fields)
                    existing[field.Name.Value] = field.Type;
            }
        }

        return map;
    }
}

#region Optimized (but has issue when coercion has to happen inside an input object)
//public sealed class GraphQLListInputCoercionMiddleware(RequestDelegate next)
//{
//    public async Task InvokeAsync(HttpContext context)
//    {
//        if (!ShouldIntercept(context))
//        {
//            await next(context);
//            return;
//        }

//        context.Request.EnableBuffering();

//        byte[]? rewritten = null;
//        try
//        {
//            using var doc = await JsonDocument.ParseAsync(context.Request.Body, default, context.RequestAborted);

//            rewritten = TryRewrite(doc.RootElement, (int)(context.Request.ContentLength ?? 0));
//        }
//        catch (JsonException)
//        {
//            // Pass through — Fusion will produce the proper error.
//        }
//        finally
//        {
//            // Reset for downstream consumers in the pass-through path.
//            if (context.Request.Body.CanSeek)
//                context.Request.Body.Position = 0;
//        }

//        if (rewritten is not null)
//        {
//            context.Request.Body = new MemoryStream(rewritten);
//            context.Request.ContentLength = rewritten.Length;
//        }

//        await next(context);
//    }

//    private static bool ShouldIntercept(HttpContext ctx)
//    {
//        if (!HttpMethods.IsPost(ctx.Request.Method)) return false;
//        var ct = ctx.Request.ContentType;
//        return ct is not null && (
//            ct.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) ||
//            ct.StartsWith("application/graphql+json", StringComparison.OrdinalIgnoreCase) ||
//            ct.StartsWith("application/graphql-response+json", StringComparison.OrdinalIgnoreCase));
//    }

//    // Returns rewritten bytes, or null if no change is required.
//    private static byte[]? TryRewrite(JsonElement root, int sizeHint)
//    {
//        switch (root.ValueKind)
//        {
//            case JsonValueKind.Object:
//                {
//                    var plan = AnalyzeOp(root);
//                    return plan is null ? null : RewriteSingle(root, plan, sizeHint);
//                }
//            case JsonValueKind.Array:
//                {
//                    Dictionary<int, IReadOnlyDictionary<string, int>>? batchPlans = null;
//                    var i = 0;
//                    foreach (var op in root.EnumerateArray())
//                    {
//                        if (op.ValueKind == JsonValueKind.Object)
//                        {
//                            var plan = AnalyzeOp(op);
//                            if (plan is not null)
//                            {
//                                batchPlans ??= [];
//                                batchPlans[i] = plan;
//                            }
//                        }
//                        i++;
//                    }
//                    return batchPlans is null ? null : RewriteBatch(root, batchPlans, sizeHint);
//                }
//            default:
//                return null;
//        }
//    }

//    // Returns variable name → number of [] wraps to add, or null if nothing to do.
//    private static IReadOnlyDictionary<string, int>? AnalyzeOp(JsonElement op)
//    {
//        if (!op.TryGetProperty("variables", out var varsEl) || varsEl.ValueKind != JsonValueKind.Object)
//        {
//            return null;
//        }

//        // Fast path: if every variable value is already an array (or null),
//        // coercion can't change anything — skip the GraphQL parse entirely.
//        var anyCandidate = false;
//        foreach (var p in varsEl.EnumerateObject())
//        {
//            var k = p.Value.ValueKind;
//            if (k != JsonValueKind.Array && k != JsonValueKind.Null)
//            {
//                anyCandidate = true;
//                break;
//            }
//        }
//        if (!anyCandidate)
//            return null;

//        if (!op.TryGetProperty("query", out var queryEl) || queryEl.ValueKind != JsonValueKind.String)
//        {
//            return null;
//        }

//        var query = queryEl.GetString();
//        if (string.IsNullOrWhiteSpace(query))
//            return null;

//        DocumentNode document;
//        try
//        {
//            document = Utf8GraphQLParser.Parse(query);
//        }
//        catch (SyntaxException)
//        {
//            return null;
//        }

//        var opName =
//            op.TryGetProperty("operationName", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
//            ? nameEl.GetString() : null;

//        OperationDefinitionNode? operation = null;
//        int operationCount = 0;
//        foreach (var def in document.Definitions)
//        {
//            if (def is not OperationDefinitionNode opDef) continue;
//            operationCount++;

//            if (opName is null)
//            {
//                operation = opDef;
//            }
//            else if (opDef.Name?.Value == opName)
//            {
//                operation = opDef;
//                break;
//            }
//        }
//        // Ambiguous (no opName + multiple ops) → bail.
//        if (opName is null && operationCount != 1)
//            operation = null;
//        if (operation is null)
//            return null;

//        Dictionary<string, int>? wraps = null;
//        foreach (var varDef in operation.VariableDefinitions)
//        {
//            int typeDepth = CountListWraps(varDef.Type);
//            if (typeDepth == 0)
//                continue;

//            var name = varDef.Variable.Name.Value;
//            if (!varsEl.TryGetProperty(name, out var valEl))
//                continue;
//            if (valEl.ValueKind == JsonValueKind.Null)
//                continue;

//            int actualDepth = MeasureListDepth(valEl);
//            if (actualDepth >= typeDepth)
//                continue;

//            wraps ??= new Dictionary<string, int>(StringComparer.Ordinal);
//            wraps[name] = typeDepth - actualDepth;
//        }

//        return wraps;
//    }

//    private static byte[] RewriteSingle(
//        JsonElement op, IReadOnlyDictionary<string, int> plan, int sizeHint)
//    {
//        var bw = new ArrayBufferWriter<byte>(Math.Max(256, sizeHint));
//        using (var writer = new Utf8JsonWriter(bw))
//            WriteOp(op, plan, writer);
//        return bw.WrittenSpan.ToArray();
//    }

//    private static byte[] RewriteBatch(
//        JsonElement batch,
//        IReadOnlyDictionary<int, IReadOnlyDictionary<string, int>> plans,
//        int sizeHint)
//    {
//        var bw = new ArrayBufferWriter<byte>(Math.Max(256, sizeHint));
//        using (var writer = new Utf8JsonWriter(bw))
//        {
//            writer.WriteStartArray();
//            int i = 0;
//            foreach (var op in batch.EnumerateArray())
//            {
//                if (plans.TryGetValue(i, out var plan))
//                    WriteOp(op, plan, writer);
//                else
//                    op.WriteTo(writer); // verbatim copy
//                i++;
//            }
//            writer.WriteEndArray();
//        }
//        return bw.WrittenSpan.ToArray();
//    }

//    private static void WriteOp(
//        JsonElement op, IReadOnlyDictionary<string, int> wraps, Utf8JsonWriter w)
//    {
//        w.WriteStartObject();
//        foreach (var prop in op.EnumerateObject())
//        {
//            if (prop.NameEquals("variables"))
//            {
//                w.WritePropertyName(prop.Name);
//                WriteVariables(prop.Value, wraps, w);
//            }
//            else
//            {
//                prop.WriteTo(w);
//            }
//        }
//        w.WriteEndObject();
//    }

//    private static void WriteVariables(
//        JsonElement vars, IReadOnlyDictionary<string, int> wraps, Utf8JsonWriter w)
//    {
//        w.WriteStartObject();
//        foreach (var p in vars.EnumerateObject())
//        {
//            w.WritePropertyName(p.Name);
//            if (wraps.TryGetValue(p.Name, out var n) && n > 0)
//            {
//                for (int i = 0; i < n; i++) w.WriteStartArray();
//                p.Value.WriteTo(w);
//                for (int i = 0; i < n; i++) w.WriteEndArray();
//            }
//            else
//            {
//                p.Value.WriteTo(w);
//            }
//        }
//        w.WriteEndObject();
//    }

//    private static int CountListWraps(ITypeNode type) => type switch
//    {
//        ListTypeNode l => 1 + CountListWraps(l.Type),
//        NonNullTypeNode n => CountListWraps(n.Type),
//        _ => 0
//    };

//    private static int MeasureListDepth(JsonElement el)
//    {
//        int d = 0;
//        while (el.ValueKind == JsonValueKind.Array)
//        {
//            d++;
//            if (el.GetArrayLength() == 0) break;
//            el = el[0];
//        }
//        return d;
//    }
//}
#endregion

#region OLD Non-Optimized Implementation
//public sealed class GraphQLListInputCoercionMiddleware(Microsoft.AspNetCore.Http.RequestDelegate next)
//{
//    public async Task InvokeAsync(HttpContext context)
//    {
//        if (!ShouldIntercept(context))
//        {
//            await next(context);
//            return;
//        }

//        context.Request.EnableBuffering();

//        string body;
//        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
//        {
//            body = await reader.ReadToEndAsync();
//        }
//        context.Request.Body.Position = 0;

//        if (!string.IsNullOrWhiteSpace(body))
//        {
//            try
//            {
//                var rewritten = TryCoerceListVariables(body);
//                if (rewritten is not null)
//                {
//                    var bytes = Encoding.UTF8.GetBytes(rewritten);
//                    context.Request.Body = new MemoryStream(bytes);
//                    context.Request.ContentLength = bytes.Length;
//                }
//            }
//            catch
//            {
//                // Anything we can't parse, we leave alone — Fusion's own error
//                // handling will surface the real problem to the client.
//            }
//        }

//        await next(context);
//    }

//    private static bool ShouldIntercept(HttpContext ctx)
//    {
//        if (!HttpMethods.IsPost(ctx.Request.Method)) return false;
//        var ct = ctx.Request.ContentType;
//        return ct is not null && (
//            ct.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) ||
//            ct.StartsWith("application/graphql+json", StringComparison.OrdinalIgnoreCase) ||
//            ct.StartsWith("application/graphql-response+json", StringComparison.OrdinalIgnoreCase));
//    }

//    private static string? TryCoerceListVariables(string body)
//    {
//        var root = JsonNode.Parse(body);
//        if (root is null) return null;

//        bool changed = root switch
//        {
//            JsonArray batch => CoerceBatch(batch),
//            JsonObject single => CoerceOne(single),
//            _ => false
//        };

//        return changed ? root.ToJsonString() : null;
//    }

//    private static bool CoerceBatch(JsonArray batch)
//    {
//        bool changed = false;
//        foreach (var item in batch)
//            if (item is JsonObject op && CoerceOne(op))
//                changed = true;
//        return changed;
//    }

//    private static bool CoerceOne(JsonObject op)
//    {
//        // Persisted-operation requests don't carry the query text — nothing we can do here.
//        var query = op["query"]?.GetValue<string?>();
//        if (string.IsNullOrWhiteSpace(query))
//            return false;

//        if (op["variables"] is not JsonObject { Count: > 0 } variables)
//            return false;

//        DocumentNode document;
//        try
//        {
//            document = Utf8GraphQLParser.Parse(query);
//        }
//        catch (SyntaxException)
//        {
//            return false;
//        }

//        var opName = op["operationName"]?.GetValue<string?>();
//        var operations = document.Definitions.OfType<OperationDefinitionNode>().ToList();

//        var operation = opName is null
//            ? (operations.Count == 1 ? operations[0] : null)
//            : operations.FirstOrDefault(o => o.Name?.Value == opName);

//        if (operation is null)
//            return false;

//        bool changed = false;
//        foreach (var varDef in operation.VariableDefinitions)
//        {
//            var listDepth = CountListWraps(varDef.Type);
//            if (listDepth == 0) continue;

//            var name = varDef.Variable.Name.Value;
//            var node = variables[name];

//            // Missing or JSON null → leave alone (spec: null stays null).
//            if (node is null || node.GetValueKind() == System.Text.Json.JsonValueKind.Null)
//                continue;

//            // Already wrapped enough → leave alone.
//            int actualDepth = MeasureListDepth(node);
//            if (actualDepth >= listDepth) 
//                continue;

//            // Wrap (listDepth - actualDepth) times.
//            JsonNode wrapped = node.DeepClone();
//            for (int i = 0; i < listDepth - actualDepth; i++)
//                wrapped = new JsonArray(wrapped);

//            variables[name] = wrapped;
//            changed = true;
//        }

//        return changed;
//    }

//    // Counts the [ ] wrappers in a type (skipping ! markers): [Int!]! => 1, [[Int]] => 2.
//    private static int CountListWraps(ITypeNode type) => type switch
//    {
//        ListTypeNode list => 1 + CountListWraps(list.Type),
//        NonNullTypeNode nn => CountListWraps(nn.Type),
//        _ => 0
//    };

//    // How many leading arrays the actual JSON value already has.
//    private static int MeasureListDepth(JsonNode? node)
//    {
//        int d = 0;
//        while (node is JsonArray arr)
//        {
//            d++;
//            if (arr.Count == 0) break;     // can't peek deeper into an empty list
//            node = arr[0];
//        }
//        return d;
//    }
//}
#endregion
