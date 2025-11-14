dotnet workload restore
dotnet publish nanoleaf-controller.csproj -c Release -o ./app
docker build -t ghcr.io/mitchfen/nanoleaf-controller:latest .
