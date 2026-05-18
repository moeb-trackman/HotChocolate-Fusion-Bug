# HotChocolate Fusion Bug

Reproduce of a bug with HotChocolate Fusion about not propagating subgraphs request-level error to the gateway response!

## Check Subgraph Response

```csharp
public static IOrder[] GetOrders(IResolverContext context)
{
    context.ReportError("This is an error message from the resolver.");
    return _orders;
}
```

Run the "ordering" project and navigate to <http://localhost:5002/graphql>, then run the query and **it will return an request-level error**.

```graphql
query {
  orders {
    name
  }
}

# -------------- Response --------------
{
  # Request-Level Error
  "errors": [
    {
      "message": "This is an error message from the resolver.",
      "path": [
        "orders"
      ]
    }
  ],
  "data": {
    "orders": []
  }
}
```

## Check Gateway Response

Run the "gateway" and navigate to <https://localhost:10079/graphql/>, then run the query and you see the **gateway does not propagate the request-level error**.

```graphql
query {
  orders {
    name
  }
}

# -------------- Response --------------
{
  "data": {
    "orders": []
  }
  # NO ERROR !
}
```

### HotChocolate Version: 16.1.0-p.1.11