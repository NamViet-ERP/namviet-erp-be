# NamViet ERP API — multi-stage image for Render / container hosts
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy props + csproj graph trước rồi mới restore để layer restore được cache
# khi chỉ thay đổi source. Không COPY .sln — sln còn Tests/Migrator mà
# .dockerignore loại tests/ và scripts/, restore theo sln sẽ fail ngay.
COPY Directory.Build.props ./
COPY src/libraries/NamViet.ERP.SharedKernel/NamViet.ERP.SharedKernel.csproj src/libraries/NamViet.ERP.SharedKernel/
COPY src/libraries/NamViet.ERP.Application.Abstractions/NamViet.ERP.Application.Abstractions.csproj src/libraries/NamViet.ERP.Application.Abstractions/
COPY src/components/Identity/NamViet.ERP.Modules.Identity.Domain/NamViet.ERP.Modules.Identity.Domain.csproj src/components/Identity/NamViet.ERP.Modules.Identity.Domain/
COPY src/components/Identity/NamViet.ERP.Modules.Identity.Application/NamViet.ERP.Modules.Identity.Application.csproj src/components/Identity/NamViet.ERP.Modules.Identity.Application/
COPY src/components/Identity/NamViet.ERP.Modules.Identity.Infrastructure/NamViet.ERP.Modules.Identity.Infrastructure.csproj src/components/Identity/NamViet.ERP.Modules.Identity.Infrastructure/
COPY src/components/Warehouse/NamViet.ERP.Modules.Warehouse.Domain/NamViet.ERP.Modules.Warehouse.Domain.csproj src/components/Warehouse/NamViet.ERP.Modules.Warehouse.Domain/
COPY src/components/Warehouse/NamViet.ERP.Modules.Warehouse.Application/NamViet.ERP.Modules.Warehouse.Application.csproj src/components/Warehouse/NamViet.ERP.Modules.Warehouse.Application/
COPY src/components/Warehouse/NamViet.ERP.Modules.Warehouse.Infrastructure/NamViet.ERP.Modules.Warehouse.Infrastructure.csproj src/components/Warehouse/NamViet.ERP.Modules.Warehouse.Infrastructure/
COPY src/components/Hr/NamViet.ERP.Modules.Hr.Domain/NamViet.ERP.Modules.Hr.Domain.csproj src/components/Hr/NamViet.ERP.Modules.Hr.Domain/
COPY src/components/Hr/NamViet.ERP.Modules.Hr.Application/NamViet.ERP.Modules.Hr.Application.csproj src/components/Hr/NamViet.ERP.Modules.Hr.Application/
COPY src/components/Hr/NamViet.ERP.Modules.Hr.Infrastructure/NamViet.ERP.Modules.Hr.Infrastructure.csproj src/components/Hr/NamViet.ERP.Modules.Hr.Infrastructure/
COPY src/ApiHost/NamViet.ERP.Api/NamViet.ERP.Api.csproj src/ApiHost/NamViet.ERP.Api/
RUN dotnet restore src/ApiHost/NamViet.ERP.Api/NamViet.ERP.Api.csproj

# Copy phần source còn lại rồi publish (dùng lại package đã restore).
COPY . .
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
# LocalFileStorage ghi vào /app/App_Data (private uploads) — tạo sẵn và cấp quyền
# cho user non-root trước khi hạ quyền.
RUN mkdir -p /app/App_Data/private-uploads && chown -R "$APP_UID:$APP_UID" /app/App_Data
VOLUME ["/app/App_Data"]
# Official aspnet:8.0 sets APP_UID (non-root).
USER $APP_UID
# Shell form so $PORT from Render is applied at runtime.
ENTRYPOINT ["/bin/sh", "-c", "export ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080}; exec dotnet NamViet.ERP.Api.dll"]
