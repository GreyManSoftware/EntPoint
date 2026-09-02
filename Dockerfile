FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app
RUN mkdir -p /app/data && chown "$APP_UID:$APP_UID" /app/data
USER $APP_UID

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY EntPoint.slnx ./
COPY src/EntPoint.Core/EntPoint.Core.csproj src/EntPoint.Core/
COPY src/EntPoint.Collector/EntPoint.Collector.csproj src/EntPoint.Collector/
RUN dotnet restore src/EntPoint.Collector/EntPoint.Collector.csproj

COPY src/ src/
RUN dotnet publish src/EntPoint.Collector/EntPoint.Collector.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM base AS runtime
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EntPoint.Collector.dll"]
