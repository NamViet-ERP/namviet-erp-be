# NamViet ERP API — multi-stage image for Render / container hosts
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/ApiHost/NamViet.ERP.Api/NamViet.ERP.Api.csproj
RUN dotnet publish src/ApiHost/NamViet.ERP.Api/NamViet.ERP.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
# Render injects PORT; default 8080 for local docker runs.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV PORT=8080
# Avoid FileSystemWatcher/inotify exhaustion on small container hosts.
ENV DOTNET_HOSTBUILDER_RELOADCONFIGONCHANGE=false
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
EXPOSE 8080
COPY --from=build /app/publish .
# Shell form so $PORT from Render is applied at runtime.
ENTRYPOINT ["/bin/sh", "-c", "export ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080}; exec dotnet NamViet.ERP.Api.dll"]
