FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# This step is separated to leverage Docker's build cache
COPY nanoleaf-controller.csproj .
RUN dotnet restore nanoleaf-controller.csproj

COPY . .

RUN dotnet publish nanoleaf-controller.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app .

EXPOSE 80
EXPOSE 443

ENTRYPOINT ["dotnet", "nanoleaf-controller.dll"]
