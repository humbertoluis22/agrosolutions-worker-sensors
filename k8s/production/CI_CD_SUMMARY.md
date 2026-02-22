# 🚀 CI/CD Pipeline - AgroSolutions Workers

## 📋 Visão Geral

Pipeline de CI/CD automatizado via **GitHub Actions** para build, teste e deploy dos Workers de Sensores no AWS EKS.

---

## 🔄 Trigger Automático

O workflow é disparado quando:
- ✅ Push na branch `main` ou `develop`
- ✅ Pull Request para `main`
- ✅ Execução manual via `workflow_dispatch`

---

## 📦 Jobs do Pipeline

### 1️⃣ Build and Test

**Execução**: Sempre (em todos os eventos)

**Passos**:
1. Checkout do código
2. Setup .NET SDK 10.0
3. Restore de dependências
4. Build da solution (ambos workers no mesmo Dockerfile)
5. Execução de testes (quando disponíveis)

**Resultado**: Garante que o código compila e testes passam.

---

### 2️⃣ Deploy to AWS EKS

**Execução**: Apenas quando push em `main`

**Dependência**: `build-and-test` deve passar

**Passos**:
1. **Autenticar na AWS via OIDC**
   - Assume a role `AgroSolutionsGatewayGithubActionsRole` via OIDC (sem access keys no GitHub)
   - Zero credenciais armazenadas como Secrets no repositório

2. **Build e Push da Imagem**
   - Login no ECR via OIDC
   - Build multi-stage e push com tag SHA do commit

3. **Configurar kubectl e Apply Manifests**
   ```bash
   kubectl apply -f k8s/production/namespace.yaml
   kubectl apply -f k8s/production/infrastructure.yaml  # ServiceAccount IRSA
   kubectl apply -f k8s/production/configmaps.yaml
   kubectl apply -f k8s/production/postgres.yaml
   kubectl apply -f k8s/production/services.yaml
   kubectl apply -f k8s/production/deployments.yaml
   kubectl apply -f k8s/production/hpa.yaml
   kubectl apply -f k8s/production/resource-configs.yaml
   kubectl apply -f k8s/production/observability.yaml
   ```

4. **Aguardar Rollout**
   - Worker Host: `kubectl rollout status deployment/worker-host`
   - Worker Generator: `kubectl rollout status deployment/worker-generator`
   - Timeout: 10 minutos

5. **Verificar Health**
   - Verifica pods running
   - Verifica logs para erros críticos

**Resultado**: Workers rodando no EKS

---

## 🔐 Secrets Necessários

Configure em: `Settings > Secrets and variables > Actions > Repository secrets`

### Obrigatórios:

| Secret | Descrição | Como obter |
|--------|-----------|------------|
| `AWS_ROLE_TO_ASSUME` | ARN da IAM Role para OIDC | `arn:aws:iam::316295889438:role/AgroSolutionsGatewayGithubActionsRole` |

> **IRSA**: Com OIDC, nenhuma `AWS_ACCESS_KEY_ID` ou `AWS_SECRET_ACCESS_KEY` é armazenada no GitHub ou no cluster Kubernetes. Os pods assumem a role AWS automaticamente via ServiceAccount annotado.

### Permissões IAM necessárias na role `AgroSolutionsGatewayGithubActionsRole`:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "ecr:GetAuthorizationToken",
        "ecr:BatchCheckLayerAvailability",
        "ecr:PutImage",
        "ecr:InitiateLayerUpload",
        "ecr:UploadLayerPart",
        "ecr:CompleteLayerUpload"
      ],
      "Resource": "*"
    },
    {
      "Effect": "Allow",
      "Action": [
        "eks:DescribeCluster",
        "eks:ListClusters"
      ],
      "Resource": "arn:aws:eks:sa-east-1:316295889438:cluster/agrosolutions-eks-cluster"
    },
    {
      "Effect": "Allow",
      "Action": [
        "sts:GetCallerIdentity"
      ],
      "Resource": "*"
    }
  ]
}
```

---

## 📊 Variáveis de Ambiente do Pipeline

```yaml
AWS_REGION: sa-east-1
ECR_REPOSITORY: agrosolutions-worker
EKS_CLUSTER_NAME: agrosolutions-eks-cluster
DOTNET_VERSION: "10.0.x"
```

---

## 🎯 Estratégia de Deploy

### Rolling Update
- **MaxUnavailable**: 0 (zero downtime)
- **MaxSurge**: 1 (cria novo pod antes de remover antigo)
- **TerminationGracePeriod**: 60s (Worker Host), 30s (Generator)

### Rollback Automático
Se o rollout falhar após 10 minutos:
```bash
kubectl rollout undo deployment/worker-host -n agrosolutions-workers
kubectl rollout undo deployment/worker-generator -n agrosolutions-workers
```

---

## 🔍 Monitoramento Pós-Deploy

### Verificações Automáticas
1. ✅ Pods em status `Running`
2. ✅ Liveness/Readiness probes passando
3. ✅ Logs sem erros críticos nos primeiros 2 minutos

### Métricas Observadas
- CPU/Memory usage
- Pod restart count
- SQS message processing rate (Worker Host)
- API call success rate (Worker Generator)

---

## 🐛 Troubleshooting do Pipeline

### Falha no Build
```bash
# Rode localmente para debug
dotnet build Agrosolutions.Worker.Sensors.slnx
```

### Falha no Push ECR
```bash
# Teste autenticação ECR
aws ecr get-login-password --region sa-east-1 | \
  docker login --username AWS --password-stdin \
  316295889438.dkr.ecr.sa-east-1.amazonaws.com
```

### Falha no Deploy K8s
```bash
# Verifique configuração kubectl local
aws eks update-kubeconfig --region sa-east-1 --name agrosolutions-eks-cluster

# Teste manifests localmente
kubectl apply --dry-run=client -f k8s/production/
```

### Rollout Timeout
```bash
# Verifique eventos do deployment
kubectl describe deployment worker-host -n agrosolutions-workers

# Verifique recursos disponíveis no cluster
kubectl describe nodes | grep -A 5 "Allocated resources"
```

---

## 📝 Workflow Exemplo

`.github/workflows/deploy.yml`:

```yaml
name: AgroSolutions Workers - CI/CD Pipeline

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]
  workflow_dispatch:

env:
  ECR_REPOSITORY: agrosolutions-worker
  EKS_CLUSTER_NAME: agrosolutions-eks-cluster
  AWS_REGION: sa-east-1
  DOTNET_VERSION: "10.0.x"

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore Agrosolutions.Worker.Sensors.slnx
      - run: dotnet build Agrosolutions.Worker.Sensors.slnx -c Release --no-restore
      - run: dotnet test Agrosolutions.Worker.Sensors.slnx --no-build -c Release

  deploy-to-eks:
    needs: build-and-test
    if: github.ref == 'refs/heads/main' && github.event_name == 'push'
    runs-on: ubuntu-latest
    permissions:
      id-token: write   # Required for OIDC
      contents: read
    steps:
      - uses: actions/checkout@v4

      - name: Configure AWS credentials via OIDC
        uses: aws-actions/configure-aws-credentials@v4
        with:
          role-to-assume: ${{ secrets.AWS_ROLE_TO_ASSUME }}
          role-session-name: GitHubActions-WorkersDeploy-${{ github.run_id }}
          aws-region: ${{ env.AWS_REGION }}

      - name: Login to ECR
        id: login-ecr
        uses: aws-actions/amazon-ecr-login@v2

      - name: Build and push Docker image
        env:
          ECR_REGISTRY: ${{ steps.login-ecr.outputs.registry }}
          IMAGE_TAG: ${{ github.sha }}
        run: |
          docker build -t $ECR_REGISTRY/$ECR_REPOSITORY:$IMAGE_TAG -t $ECR_REGISTRY/$ECR_REPOSITORY:latest .
          docker push $ECR_REGISTRY/$ECR_REPOSITORY:$IMAGE_TAG
          docker push $ECR_REGISTRY/$ECR_REPOSITORY:latest

      - name: Configure Kubectl and Deploy
        env:
          ECR_REGISTRY: ${{ steps.login-ecr.outputs.registry }}
          IMAGE_TAG: ${{ github.sha }}
        run: |
          aws eks update-kubeconfig --name $EKS_CLUSTER_NAME --region $AWS_REGION

          sed -i "s|agrosolutions-worker:latest|$ECR_REGISTRY/$ECR_REPOSITORY:$IMAGE_TAG|g" k8s/production/deployments.yaml

          kubectl apply -f k8s/production/namespace.yaml
          kubectl apply -f k8s/production/infrastructure.yaml
          kubectl apply -f k8s/production/configmaps.yaml
          kubectl apply -f k8s/production/postgres.yaml
          kubectl apply -f k8s/production/services.yaml
          kubectl apply -f k8s/production/deployments.yaml
          kubectl apply -f k8s/production/hpa.yaml
          kubectl apply -f k8s/production/resource-configs.yaml
          kubectl apply -f k8s/production/observability.yaml

          kubectl rollout status deployment/worker-host -n agrosolutions-workers --timeout=10m
          kubectl rollout status deployment/worker-generator -n agrosolutions-workers --timeout=10m
```

---

## ✅ Checklist Pré-Deploy

- [ ] Secret `AWS_ROLE_TO_ASSUME` configurado no GitHub (`arn:aws:iam::316295889438:role/AgroSolutionsGatewayGithubActionsRole`)
- [ ] OIDC Provider configurado no AWS IAM para o repositório GitHub
- [ ] ECR repository `agrosolutions-worker` criado
- [ ] EKS cluster `agrosolutions-eks-cluster` ativo
- [ ] IRSA: Trust policy da role inclui o OIDC provider do EKS para o namespace `agrosolutions-workers`
- [ ] StorageClass configurado para PVC (GP3)
- [ ] Namespace Ingestion API existe (para cross-namespace calls)
- [ ] Prometheus Operator instalado (para ServiceMonitor)
- [ ] AWS SQS queue `agrosolutions-identity-events` criada e acessível
