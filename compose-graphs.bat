:: dotnet tool install -g HotChocolate.Fusion.CommandLine
cd src\quick-start.Ordering
dotnet run -- schema export --output schema.graphql
fusion subgraph pack

cd ..\..

cd src\quick-start.Products
dotnet run -- schema export --output schema.graphql
fusion subgraph pack

cd ..\..

cd src\quick-start.Gateway
fusion compose -p gateway.fgp -s ../quick-start.Products -s ../quick-start.Ordering --enable-nodes

cd ..\..