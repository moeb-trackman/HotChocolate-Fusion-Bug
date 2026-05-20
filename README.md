# HotChocolate Fusion Bug

Reproduce of a bug with HotChocolate Fusion **where it doesn't automatically coerce single values into lists for variables.**

## Reproduce

Run the "gateway" and navigate to <https://localhost:10079/graphql/>, then run the query

### Run Query

```graphql
# Query
query myQuery($id: [Int!]!){
  ordersByIds(ids: $id){
    id
    name
  }
}

# Variable
{
  "id": 1 # NOT array
}
```

### Response

```graphql
{
  "errors": [
    {
      "message": "The value is not a list value.",
      "extensions": {
        "variable": "id"
      }
    }
  ]
}
```

### HotChocolate Version: 16.1.0-p.1.12