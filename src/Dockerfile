FROM mcr.microsoft.com/dotnet/aspnet:10.0.10-alpine AS final
LABEL org.opencontainers.image.source=https://github.com/mitchfen/nanoleaf-controller
WORKDIR /app
COPY /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "nanoleaf-controller.dll"]
