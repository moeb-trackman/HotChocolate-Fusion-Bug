dotnet tool update -g ChilliCream.Nitro.CommandLine --version "16.1.0-p.1.11"

cd src\quick-start.Ordering
dotnet run -- schema export

cd ..\..

cd src\quick-start.Products
dotnet run -- schema export

cd ..\..

cd src\quick-start.Gateway
nitro fusion compose -a gateway.far -f ../quick-start.Products -f ../quick-start.Ordering --enable-global-object-identification

cd ..\..