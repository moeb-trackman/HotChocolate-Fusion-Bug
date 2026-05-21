# HotChocolate Fusion Bug

Reproducing a Fusion bug with **deeply nested field queries**!

## Reproduce

Run the gateway and navigate to <https://localhost:10079/graphql/>, then run the query

### Error with Query

Running the query with the `product.name` field results in an `"Invalid GraphQL Request"` error, while the query works fine without it.

```graphql
query getAllOrders {
  orders {
    name
    product {
      id
      # Adding "name" field results in an "Invalid GraphQL Request" error
      name
    }
  }
}

# Response
{
  "errors": [
    {
      "message": "Invalid GraphQL Request.",
      "extensions": {
        "code": "HC0009"
      }
    },
    {
      "message": "Invalid GraphQL Request.",
      "extensions": {
        "code": "HC0009"
      }
    },
    {
      "message": "Unexpected Execution Error",
      "path": [
        "orders",
        0,
        "product",
        "name"
      ]
    }
  ],
  "data": null
}
```

### Error with Node

Querying the node with the nested `comments { content }` field results in an error at *Planning* (details below), while the query works fine without it.

```graphql
query getOrder1 {
  node(id: "T3JkZXIxOjE=") {
    ... on IOrder {
      name
      product {
        name
        # Adding this nested field results in an ERROR
        comments {
          content
        }
      }
    }
  }
}

# Response
{
  "errors": [
    {
      "message": "Unexpected Execution Error",
      "extensions": {
        "exception": {
          "message": "The given key '{\r\n  name\r\n  comments {\r\n    content\r\n  }\r\n}' was not present in the dictionary.",
          "stackTrace": "   at System.Collections.ThrowHelper.ThrowKeyNotFoundException[TKey](TKey key)\r\n   at System.Collections.Immutable.ImmutableDictionary`2.get_Item(TKey key)\r\n   at HotChocolate.Fusion.Planning.SelectionSetIndexBuilder.RegisterCloned(SelectionSetNode original, SelectionSetNode cloned)\r\n   at HotChocolate.Fusion.Planning.Partitioners.SelectionSetByTypePartitioner.<>c__DisplayClass4_0.<AddSelectionsForConcreteType>b__0(ISyntaxNode node)\r\n   at HotChocolate.Language.Visitors.SyntaxRewriter.<>c__DisplayClass0_0.<Create>b__0(ISyntaxNode node, Object _)\r\n   at HotChocolate.Language.Visitors.DelegateSyntaxRewriter`1.OnRewrite(ISyntaxNode node, TContext context)\r\n   at HotChocolate.Language.Visitors.SyntaxRewriter`1.Rewrite(ISyntaxNode node, TContext context)\r\n   at HotChocolate.Language.Visitors.SyntaxRewriter`1.RewriteNodeOrDefault[T](T node, TContext context)\r\n   at HotChocolate.Language.Visitors.SyntaxRewriter`1.RewriteField(FieldNode node, TContext context)\r\n   at HotChocolate.Language.Visitors.SyntaxRewriter`1.OnRewrite(ISyntaxNode node, TContext context)\r\n   at HotChocolate.Language.Visitors.DelegateSyntaxRewriter`1.OnRewrite(ISyntaxNode node, TContext context)\r\n   at HotChocolate.Language.Visitors.SyntaxRewriter`1.Rewrite(ISyntaxNode node, TContext context)\r\n   at HotChocolate.Language.Visitors.SyntaxRewriterExtensions.Rewrite[T](ISyntaxRewriter`1 rewriter, T node)\r\n   at HotChocolate.Fusion.Planning.Partitioners.SelectionSetByTypePartitioner.AddSelectionsForConcreteType(Context context, FusionObjectTypeDefinition type, List`1 selections, Boolean cloneSelectionSets)\r\n   at HotChocolate.Fusion.Planning.Partitioners.SelectionSetByTypePartitioner.CollectSelections(SelectionSetNode selectionSet, ITypeDefinition type, Context context)\r\n   at HotChocolate.Fusion.Planning.Partitioners.SelectionSetByTypePartitioner.CollectSelections(SelectionSetNode selectionSet, ITypeDefinition type, Context context)\r\n   at HotChocolate.Fusion.Planning.Partitioners.SelectionSetByTypePartitioner.Partition(SelectionSetByTypePartitionerInput input)\r\n   at HotChocolate.Fusion.Planning.OperationPlanner.PlanNode(NodeFieldWorkItem workItem, PlanNode current, PlanQueue possiblePlans, Backlog backlog)\r\n   at HotChocolate.Fusion.Planning.OperationPlanner.TryBuildGreedyCompletePlan(PlanQueue possiblePlans, CancellationToken cancellationToken)\r\n   at HotChocolate.Fusion.Planning.OperationPlanner.Plan(String operationId, PlanQueue possiblePlans, Boolean emitPlannerEvents, CancellationToken cancellationToken)\r\n   at HotChocolate.Fusion.Planning.OperationPlanner.CreatePlan(String id, String hash, String shortHash, OperationDefinitionNode operationDefinition, CancellationToken cancellationToken)\r\n   at HotChocolate.Fusion.Execution.Pipeline.OperationPlanMiddleware.PlanOperation(RequestContext context, OperationDocumentInfo operationDocumentInfo, DocumentNode operationDocument)\r\n   at HotChocolate.Fusion.Execution.Pipeline.OperationPlanMiddleware.InvokeAsync(RequestContext context, RequestDelegate next)\r\n   at HotChocolate.Fusion.Execution.Pipeline.OperationPlanMiddleware.<>c__DisplayClass8_0.<Create>b__1(RequestContext requestContext)\r\n   at HotChocolate.Fusion.Execution.Pipeline.OperationPlanCacheMiddleware.InvokeAsync(RequestContext context, RequestDelegate next)\r\n   at HotChocolate.Execution.Pipeline.DocumentValidationMiddleware.InvokeAsync(RequestContext context)\r\n   at HotChocolate.Execution.Pipeline.DocumentParserMiddleware.InvokeAsync(RequestContext context)\r\n   at HotChocolate.Execution.Pipeline.DocumentCacheMiddleware.InvokeAsync(RequestContext context)\r\n   at HotChocolate.Fusion.Execution.Pipeline.TimeoutMiddleware.InvokeAsync(RequestContext context)\r\n   at HotChocolate.Execution.Pipeline.ExceptionMiddleware.InvokeAsync(RequestContext context)"
        }
      }
    }
  ]
}

# Stack Trace
at System.Collections.ThrowHelper.ThrowKeyNotFoundException[TKey](TKey key)
   at System.Collections.Immutable.ImmutableDictionary`2.get_Item(TKey key)
   at HotChocolate.Fusion.Planning.SelectionSetIndexBuilder.RegisterCloned(SelectionSetNode original, SelectionSetNode cloned)
   at HotChocolate.Fusion.Planning.Partitioners.SelectionSetByTypePartitioner.<>c__DisplayClass4_0.<AddSelectionsForConcreteType>b__0(ISyntaxNode node)
   at HotChocolate.Language.Visitors.SyntaxRewriter.<>c__DisplayClass0_0.<Create>b__0(ISyntaxNode node, Object _)
   at HotChocolate.Language.Visitors.DelegateSyntaxRewriter`1.OnRewrite(ISyntaxNode node, TContext context)
   at HotChocolate.Language.Visitors.SyntaxRewriter`1.Rewrite(ISyntaxNode node, TContext context)
   at HotChocolate.Language.Visitors.SyntaxRewriter`1.RewriteNodeOrDefault[T](T node, TContext context)
   at HotChocolate.Language.Visitors.SyntaxRewriter`1.RewriteField(FieldNode node, TContext context)
   at HotChocolate.Language.Visitors.SyntaxRewriter`1.OnRewrite(ISyntaxNode node, TContext context)
   at HotChocolate.Language.Visitors.DelegateSyntaxRewriter`1.OnRewrite(ISyntaxNode node, TContext context)
   at HotChocolate.Language.Visitors.SyntaxRewriter`1.Rewrite(ISyntaxNode node, TContext context)
   at HotChocolate.Language.Visitors.SyntaxRewriterExtensions.Rewrite[T](ISyntaxRewriter`1 rewriter, T node)
   at HotChocolate.Fusion.Planning.Partitioners.SelectionSetByTypePartitioner.AddSelectionsForConcreteType(Context context, FusionObjectTypeDefinition type, List`1 selections, Boolean cloneSelectionSets)
   at HotChocolate.Fusion.Planning.Partitioners.SelectionSetByTypePartitioner.CollectSelections(SelectionSetNode selectionSet, ITypeDefinition type, Context context)
   at HotChocolate.Fusion.Planning.Partitioners.SelectionSetByTypePartitioner.CollectSelections(SelectionSetNode selectionSet, ITypeDefinition type, Context context)
   at HotChocolate.Fusion.Planning.Partitioners.SelectionSetByTypePartitioner.Partition(SelectionSetByTypePartitionerInput input)
   at HotChocolate.Fusion.Planning.OperationPlanner.PlanNode(NodeFieldWorkItem workItem, PlanNode current, PlanQueue possiblePlans, Backlog backlog)
   at HotChocolate.Fusion.Planning.OperationPlanner.TryBuildGreedyCompletePlan(PlanQueue possiblePlans, CancellationToken cancellationToken)
   at HotChocolate.Fusion.Planning.OperationPlanner.Plan(String operationId, PlanQueue possiblePlans, Boolean emitPlannerEvents, CancellationToken cancellationToken)
   at HotChocolate.Fusion.Planning.OperationPlanner.CreatePlan(String id, String hash, String shortHash, OperationDefinitionNode operationDefinition, CancellationToken cancellationToken)
   at HotChocolate.Fusion.Execution.Pipeline.OperationPlanMiddleware.PlanOperation(RequestContext context, OperationDocumentInfo operationDocumentInfo, DocumentNode operationDocument)
   at HotChocolate.Fusion.Execution.Pipeline.OperationPlanMiddleware.InvokeAsync(RequestContext context, RequestDelegate next)
   at HotChocolate.Fusion.Execution.Pipeline.OperationPlanMiddleware.<>c__DisplayClass8_0.<Create>b__1(RequestContext requestContext)
   at HotChocolate.Fusion.Execution.Pipeline.OperationPlanCacheMiddleware.InvokeAsync(RequestContext context, RequestDelegate next)
   at HotChocolate.Execution.Pipeline.DocumentValidationMiddleware.InvokeAsync(RequestContext context)
   at HotChocolate.Execution.Pipeline.DocumentParserMiddleware.InvokeAsync(RequestContext context)
   at HotChocolate.Execution.Pipeline.DocumentCacheMiddleware.InvokeAsync(RequestContext context)
   at HotChocolate.Fusion.Execution.Pipeline.TimeoutMiddleware.InvokeAsync(RequestContext context)
   at HotChocolate.Execution.Pipeline.ExceptionMiddleware.InvokeAsync(RequestContext context)
```

### HotChocolate Version: 16.0.8