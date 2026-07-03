# HotChocolate Fusion Bug

Reproducing a Fusion bug with **Inheritance**!

## Reproduce

### Overview

We have two graphql serivces (ordering and products), this is the schematic diagram and a gatewgay in front of them to aggregate the result

```
+-----------------------------------------+  |  +------------------+
|             ORDERING SERVICE            |  |  | PRODUCTS SERVICE |
+-----------------------------------------+  |  +------------------+
                                             |
          +----------------------+           |
          |      OrderBase       |           |
          +----------------------+           |
          | string Name          |           |
          +----------------------+           |
                     ▲                       |
                     │                       |
                     │                       |
     +--------------------------------+      |
     |         MultiOrderBase         |      |
     +--------------------------------+      |
     | OrderItem[] Items              |      |
     +--------------------------------+      |
              ▲              ▲               |
              │              │               |
              │              │               |
 +----------------+    +----------------+    |
 |     OrderA     |    |     OrderB     |    |
 +----------------+    +----------------+    |
 |(no new members)|    |(no new members)|    |
 +----------------+    +----------------+    |
                                             |
     +--------------------------------+      |       +-------------+
     |           OrderItem            |      |       |   Product   |
     +--------------------------------+      |       +-------------+
     | string ProductId               | -----|-----> | Id          |
     +--------------------------------+      |       | Name        |
                                             |       | Description |
                                             |       +-------------+
                                             |
```

### Running the Query

Run the aspire host and navigate to gateway <https://localhost:10079/graphql/>, then run the query and it will resolve all products as `null`

```graphql
query {
  orders {
    ...OrderBaseFragment
  }
}
fragment OrderBaseFragment on OrderBase {
  __typename
  name
  ...MultiOrderBaseFragment
}
# 🔶 on MultiOrderBase
fragment MultiOrderBaseFragment on MultiOrderBase { 
  __typename
  items {
    product {
      id
      name
      description
    }
  }
}

# ---------- Response ----------
{
  "data": {
    "orders": [
      {
        "__typename": "OrderA",
        "name": "Order A",
        "items": [
          {
            "product": {
              "id": "UHJvZHVjdDoxMDAx",
              "name": null,        # ❌ <------------ null!
              "description": null  # ❌ <------------ null!
            }
          },
          {
            "product": {
              "id": "UHJvZHVjdDoxMDAy",
              "name": null,        # ❌ <------------ null!
              "description": null  # ❌ <------------ null!
            }
          }
        ]
      },
      {
        "__typename": "OrderB",
        "name": "Order B",
        "items": [
          {
            "product": {
              "id": "UHJvZHVjdDoxMDAz",
              "name": null,        # ❌ <------------ null!
              "description": null  # ❌ <------------ null!
            }
          },
          {
            "product": {
              "id": "UHJvZHVjdDoxMDA0",
              "name": null,        # ❌ <------------ null
              "description": null  # ❌ <------------ null 
            }
          }
        ]
      }
    ]
  }
}
```

#### Querying Derived Types

Running the query with `OrderA` or `OrderB` will resolve the corresponding type correctly (so it works with derived types but not with base type)

```graphql
query {
  orders {
    ...OrderBaseFragment
  }
}
fragment OrderBaseFragment on OrderBase {
  __typename
  name
  ...MultiOrderBaseFragment
}
# 🔶 on OrderA (concrete class)
fragment MultiOrderBaseFragment on OrderA {
  __typename
  items {
    product {
      id
      name
      description
    }
  }
}

# ---------- Response ----------
{
  "data": {
    "orders": [
      {
        "__typename": "OrderA",
        "name": "Order A",
        "items": [
          {
            "product": {
              "id": "UHJvZHVjdDoxMDAx",
              "name": "Product 1",         # ✅ <------------ has value 
              "description": "Description for Product 1"  # ✅ <------------ has value 
            }
          },
          {
            "product": {
              "id": "UHJvZHVjdDoxMDAy",
              "name": "Product 2",         # ✅ <------------ has value 
              "description": "Description for Product 2"  # ✅ <------------ has value 
            }
          }
        ]
      },
      {
        "__typename": "OrderB",
        "name": "Order B"                  # Not showing other fields as expected because it's a different type
      }
    ]
  }
}
```

```graphql
query {
  orders {
    ...OrderBaseFragment
  }
}
fragment OrderBaseFragment on OrderBase {
  __typename
  name
  ...MultiOrderBaseFragment
}
# 🔶 on OrderB (concrete class)
fragment MultiOrderBaseFragment on OrderB {
  __typename
  items {
    product {
      id
      name
      description
    }
  }
}

# ---------- Response ----------
{
  "data": {
    "orders": [
      {
        "__typename": "OrderA",              # Not showing other fields as expected because it's a different type
        "name": "Order A"
      },
      {
        "__typename": "OrderB",
        "name": "Order B",
        "items": [
          {
            "product": {
              "id": "UHJvZHVjdDoxMDAz",
              "name": "Product 3",            # ✅ <------------ has value 
              "description": "Description for Product 3"  # ✅ <------------ has value 
            }
          },
          {
            "product": {
              "id": "UHJvZHVjdDoxMDA0",
              "name": "Product 4",            # ✅ <------------ has value 
              "description": "Description for Product 4"  # ✅ <------------ has value 
            }
          }
        ]
      }
    ]
  }
}
```

### HotChocolate Version: 16.0.8