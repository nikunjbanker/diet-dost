# DevOps, Infrastructure & Aspire Orchestration
> **Specification Version**: `v1.1.0 (Production & Living SDD)`  
> **Host Framework**: .NET Aspire 11 RC (`Aspire.Hosting.AppHost`)  
> **Runtime**: .NET 11 RC  

---

## 1. Aspire AppHost Topology

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Web Gateway hosting the Linear.app PWA and microservice endpoints
builder.AddProject("web-gateway", "../Nutrition.WebGateway/Nutrition.WebGateway.csproj")
       .WithEnvironment("Database__Provider", "Sqlite")
       .WithEnvironment("ConnectionStrings__DefaultConnection", "Data Source=diettracker.db");

builder.Build().Run();
```

---

## 2. OpenTelemetry (OTel) Pipeline

- **Distributed Tracing**: Spans emitted across gateway requests, AI vision calls, and database operations.
- **Metrics**:
  - `dietdost.meals.uploaded_count`: Counter of uploaded meal photos.
  - `dietdost.meals.confidence_gated_failed`: Counter of photos flagged for retake (<70%).
  - `dietdost.ai.latency_ms`: Histogram of multimodal vision parsing latency.
  - `dietdost.calories.consumed_total`: Cumulative calories logged.

---

## 3. Local Execution & Launch Runbook

```bash
# 1. Restore & Build Solution
dotnet build --framework net11.0

# 2. Run Test Harness
dotnet test

# 3. Launch App via WebGateway
dotnet run --project src/Nutrition.WebGateway/Nutrition.WebGateway.csproj --framework net11.0 --launch-profile http
```

Access the UI at `http://localhost:5240/`.
