# 🚀 Kubernetes Deployment - AgroSolutions Workers

## 📋 Visão Geral

Manifests de produção para deploy dos Workers de Sensores no AWS EKS.

## 📁 Estrutura de Arquivos

```
k8s/production/
├── namespace.yaml           # Namespace agrosolutions-workers
├── infrastructure.yaml     # ServiceAccount com IRSA (sem credenciais explícitas)
├── secrets.yaml            # Nota: credenciais AWS via IRSA, não K8s secrets
├── configmaps.yaml         # Configurações dos workers (queues, DB, OTEL)
├── postgres.yaml           # PostgreSQL com PVC
├── deployments.yaml        # Worker Host e Generator
├── services.yaml           # Services para acesso interno
├── hpa.yaml               # Autoscaling do Generator
├── resource-configs.yaml   # PDB, NetworkPolicy, Quotas
├── observability.yaml      # Prometheus monitoring e alerts
└── README.md              # Este arquivo
```

## 🔧 Ordem de Aplicação

```bash
# 1. Namespace
kubectl apply -f namespace.yaml

# 2. ServiceAccount com IRSA (sem credenciais AWS explícitas)
kubectl apply -f infrastructure.yaml

# 3. ConfigMaps
kubectl apply -f configmaps.yaml

# 4. PostgreSQL (com PVC)
kubectl apply -f postgres.yaml

# 5. Services
kubectl apply -f services.yaml

# 6. Deployments
kubectl apply -f deployments.yaml

# 7. HPA (Horizontal Pod Autoscaler)
kubectl apply -f hpa.yaml

# 8. Resource Configs (PDB, NetworkPolicy, Quotas)
kubectl apply -f resource-configs.yaml

# 9. Observability (ServiceMonitor, Alerts)
kubectl apply -f observability.yaml
```

## 🚀 Deploy Completo

```bash
# Deploy tudo de uma vez (após configurar secrets)
kubectl apply -f k8s/production/
```

## 🔍 Verificação

```bash
# Status dos pods
kubectl get pods -n agrosolutions-workers

# Logs do Worker Host
kubectl logs -f deployment/worker-host -n agrosolutions-workers

# Logs do Worker Generator
kubectl logs -f deployment/worker-generator -n agrosolutions-workers

# Status do HPA
kubectl get hpa -n agrosolutions-workers

# Métricas de recursos
kubectl top pods -n agrosolutions-workers
```

## 🎯 Configurações Importantes

### Worker Host (Registration)
- **Replicas**: 1 (SQS consumer - evita processamento duplicado)
- **Resources**: 256Mi-512Mi RAM, 200m-1000m CPU
- **Probe**: Liveness verificando processo .NET
- **InitContainer**: Aguarda PostgreSQL estar pronto

### Worker Generator
- **Replicas**: 2-8 (via HPA)
- **Resources**: 256Mi-512Mi RAM, 200m-1000m CPU  
- **HPA**: Escala baseado em CPU (70%) e Memória (80%)
- **Interval**: 10s (configurável via ConfigMap)

### PostgreSQL
- **Storage**: 10Gi GP3 (PersistentVolumeClaim)
- **Resources**: 256Mi-1Gi RAM, 250m-1000m CPU
- **Probes**: Liveness e Readiness com pg_isready

## 🔐 Security

- **IRSA**: Pods usam IAM Role via ServiceAccount (sem credenciais AWS no cluster)
- **OIDC GitHub Actions**: CI/CD autentica na AWS via role assumption, não com access keys
- **RunAsNonRoot**: Todos os containers rodam como usuário não-root
- **Capabilities**: DROP ALL + mínimo necessário
- **NetworkPolicy**: Restringe tráfego apenas ao necessário
- **PodDisruptionBudget**: Garante disponibilidade durante updates

## 📊 Observability

### Métricas (Prometheus)
- ServiceMonitor configurado para scraping em `/metrics`
- Intervalo: 30s

### Alertas Configurados
- **Critical**: Worker down, PostgreSQL down, NoPodsAvailable
- **Warning**: High CPU/Memory, Frequent restarts

### Dashboards Sugeridos
- CPU/Memory usage por worker
- SQS message processing rate
- Data generation throughput
- Database connection pool

## 🔄 Rollout e Rollback

```bash
# Verificar status do rollout
kubectl rollout status deployment/worker-host -n agrosolutions-workers
kubectl rollout status deployment/worker-generator -n agrosolutions-workers

# Histórico de rollouts
kubectl rollout history deployment/worker-host -n agrosolutions-workers

# Rollback para versão anterior
kubectl rollout undo deployment/worker-host -n agrosolutions-workers
```

## 🐛 Troubleshooting

### Worker não inicia
```bash
# Verificar eventos
kubectl describe pod <pod-name> -n agrosolutions-workers

# Verificar logs do initContainer
kubectl logs <pod-name> -c wait-for-postgres -n agrosolutions-workers
```

### SQS não consome mensagens
```bash
# Verificar secrets AWS
kubectl get secret workers-aws-secrets -n agrosolutions-workers -o yaml

# Testar credenciais AWS
kubectl exec -it deployment/worker-host -n agrosolutions-workers -- env | grep AWS
```

### Generator não envia dados
```bash
# Verificar ConfigMap
kubectl get configmap worker-generator-config -n agrosolutions-workers -o yaml

# Testar conectividade com Ingestion API
kubectl exec -it deployment/worker-generator -n agrosolutions-workers -- \
  curl -v http://ingestion-api-service.agrosolutions-ingestion.svc.cluster.local/health
```

## 📝 Notas

- **StorageClass**: Ajuste `storageClassName: gp3` no postgres.yaml conforme seu cluster
- **Image Registry**: Altere `316295889438.dkr.ecr.sa-east-1.amazonaws.com` para seu ECR
- **Namespace Labels**: NetworkPolicy usa labels, ajuste conforme sua arquitetura
- **Resource Limits**: Ajuste requests/limits baseado em profiling real
