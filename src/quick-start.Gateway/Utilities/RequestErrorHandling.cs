using System.Text.Json;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Fusion.Configuration;
using Microsoft.Extensions.Http;

namespace quick_start.Gateway.Utilities;

public static class RequestErrorHandlingExtensions
{
    public static IServiceCollection AddCaptureRequestError(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.AddTransient<CaptureRequestErrorHandler>();

        // Attach the handler to every subgraph HttpClient.
        // Fusion uses named IHttpClientFactory clients per subgraph, so ConfigureAll
        // is the simplest way to cover all of them without naming each.
        return services.ConfigureAll<HttpClientFactoryOptions>(opts =>
        {
            opts.HttpMessageHandlerBuilderActions.Add(b =>
            {
                var handler = b.Services.GetRequiredService<CaptureRequestErrorHandler>();
                b.AdditionalHandlers.Add(handler);
            });
        });
    }

    public static IFusionGatewayBuilder UseRestoreRequestError(this IFusionGatewayBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.UseRequest(RestoreRequestErrorMiddleware.Create());
    }
}

public sealed class CaptureRequestErrorHandler(
    IHttpContextAccessor httpContextAccessor,
    ILogger<CaptureRequestErrorHandler> logger) : DelegatingHandler
{
    public const string HttpContextKey = "__GraphQL_RequestErrors";

    private static readonly HashSet<string> JsonMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/json",
        "application/graphql-response+json",
        "application/graphql+json"
    };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode || response.Content is null)
            return response;

        // Skip subscriptions (text/event-stream), multipart/defer, etc.
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is null || !JsonMediaTypes.Contains(mediaType))
            return response;

        byte[] body;
        try
        {
            body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read subgraph response body to capture request errors.");
            throw; //return response;
        }

        if (body.Length == 0)
            return response;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return response;

            // Need both errors AND non-null data for this to be the case we care about.
            if (!root.TryGetProperty("errors", out var errors)
                || errors.ValueKind != JsonValueKind.Array
                || errors.GetArrayLength() == 0)
            {
                return response;
            }

            var dataIsNullOrMissing =
                !root.TryGetProperty("data", out var data)
                || data.ValueKind == JsonValueKind.Null;

            // If data is null, Fusion already propagates errors → don't touch.
            if (dataIsNullOrMissing)
                return response;

            // Capture (clone — the JsonDocument disposes when we leave this scope).
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext is not null)
            {
                var captured = httpContext.Items[HttpContextKey] as List<JsonElement> ?? [];

                foreach (var error in errors.EnumerateArray())
                    captured.Add(error.Clone());

                httpContext.Items[HttpContextKey] = captured;
            }
        }
        catch (JsonException ex)
        {
            logger.LogDebug(ex, "Subgraph response was not JSON; leaving as-is.");
        }

        return response;
    }
}


public sealed class RestoreRequestErrorMiddleware(
    HotChocolate.Execution.RequestDelegate next,
    IHttpContextAccessor? httpContextAccessor)
{
    public static RequestMiddlewareConfiguration Create()
    {
        return new RequestMiddlewareConfiguration((factoryContext, next) =>
        {
            var middleware = new RestoreRequestErrorMiddleware(next,
                factoryContext.Services.GetService<IHttpContextAccessor>());
            return context => middleware.InvokeAsync(context);
        });
    }

    public async ValueTask InvokeAsync(RequestContext context)
    {
        await next(context).ConfigureAwait(false);

        var httpContext = httpContextAccessor?.HttpContext;
        if (httpContext is null) return;

        if (httpContext.Items[CaptureRequestErrorHandler.HttpContextKey]
            is not List<JsonElement> { Count: > 0 } captured)
        {
            return;
        }

        // Subscriptions / @defer / @stream produce a response stream — leave those alone.
        if (context.Result is not OperationResult operationResult)
            return;

        var errors = captured.ConvertAll(ConvertToError);
        operationResult.Errors = operationResult.Errors.AddRange(errors);
    }

    private static IError ConvertToError(JsonElement json)
    {
        var builder = ErrorBuilder.New();

        if (json.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String
            && msg.GetString() is string message)
        {
            builder.SetMessage(message);
        }

        if (json.TryGetProperty("path", out var path) && path.ValueKind == JsonValueKind.Array)
        {
            var segments = new List<object>();
            foreach (var seg in path.EnumerateArray())
            {
                segments.Add(seg.ValueKind switch
                {
                    JsonValueKind.String => seg.GetString()!,
                    JsonValueKind.Number => seg.GetInt32(),
                    _ => seg.GetRawText()
                });
            }
            builder.SetPath(HotChocolate.Path.FromList(segments));
        }

        if (json.TryGetProperty("locations", out var locs) && locs.ValueKind == JsonValueKind.Array)
        {
            foreach (var loc in locs.EnumerateArray())
            {
                if (loc.TryGetProperty("line", out var l)
                    && loc.TryGetProperty("column", out var c))
                {
                    builder.AddLocation(new Location(l.GetInt32(), c.GetInt32()));
                }
            }
        }

        if (json.TryGetProperty("extensions", out var ext) && ext.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in ext.EnumerateObject())
                builder.SetExtension(prop.Name, JsonElementToObject(prop.Value));
        }

        return builder.Build();
    }

    private static object? JsonElementToObject(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var i) ? i : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => el.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
            JsonValueKind.Array => el.EnumerateArray().Select(JsonElementToObject).ToList(),
            _ => el.GetRawText()
        };
    }
}