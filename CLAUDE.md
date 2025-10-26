# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ChangeLogMonitor is a modular database audit system built on Debezium CDC (Change Data Capture). It tracks and visualizes all data changes in a database and can be deployed either as a standalone service or embedded into an existing application.

## Architecture

### Data Flow
1. **EF Core Interceptor** (ChangeLogMonitor.Interceptor) captures changes during transaction commit and writes them to audit tables with metadata
2. **Debezium** monitors database changes and publishes CDC events to Kafka
3. **Data Aggregator** (ChangeLogMonitor.DataAggregator) consumes Kafka events, joins change data with metadata, and prepares it for display
4. **UI/API** presents the audit log to users with filtering, search, and export capabilities

### Module Dependencies
- **Core** → no dependencies (base abstractions, models, interfaces)
- **Interceptor** → Core (EF Core interceptor logic)
- **Configuration** → Core (YAML config provider)
- **DataAggregator** → Core (Kafka consumer and data aggregation)
- **Api** → Core, DataAggregator (REST API)
- **UI** → Core, Api (Razor Pages UI)
- **Embedded** → Core, Interceptor, Configuration (library for embedding into host app)
- **Standalone** → UI, Api, DataAggregator (standalone deployment, no Interceptor)

### Deployment Modes
- **Embedded**: Use `ChangeLogMonitor.Embedded` NuGet package in your app. Includes Interceptor to capture changes in-process.
- **Standalone**: Deploy `ChangeLogMonitor.Standalone` as separate service. Only reads from Debezium/Kafka, no Interceptor needed.

## Configuration

Configuration is split into two files:

### changelog-config.yaml (Business Logic)
Defines **what** to audit:
- Which entities/tables to track
- Which fields to mask (e.g., passwords, credit cards)
- Metadata to collect (user, timestamp, context)
- Filtering rules (exclude system users, exclude auto-updated fields)

Copy from `changelog-config.example.yaml` to `changelog-config.yaml`

### appsettings.json (Infrastructure)
Defines **how** to connect:
- Database connection strings (ApplicationDatabase, AuditDatabase)
- Kafka settings (bootstrap servers, topics, consumer group)
- Debezium connector configuration
- Storage settings (table prefix, schema, compression)
- .NET logging levels

Copy from `appsettings.example.json` to `appsettings.json`

**Important**: Never commit actual `changelog-config.yaml` or `appsettings.json` files. Only commit `.example` versions.

## Build Commands

```bash
# Build entire solution
dotnet build ChangeLogMonitor.sln

# Build specific project
dotnet build ChangeLogMonitor.Core/ChangeLogMonitor.Core.csproj

# Build in Release mode
dotnet build ChangeLogMonitor.sln -c Release

# Restore dependencies
dotnet restore
```

## Run Commands

```bash
# Run Standalone service
dotnet run --project ChangeLogMonitor.Standalone

# Run UI only (if developed separately)
dotnet run --project ChangeLogMonitor.UI

# Run with specific environment
dotnet run --project ChangeLogMonitor.Standalone --environment Production
```

## Project Structure Notes

All projects are at root level (no `src/` folder). This is intentional for simplicity.

Each module has:
- `/README.md` - module-specific documentation
- Subdirectories with `.gitkeep` files to preserve structure:
  - Core: `/Models`, `/Interfaces`, `/Enums`
  - Interceptor: `/Interceptors`, `/Services`
  - Configuration: `/Providers`, `/Models`
  - DataAggregator: `/Consumers`, `/Services`, `/Models`
  - UI: `/Pages`, `/Components`
  - Api: `/Controllers`
  - Embedded: `/Extensions`

## Target Framework

All projects target `.NET 8.0` with `nullable` reference types enabled.

## Key Technologies

- **Entity Framework Core** - for interceptor and database access
- **Kafka** - message broker for CDC events
- **Debezium** - CDC connector
- **Razor Pages** - for UI
- **YAML** - for audit configuration (use YamlDotNet or similar)
- **PostgreSQL/SQL Server/MySQL** - supported databases
