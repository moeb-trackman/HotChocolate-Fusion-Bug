dotnet tool update -g ChilliCream.Nitro.CommandLine --version "16.0.8" --allow-downgrade --source "https://api.nuget.org/v3/index.json"

cd src\quick-start.Ordering
dotnet run -- schema export

cd ..\..

cd src\quick-start.Products
dotnet run -- schema export

cd ..\..

cd src\quick-start.Gateway
nitro fusion compose -a gateway.far -f ../quick-start.Products -f ../quick-start.Ordering --enable-global-object-identification

cd ..\..