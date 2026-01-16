# .NET Aspire Orchestration

This directory contains the .NET Aspire AppHost project that orchestrates the FxMovieAlert application.

## What is .NET Aspire?

.NET Aspire is an opinionated, cloud-ready stack for building observable, production-ready, distributed applications. It provides:

- **Service orchestration**: Start and manage multiple services with a single command
- **Service discovery**: Automatic service-to-service communication
- **Observability**: Built-in OpenTelemetry support for metrics, traces, and logs
- **Resilience**: Automatic retry policies and circuit breakers

## Running with Aspire

To start the FxMovieAlert application using the Aspire orchestrator:

```bash
cd AppHost
dotnet run
```

Or from the solution root:

```bash
dotnet run --project AppHost/AppHost.csproj
```

This will:
1. Start the Aspire dashboard (typically at http://localhost:15888)
2. Launch the Site project with full observability
3. Provide real-time metrics, traces, and logs in the dashboard

## Dashboard

The Aspire dashboard provides:
- **Resources view**: See all running services and their health
- **Console logs**: View real-time logs from all services
- **Traces**: Distributed tracing across services
- **Metrics**: Performance metrics and monitoring

Access the dashboard at the URL displayed when you run `dotnet run`.

## Project Structure

- **AppHost**: The orchestration host that defines and starts all services
- **ServiceDefaults**: Shared configuration for all Aspire-enabled services
- **Site**: The web application (referenced by AppHost)

## Configuration

The AppHost is configured in [Program.cs](Program.cs) and defines:
- The Site project as a managed resource
- External HTTP endpoints for the Site

The ServiceDefaults project provides:
- OpenTelemetry configuration
- Service discovery
- HTTP client resilience handlers
- Health check defaults

## Development

When running with Aspire during development:
- All services automatically get observability instrumentation
- Service-to-service calls use resilient HTTP clients
- Health checks are automatically configured
- Logs, traces, and metrics are exported to the Aspire dashboard
