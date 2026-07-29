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
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NamViet.ERP.Api.dll"]
