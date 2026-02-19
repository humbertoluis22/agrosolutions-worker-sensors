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
6. Validação de manifests K8s com `kubeval`

**Resultado**: Garante que o código compila e manifests são válidos.

---

### 2️⃣ Build and Push Docker Image

**Execução**: Apenas quando push em `main`

**Dependência**: `build-and-test` deve passar

**Passos**:
1. **Build Multi-Stage Docker Image**
   - Stage 1: Build ambos workers (.NET 10 Alpine)
   - Stage 2: Runtime image com ambos binários
   - Tag com `$(github.sha)` e `latest`
   
2. **Push para ECR**
   - Login no ECR: `316295889438.dkr.ecr.sa-east-1.amazonaws.com`
   - Push da imagem: `agrosolutions-worker:latest`
   - Push também com tag SHA do commit para rastreabilidade

**Resultado**: Imagem Docker disponível no ECR

---

### 3️⃣ Deploy to EKS Production

**Execução**: Apenas quando push em `main`

**Dependência**: `build-and-push` deve passar

**Passos**:
1. **Configurar kubectl**
   - Autentica no EKS cluster: `agrosolutions-eks-cluster`
   - Região: `sa-east-1`

2. **Preparar Secrets**
   - Codifica AWS credentials em base64
   - Substitui variáveis no secrets.yaml via `envsubst`

3. **Apply Manifests**
   ```bash
   kubectl apply -f k8s/production/namespace.yaml
   kubectl apply -f k8s/production/secrets.yaml
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
| `AWS_ACCESS_KEY_ID` | AWS Access Key para ECR/EKS | `aws configure get aws_access_key_id` |
| `AWS_SECRET_ACCESS_KEY` | AWS Secret Key | `aws configure get aws_secret_access_key` |

### Permissões IAM necessárias:

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
ECR_REGISTRY: 316295889438.dkr.ecr.sa-east-1.amazonaws.com
ECR_REPOSITORY: agrosolutions-worker
EKS_CLUSTER_NAME: agrosolutions-eks-cluster
K8S_NAMESPACE: agrosolutions-workers
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
name: Deploy Workers to EKS

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]
  workflow_dispatch:

env:
  AWS_REGION: sa-east-1
  ECR_REGISTRY: 316295889438.dkr.ecr.sa-east-1.amazonaws.com
  ECR_REPOSITORY: agrosolutions-worker
  EKS_CLUSTER_NAME: agrosolutions-eks-cluster

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      
      - name: Restore dependencies
        run: dotnet restore
      
      - name: Build
        run: dotnet build --no-restore
      
      - name: Test
        run: dotnet test --no-build --verbosity normal

  build-and-push:
    if: github.ref == 'refs/heads/main' && github.event_name == 'push'
    needs: build-and-test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Configure AWS credentials
        uses: aws-actions/configure-aws-credentials@v4
        with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
          aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          aws-region: ${{ env.AWS_REGION }}
      
      - name: Login to ECR
        id: login-ecr
        uses: aws-actions/amazon-ecr-login@v2
      
      - name: Build, tag, and push image
        env:
          IMAGE_TAG: ${{ github.sha }}
        run: |
          docker build -t $ECR_REGISTRY/$ECR_REPOSITORY:$IMAGE_TAG .
          docker tag $ECR_REGISTRY/$ECR_REPOSITORY:$IMAGE_TAG $ECR_REGISTRY/$ECR_REPOSITORY:latest
          docker push $ECR_REGISTRY/$ECR_REPOSITORY:$IMAGE_TAG
          docker push $ECR_REGISTRY/$ECR_REPOSITORY:latest

  deploy:
    if: github.ref == 'refs/heads/main' && github.event_name == 'push'
    needs: build-and-push
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Configure AWS credentials
        uses: aws-actions/configure-aws-credentials@v4
        with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
          aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          aws-region: ${{ env.AWS_REGION }}
      
      - name: Update kubeconfig
        run: |
          aws eks update-kubeconfig --region $AWS_REGION --name $EKS_CLUSTER_NAME
      
      - name: Deploy to EKS
        env:
          AWS_ACCESS_KEY_ID_B64: ${{ secrets.AWS_ACCESS_KEY_ID }}
          AWS_SECRET_ACCESS_KEY_B64: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
        run: |
          # Encode secrets
          export AWS_ACCESS_KEY_ID_B64=$(echo -n "$AWS_ACCESS_KEY_ID_B64" | base64)
          export AWS_SECRET_ACCESS_KEY_B64=$(echo -n "$AWS_SECRET_ACCESS_KEY_B64" | base64)
          
          # Apply manifests
          kubectl apply -f k8s/production/namespace.yaml
          envsubst < k8s/production/secrets.yaml | kubectl apply -f -
          kubectl apply -f k8s/production/configmaps.yaml
          kubectl apply -f k8s/production/postgres.yaml
          kubectl apply -f k8s/production/services.yaml
          kubectl apply -f k8s/production/deployments.yaml
          kubectl apply -f k8s/production/hpa.yaml
          kubectl apply -f k8s/production/resource-configs.yaml
          kubectl apply -f k8s/production/observability.yaml
          
          # Wait for rollout
          kubectl rollout status deployment/worker-host -n agrosolutions-workers --timeout=10m
          kubectl rollout status deployment/worker-generator -n agrosolutions-workers --timeout=10m
```

---

## ✅ Checklist Pré-Deploy

- [ ] Secrets configurados no GitHub
- [ ] ECR repository `agrosolutions-worker` criado
- [ ] EKS cluster `agrosolutions-eks-cluster` ativo
- [ ] kubectl tem permissões adequadas
- [ ] StorageClass configurado para PVC (GP3)
- [ ] Namespace Ingestion API existe (para cross-namespace calls)
- [ ] Prometheus Operator instalado (para ServiceMonitor)
- [ ] AWS SQS queue criada e acessível
