# ChangeLogMonitor.Embedded

Library for embedding audit functionality into your application.

## Features

- EF Core interceptor registration
- Configuration loading from YAML
- API endpoints for viewing audit logs
- Full integration with host application

## Usage

```csharp
// In Program.cs or Startup.cs

// 1. Add services
builder.Services.AddChangeLogMonitor(options =>
{
    options.ConfigFilePath = "changelog-config.yaml";
    options.EnableInterceptor = true;
    options.EnableApi = true;
    options.ApiBasePath = "/changelog"; // API will be at /changelog/healthz, /changelog/diffs, etc.
});

// 2. Configure EF Core with interceptor
builder.Services.AddDbContext<YourDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString);
    options.UseChangeLogInterceptor(sp); // Add this line
});

var app = builder.Build();

// 3. Map endpoints (if EnableApi is true)
app.UseChangeLogMonitor();
```

## Configuration

Create a `changelog-config.yaml` file in your application root:

```yaml
version: 1
mode: whitelist

entities:
  User:
    enabled: true
    fields:
      Name:
        action: track
      Email:
        action: mask
      Password:
        action: ignore
```

## API Endpoints

When `EnableApi` is true, the following endpoints are available:

- `GET {ApiBasePath}/healthz` - Combined health check
- `GET {ApiBasePath}/aggregator/healthz` - Aggregator health
- `GET {ApiBasePath}/finalizer/healthz` - Finalizer health
- `GET {ApiBasePath}/diffs` - List all diffs
- `GET {ApiBasePath}/diffs/{tableName}/{entityId}` - Get diffs for entity
- `GET {ApiBasePath}/diffs/tx/{transactionId}` - Get diffs for transaction
- `GET {ApiBasePath}/debug/config` - View loaded configuration

See full documentation: [DOCUMENTATION.md](../DOCUMENTATION.md#changelogmonitorembedded)
