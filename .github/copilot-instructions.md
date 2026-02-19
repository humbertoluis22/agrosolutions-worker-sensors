# Agrosolutions Worker Sensors - AI Coding Agent Instructions

## Project Overview

Two .NET 10.0 BackgroundService workers managing agricultural IoT sensor lifecycle:
- **Registration Worker** ([AgrosolutionsWorkerSensors.Host](src/AgrosolutionsWorkerSensors.Host/)): Consumes AWS SQS messages for sensor CRUD operations → PostgreSQL
- **Generator Worker** ([AgrosolutionsWorkerSensors.Generator](src/AgrosolutionsWorkerSensors.Generator/)): Timer-based mock sensor data generation → HTTP POST to external ingestion API

## Architecture & Data Flow

### Registration Flow
```
AWS SQS (register_sensor) → RegistrationWorker → Parse RegisterSensorMessage → 
  TypeOperation switch (Create/Update/Delete) → PostgreSQL (Sensors table)
```
- **Key File**: [RegistrationWorker.cs](src/AgrosolutionsWorkerSensors.Host/Services/RegistrationWorker.cs)
- Long polling: 20s `WaitTimeSeconds` on SQS, processes 1 message at a time
- Messages deleted from queue only after successful DB commit
- Queue URL configured via `AWS:SqsQueueUrl` in appsettings

### Generator Flow
```
PeriodicTimer (configurable interval) → Query active sensors from PostgreSQL → 
  Generate mock data by SensorType → POST to ApiRawUrl/api/ingestion/sensor
```
- **Key File**: [DataGenerationWorker.cs](src/AgrosolutionsWorkerSensors.Generator/Service/DataGenerationWorker.cs)
- Interval controlled by `GenerationSettings:IntervalSeconds` (default: 5s)
- Uses IHttpClientFactory with named client "ApiRaw"
- Filters `StatusSensor = true` before generation

## Domain Model

### SensorRaw Entity ([SensorRaw.cs](src/AgrosolutionsWorkerSensors.Domain/Entities/SensorRaw.cs))
```csharp
SensorId (Guid, PK) | FieldId (Guid) | DtCreated | TypeSensor (enum) | 
StatusSensor (bool) | TypeOperation (enum)
```

### Enums (Portuguese naming)
- **SensorType**: `Solo` (1), `Silos` (2), `Meteoroligica` (3)
- **TypeOperation**: `Create` (1), `Update` (2), `Delete` (3)

### EF Core Configuration ([SensorContext.cs](src/AgrosolutionsWorkerSensors.Infrastructure/Data/SensorContext.cs))
- Enums stored as **strings** in DB via `HasConversion<string>()`
- Migrations auto-applied on Host startup in [Program.cs](src/AgrosolutionsWorkerSensors.Host/Program.cs#L22-L26)

## Project-Specific Conventions

### Configuration Mapping
Environment variables use **double underscore** for nested JSON:
```bash
appsettings: ConnectionStrings:DefaultConnection
env var:    ConnectionStrings__DefaultConnection
```

### Scoped Services in BackgroundService
Always create scope when accessing DbContext:
```csharp
using var scope = _serviceProvider.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<SensorContext>();
```
See examples in both [RegistrationWorker.cs](src/AgrosolutionsWorkerSensors.Host/Services/RegistrationWorker.cs#L88) and [DataGenerationWorker.cs](src/AgrosolutionsWorkerSensors.Generator/Service/DataGenerationWorker.cs#L46)

### DTOs and Records
Use C# records for message contracts:
```csharp
public record RegisterSensorMessage(Guid FieldId, Guid SensorId, ...);
```
See [RegisterSensorMessage.cs](src/AgrosolutionsWorkerSensors.Host/Dtos/RegisterSensorMessage.cs)

## Build & Deployment

### Local Development
```bash
docker-compose up --build  # Runs both workers + PostgreSQL
```
- Host worker runs by default (Dockerfile ENTRYPOINT)
- Generator overrides entrypoint in [docker-compose.yaml](docker-compose.yaml#L52)
- Expects external network `agrosolutions-network` to exist
- ApiRawUrl points to `http://ingestion-api:5198` (container name from Ingestion project)

### Docker Multi-Stage Build ([Dockerfile](Dockerfile))
Single Dockerfile publishes **both workers** to separate directories:
- `/app/publish/host` → AgrosolutionsWorkerSensors.Registration.dll
- `/app/publish/generator` → AgrosolutionsWorkerSensors.Generator.dll

### Kubernetes Deployment

**Production Manifests**: [k8s/production/](k8s/production/) - Arquitetura completa baseada no padrão do projeto Ingestion

**Estrutura**:
```
namespace.yaml          # agrosolutions-workers namespace
secrets.yaml           # AWS credentials (base64)
configmaps.yaml        # 3 ConfigMaps (common, host-specific, generator-specific)
postgres.yaml          # PostgreSQL + PVC (10Gi GP3)
deployments.yaml       # Worker Host + Generator com probes e security
services.yaml          # ClusterIP services para métricas
hpa.yaml              # Autoscaling do Generator (2-8 réplicas)
resource-configs.yaml  # PDB, NetworkPolicy, ResourceQuota, LimitRange
observability.yaml     # ServiceMonitor + PrometheusRule (11 alertas)
```

**Deploy Order**:
```bash
kubectl apply -f k8s/production/namespace.yaml
envsubst < k8s/production/secrets.yaml | kubectl apply -f -
kubectl apply -f k8s/production/{configmaps,postgres,services,deployments,hpa,resource-configs,observability}.yaml
```

**Legacy Manifests**: [k8s/](k8s/) - Arquivos originais numerados (00-05) mantidos para referência

### Environment Variables Required
**Host Worker:**
- `ConnectionStrings__DefaultConnection`
- `AWS__Region` (e.g., sa-east-1)
- `AWS__SqsQueueUrl`
- `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`

**Generator Worker:**
- `ConnectionStrings__DefaultConnection`
- `ApiRawUrl` (external ingestion API endpoint)
- `GenerationSettings__IntervalSeconds`

## Testing & Debugging

### Running Migrations
```bash
cd src/AgrosolutionsWorkerSensors.Infrastructure
dotnet ef migrations add <MigrationName> --startup-project ../AgrosolutionsWorkerSensors.Host
dotnet ef database update --startup-project ../AgrosolutionsWorkerSensors.Host
```

### Manual SQS Message Publishing
Use AWS CLI or console to send JSON to queue:
```json
{
  "FieldId": "uuid",
  "SensorId": "uuid",
  "DtCreated": "2026-02-19T10:00:00Z",
  "TypeSensor": 1,
  "StatusSensor": true,
  "TypeOperation": 1
}
```

### Common Issues
- **Generator API timeout**: Check `ApiRawUrl` matches external service and interval timeout
- **SQS connection errors**: Verify IAM credentials and queue URL in AWS console
- **Migration errors**: Host project is the startup project for EF Core commands

## Dependencies
- .NET 10.0 SDK
- PostgreSQL 16+ (uses Npgsql EF provider)
- AWS SQS SDK (AWSSDK.SQS 4.0.2.14)
- No explicit caching layer (direct DB queries)
Production Architecture (K8s)

### Resource Management
- **Worker Host**: 1 réplica (SQS consumer), 256Mi-512Mi RAM, 200m-1000m CPU
- **Worker Generator**: 2-8 réplicas (HPA), escala em CPU 70% e memória 80%
- **PostgreSQL**: 1 réplica (Recreate strategy), PVC 10Gi GP3, 256Mi-1Gi RAM
- **Security**: RunAsNonRoot (UID 1001), capabilities DROP ALL, NetworkPolicy restrito

### Observability
- **ServiceMonitor**: Scraping `/metrics` a cada 30s (ambos workers)
- **PrometheusRule**: 11 alertas (Critical: down, NoPodsAvailable; Warning: High CPU/Memory, Restarts)
- **Health Probes**: Liveness via `pgrep` para workers, `pg_isready` para PostgreSQL

### Cross-Service Communication
Generator → Ingestion API via FQDN:
```bash
http://ingestion-api-service.agrosolutions-ingestion.svc.cluster.local
```

### CI/CD Pipeline
1. Build & Test → Build Docker (multi-stage) → Push ECR (`agrosolutions-worker:latest`)
2. Deploy manifests sequencialmente → Rollout status (timeout 10m)
3. Secrets via GitHub Actions, base64 encoded via `envsubst`

Ver [k8s/production/CI_CD_SUMMARY.md](k8s/production/CI_CD_SUMMARY.md) para workflow completo

## Adding New Features

### New Sensor Type
1. Add enum value in [SensorType.cs](src/AgrosolutionsWorkerSensors.Domain/Enums/SensorType.cs)
2. Create DTO record in `Generator/Dtos/SensorData/`
3. Update `GenerateDummyData` switch in [DataGenerationWorker.cs](src/AgrosolutionsWorkerSensors.Generator/Service/DataGenerationWorker.cs#L78)

### New Message Operation
1. Add enum in [TypeOperation.cs](src/AgrosolutionsWorkerSensors.Domain/Enums/TypeOperation.cs)
2. Update `ProcessSensorAsync` switch in [RegistrationWorker.cs](src/AgrosolutionsWorkerSensors.Host/Services/RegistrationWorker.cs#L98)

### Scaling Adjustments
- HPA thresholds: Edit `metrics` in [hpa.yaml](k8s/production/hpa.yaml)
- Resource limits: Adjust `resources` in [deployments.yaml](k8s/production/deployments.yaml)
- Generator interval: Change `GenerationSettings__IntervalSeconds` in [configmaps.yaml](k8s/production/configmaps.yaml
2. Update `ProcessSensorAsync` switch in [RegistrationWorker.cs](src/AgrosolutionsWorkerSensors.Host/Services/RegistrationWorker.cs#L98)
