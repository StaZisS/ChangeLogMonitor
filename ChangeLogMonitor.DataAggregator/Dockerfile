# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish ChangeLogMonitor.DataAggregator/ChangeLogMonitor.DataAggregator.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:9.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["./ChangeLogMonitor.DataAggregator"]
