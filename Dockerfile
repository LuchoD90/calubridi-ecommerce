FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY backend/Calubridi.Api/Calubridi.Api.csproj backend/Calubridi.Api/
RUN dotnet restore backend/Calubridi.Api/Calubridi.Api.csproj
COPY backend/Calubridi.Api/ backend/Calubridi.Api/
RUN dotnet publish backend/Calubridi.Api/Calubridi.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build --chown=app:app /app .
RUN mkdir -p /app/wwwroot/uploads && chown app:app /app/wwwroot/uploads
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Calubridi.Api.dll"]
