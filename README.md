# HotChocolate Fusion Bug

Reproduce of a bug with HotChocolate Fusion when using inheritance!

## Ordering Service

We have Ordering service that has two implementation (`Order1` and `Order2`) for `IOrder` interface and inherits `Node` interface (relay patten)

```csharp

public interface IOrder
{
    [ID]
    int Id { get; set; }
    string Name { get; set; }
    Product Product { get; set; }
}

[Node]
public class Order1 : IOrder
{
    [ID]
    public int Id { get; set; }

    public string Name { get; set; }
    public Product Product { get; set; }

}

[Node]
public class Order2 : IOrder
{
    [ID]
    public int Id { get; set; }

    public string Name { get; set; }
    public Product Product { get; set; }
}

public sealed record Product([property: ID] int Id);
```

Run the project and navigate to <http://localhost:5002/graphql> to see the its schema.

```graphql
schema {
  query: Query
}

interface IOrder {
  id: ID!
  name: String!
  product: Product!
}

"The node interface is implemented by entities that have a global unique identifier."
interface Node {
  id: ID!
}

type Order1 implements Node & IOrder {
  id: ID!
  name: String!
  product: Product!
}

type Order2 implements Node & IOrder {
  id: ID!
  name: String!
  product: Product!
}

type Product {
  id: ID!
}

type Query {
  "Fetches an object given its ID."
  node("ID of the object." id: ID!): Node @cost(weight: "10")
  "Lookup nodes by a list of IDs."
  nodes("The list of node IDs." ids: [ID!]!): [Node]! @cost(weight: "10")
  order1ById(id: ID!): Order1 @cost(weight: "10")
  order2ById(id: ID!): Order2 @cost(weight: "10")
  orders: [IOrder!]! @cost(weight: "10")
}
```

We have also Product service that resolves `product` type by Id via Node (relay pattern)

## Gateway

Both Ordering and Product services are composed into the super-graph for the gateway.

Run the project and navigate to <https://localhost:10079/graphql/> to see the schema.

```graphql
schema {
  query: Query
}

interface IOrder {
  id: ID!
  name: String!
  product: Product!
}

"The node interface is implemented by entities that have a global unique identifier."
interface Node {
  id: ID!
}

type Order1 implements Node & IOrder {
  id: ID!
  name: String!
  product: Product!
}

type Order2 implements Node & IOrder {
  id: ID!
  name: String!
  product: Product!
}

type Product implements Node {
  id: ID!
  name: String!
}

type Query {
  "Fetches an object given its ID."
  node("ID of the object." id: ID!): Node @cost(weight: "10")
  order1ById(id: ID!): Order1 @cost(weight: "10")
  order2ById(id: ID!): Order2 @cost(weight: "10")
  orders: [IOrder!]! @cost(weight: "10")
  products: [Product!]! @cost(weight: "10")
}

"The purpose of the `cost` directive is to define a `weight` for GraphQL types, fields, and arguments. Static analysis can use these weights when calculating the overall cost of a query or response."
directive @cost(
  "The `weight` argument defines what value to add to the overall cost for every appearance, or possible appearance, of a type, field, argument, etc."
  weight: String!
) on SCALAR | OBJECT | FIELD_DEFINITION | ARGUMENT_DEFINITION | ENUM | INPUT_FIELD_DEFINITION
```

## Run the Query

If you run this query on the Gateway

```graphql
query Order1 {
  node(id: "T3JkZXIxOjE=") {
    ... on IOrder {
      name
      product {
        id
        name
      }
    }
  }
}

query Order2 {
  node(id: "T3JkZXIyOjI=") {
    ... on IOrder {
      name
      product {
        id
        name
      }
    }
  }
}
```


The `Order2` query works fine but `Order1` returns this error:

> The variable value `__fusion_exports__1` was not provided but is required. (Parameter 'requirementValues')

```json
{
  "errors": [
    {
      "message": "Cannot return null for non-nullable field.",
      "locations": [
        {
          "line": 7,
          "column": 9
        }
      ],
      "path": [
        "node",
        "product",
        "name"
      ],
      "extensions": {
        "code": "HC0018"
      }
    },
    {
      "message": "Unexpected Execution Error",
      "extensions": {
        "message": "The variable value `__fusion_exports__1` was not provided but is required. (Parameter 'requirementValues')",
        "stackTrace": "   at HotChocolate.Fusion.Execution.Nodes.ResolverNodeBase.CreateRequest(IVariableValueCollection variables, IReadOnlyDictionary`2 requirementValues)\r\n   at HotChocolate.Fusion.Execution.Nodes.Resolve.InitializeRequests(FusionExecutionContext context, List`1 executionState, SubgraphGraphQLRequest[] requests)\r\n   at HotChocolate.Fusion.Execution.Nodes.Resolve.OnExecuteAsync(FusionExecutionContext context, RequestState state, CancellationToken cancellationToken)"
      }
    }
  ],
  "data": {
    "node": null
  },
  "extensions": {
    "queryPlan": {
      "document": "query Order1 { node(id: \"T3JkZXIxOjE=\") { ... on IOrder { name product { id name } } } }",
      "operation": "Order1",
      "rootNode": {
        "type": "Sequence",
        "nodes": [
          {
            "type": "ResolveNode",
            "selectionId": 0,
            "responseName": "node",
            "branches": [
              {
                "type": "Product",
                "node": {
                  "type": "Resolve",
                  "subgraph": "Products",
                  "document": "query Order1_1 { node(id: \"T3JkZXIxOjE=\") { ... on Product { __typename } } }",
                  "selectionSetId": 10
                }
              },
              {
                "type": "Order2",
                "node": {
                  "type": "Resolve",
                  "subgraph": "Ordering",
                  "document": "query Order1_2 { node(id: \"T3JkZXIxOjE=\") { ... on Order2 { name product { id __fusion_exports__1: id } __typename } } }",
                  "selectionSetId": 10
                }
              },
              {
                "type": "Order1",
                "node": {
                  "type": "Resolve",
                  "subgraph": "Ordering",
                  "document": "query Order1_3 { node(id: \"T3JkZXIxOjE=\") { ... on Order1 { name product { id } __typename } } }",
                  "selectionSetId": 10
                }
              }
            ]
          },
          {
            "type": "Compose",
            "selectionSetIds": [
              10
            ]
          },
          {
            "type": "Resolve",
            "subgraph": "Products",
            "document": "query Order1_4($__fusion_exports__1: ID!) { productById(id: $__fusion_exports__1) { name } }",
            "selectionSetId": 14,
            "path": [
              "productById"
            ],
            "requires": [
              {
                "variable": "__fusion_exports__1"
              }
            ]
          },
          {
            "type": "Compose",
            "selectionSetIds": [
              14
            ]
          }
        ]
      },
      "state": {
        "__fusion_exports__1": "Product_id"
      }
    },
    "queryPlanHash": "646F6E278928C0A79545D23D1E1D6E45CDD9A71E"
  }
}
```

### HotChocolate Version: 15.1.13