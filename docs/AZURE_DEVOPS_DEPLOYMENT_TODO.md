# Azure DevOps & Deployment Feature TODO List: Diet Dost
> **Specification & Execution Roadmap**: Production Cloud Deployment  
> **Container Engine**: Podman 5.7.0 (WSL2 Backend, OCI Compliant)  
> **Target Cloud Host**: Azure Container Apps (ACA) / Serverless Kubernetes  
> **Runtime**: .NET 11 RC (`net11.0`) on Linux Container  
> **Domain & Security**: Custom Domain Mapping with Free Azure Managed TLS 1.3  

---

## 1. Cloud Architecture Blueprint

```mermaid
graph TD
    User([End User / Mobile PWA]) -->|HTTPS / TLS 1.3| DNS[Custom Domain DNS<br/>diet.yourdomain.com]
    DNS -->|CNAME: diet -> *.azurecontainerapps.io<br/>TXT: asuid.diet -> Domain Verification ID| ACA_Env[Azure Container Apps Environment<br/>Managed TLS / Ingress]
    
    subgraph Azure_Cloud ["Azure Cloud (Secure Perimeter)"]
        ACA_Env --> App[WebGateway Container<br/>.NET 11 Web API + Static PWA<br/>Port 8080]
        
        App -.->|Managed Identity| KV[Azure Key Vault<br/>AI Keys & Secrets]
        App -.->|OTel Logs & Traces| AppInsights[Azure Monitor / Log Analytics]
        
        alt Strategy A: Durable SQLite (Low Cost ~$0-5/mo)
            App -->|Volume Mount /app/data| AzureFile[Azure Files SMB Volume<br/>diettracker.db]
        else Strategy B: Azure PostgreSQL (Enterprise ~$12-25/mo)
            App -->|PostgreSQL Protocol| AzurePg[Azure Database for PostgreSQL<br/>Flexible Server]
        end
    end
    
    App -->|HTTPS Outbound| Gemini[Google AI Gemini API]
    App -->|HTTPS Outbound| AzureOpenAI[Azure OpenAI Service]
    
    subgraph Local_DevOps ["Local DevOps (Podman Engine)"]
        Developer([Developer / CLI]) -->|podman build / run| LocalPodman[Podman 5.7.0 Engine]
        LocalPodman -->|podman login via az acr token| ACR[Azure Container Registry]
        ACR -->|Deploy Revision| ACA_Env
    end
```

---

## 2. Master Feature TODO List

### Phase 1: Containerization & Podman Local Engineering
- [ ] **1.1 Create Multi-Stage `Containerfile` / `Dockerfile`**
  - [ ] Target official `mcr.microsoft.com/dotnet/sdk:11.0-preview` for build stage.
  - [ ] Target `mcr.microsoft.com/dotnet/aspnet:11.0-preview-noble-chiseled` (or non-root Linux) for runtime stage.
  - [ ] Configure `ASPNETCORE_URLS=http://+:8080` and `ASPNETCORE_ENVIRONMENT=Production`.
  - [ ] Configure persistent data directory `/app/data` with non-root permissions for SQLite storage.
- [ ] **1.2 Create `.dockerignore`**
  - [ ] Exclude `bin/`, `obj/`, `.git/`, `.gemini/`, `*.db`, test media (`test_meal.jpg`), and node modules from context.
- [ ] **1.3 Create Local `podman-compose.yml`**
  - [ ] Define `web-gateway` service mounting `./data:/app/data` for local container testing.
- [ ] **1.4 Local Podman Verification Gate**
  - [ ] Build image locally: `podman build -t diet-dost-web:local -f Containerfile .`
  - [ ] Run container locally: `podman run -d --name diet-dost-test -p 5240:8080 diet-dost-web:local`
  - [ ] Smoke test endpoints: `curl http://localhost:5240/api/clinical/guidelines`
  - [ ] Clean up local container: `podman stop diet-dost-test && podman rm diet-dost-test`

---

### Phase 2: Azure Infrastructure as Code (IaC) & Resource Provisioning
- [ ] **2.1 Azure Subscription & Resource Group Setup**
  - [ ] Select deployment region (e.g. `centralindia`, `eastus`, or `westeurope`).
  - [ ] Create resource group: `az group create --name rg-dietdost-prod --location <region>`
- [ ] **2.2 Observability & Log Analytics**
  - [ ] Provision Log Analytics Workspace (`log-dietdost-prod`).
  - [ ] Provision Application Insights for OpenTelemetry ingestion.
- [ ] **2.3 Azure Container Registry (ACR)**
  - [ ] Create ACR (Basic SKU): `az acr create --resource-group rg-dietdost-prod --name crdietdostprod --sku Basic --admin-enabled false`
  - [ ] Verify ACR status and retrieve login server name (`crdietdostprod.azurecr.io`).
- [ ] **2.4 Storage & Database Strategy Provisioning**
  - [ ] *If Strategy A (SQLite on Azure Files)*:
    - [ ] Create Azure Storage Account: `stgddprod`
    - [ ] Create Azure File Share: `az storage share create --name sqlite-share --account-name stgddprod`
  - [ ] *If Strategy B (Azure PostgreSQL)*:
    - [ ] Create Azure Database for PostgreSQL Flexible Server (Burstable B1ms).
    - [ ] Configure firewall rules to allow Azure Container Apps access.
- [ ] **2.5 Azure Container Apps Environment (CAE)**
  - [ ] Create managed environment:
    ```bash
    az containerapp env create \
      --name cae-dietdost-prod \
      --resource-group rg-dietdost-prod \
      --location <region> \
      --logs-workspace-id <LOG_WORKSPACE_ID> \
      --logs-workspace-key <LOG_WORKSPACE_KEY>
    ```
  - [ ] Link Azure File share to CAE storage mount (for Strategy A).

---

### Phase 3: Podman ACR Authentication & Image Ingestion
- [ ] **3.1 Podman Authentication to Azure Container Registry**
  - [ ] Retrieve dynamic OAuth token from Azure CLI:
    ```powershell
    $ACR_NAME = "crdietdostprod"
    $TOKEN = az acr login --name $ACR_NAME --expose-token --output tsv --query accessToken
    ```
  - [ ] Authenticate Podman using the token:
    ```powershell
    podman login "$ACR_NAME.azurecr.io" -u 00000000-0000-0000-0000-000000000000 -p $TOKEN
    ```
- [ ] **3.2 Tag and Push Production Image**
  - [ ] Tag build: `podman tag diet-dost-web:local "$ACR_NAME.azurecr.io/diet-dost-web:v1.0"`
  - [ ] Push image: `podman push "$ACR_NAME.azurecr.io/diet-dost-web:v1.0"`
  - [ ] Verify repository in ACR: `az acr repository list --name $ACR_NAME --output table`

---

### Phase 4: Container App Deployment & Configuration
- [ ] **4.1 Create / Deploy Azure Container App**
  - [ ] Enable Managed Identity on Container App for secure ACR pulls.
  - [ ] Deploy container with external ingress:
    ```bash
    az containerapp create \
      --name diet-dost-web \
      --resource-group rg-dietdost-prod \
      --environment cae-dietdost-prod \
      --image crdietdostprod.azurecr.io/diet-dost-web:v1.0 \
      --target-port 8080 \
      --ingress external \
      --min-replicas 1 \
      --max-replicas 3 \
      --cpu 0.5 --memory 1.0Gi
    ```
- [ ] **4.2 Configure Secrets & Environment Variables**
  - [ ] Set `AI__Provider=GoogleAI` (or `AzureOpenAI`).
  - [ ] Securely add `AI__GoogleAI__ApiKey` as a Container App secret.
  - [ ] If using Azure Files SQLite: configure volume mount `/app/data` and `ConnectionStrings__DefaultConnection="Data Source=/app/data/diettracker.db"`.
  - [ ] If using PostgreSQL: configure `Database__Provider=PostgreSql` and connection string secret.
- [ ] **4.3 Ingress Verification**
  - [ ] Test the default Azure FQDN: `https://diet-dost-web.<env-hash>.<region>.azurecontainerapps.io/`

---

### Phase 5: Custom Domain Mapping & Free Managed TLS
- [ ] **5.1 Retrieve Ingress FQDN & Domain Verification ID**
  - [ ] FQDN: `diet-dost-web.<env-hash>.<region>.azurecontainerapps.io`
  - [ ] Retrieve Verification ID:
    ```bash
    az containerapp show \
      --name diet-dost-web \
      --resource-group rg-dietdost-prod \
      --query "properties.configuration.ingress.fqdn" -o tsv
    
    az containerapp env show \
      --name cae-dietdost-prod \
      --resource-group rg-dietdost-prod \
      --query "properties.customDomainConfiguration.customDomainVerificationId" -o tsv
    ```
- [ ] **5.2 Configure DNS Records at Domain Registrar**
  - [ ] *For Subdomain (`diet.yourdomain.com` or `app.yourdomain.com`)*:
    - [ ] `CNAME`: Host `diet` $\rightarrow$ Target `<app-name>.<env-hash>.<region>.azurecontainerapps.io`
    - [ ] `TXT`: Host `asuid.diet` $\rightarrow$ Target `<Verification-ID>`
  - [ ] *For Apex Domain (`yourdomain.com`)*:
    - [ ] `A`: Host `@` $\rightarrow$ Target `<Static-IP-of-CAE>`
    - [ ] `TXT`: Host `asuid` $\rightarrow$ Target `<Verification-ID>`
- [ ] **5.3 Verify DNS Propagation**
  - [ ] Check CNAME: `Resolve-DnsName -Name diet.yourdomain.com -Type CNAME`
  - [ ] Check TXT: `Resolve-DnsName -Name asuid.diet.yourdomain.com -Type TXT`
- [ ] **5.4 Bind Custom Domain & Issue Free Managed Certificate**
  - [ ] Bind hostname to Container App:
    ```bash
    az containerapp hostname add \
      --resource-group rg-dietdost-prod \
      --name diet-dost-web \
      --hostname diet.yourdomain.com
    ```
  - [ ] Issue free Azure Managed Certificate:
    ```bash
    az containerapp hostname bind \
      --resource-group rg-dietdost-prod \
      --name diet-dost-web \
      --hostname diet.yourdomain.com \
      --environment cae-dietdost-prod \
      --validation-method CNAME
    ```
  - [ ] Confirm certificate binding status: `az containerapp hostname list --name diet-dost-web --resource-group rg-dietdost-prod`

---

### Phase 6: Automated CI/CD Pipeline (GitHub Actions / Azure DevOps)
- [ ] **6.1 Configure Workload Identity Federation (OIDC)**
  - [ ] Create Azure AD App Registration & Service Principal.
  - [ ] Configure Federated Credential linking GitHub repository `main` branch to Azure.
- [ ] **6.2 Create CI/CD Workflow (`.github/workflows/deploy.yml`)**
  - [ ] Trigger on push to `main`.
  - [ ] Step 1: `dotnet test` (Zero failure gate).
  - [ ] Step 2: Login to Azure via OIDC.
  - [ ] Step 3: Build & push container image using Podman or GitHub Docker action.
  - [ ] Step 4: Azure Container App revision deploy with zero-downtime traffic switch.

---

### Phase 7: Post-Deployment Verification & Living Documentation
- [ ] **7.1 Live Health & Functional Testing**
  - [ ] Verify HTTPS handshake and TLS 1.3 cipher negotiation: `curl -Iv https://diet.yourdomain.com/api/clinical/guidelines`
  - [ ] Verify PWA installability and ServiceWorker caching on mobile device.
  - [ ] Test AI meal photo analysis and text estimation.
  - [ ] Test container restart and verify SQLite / Postgres data persistence.
- [ ] **7.2 Living SDD Synchronization**
  - [ ] Update `docs/sdd/05_devops_and_infrastructure.md` with production topology and runbook.
  - [ ] Update `docs/sdd/00_sdd_index.md` inventory.
  - [ ] Append audit log entry in `docs/sdd/07_living_documentation_log.md`.

---

## 3. Quick Reference Command Cheat Sheet

### Podman Local Workflow
```powershell
# Build image
podman build -t diet-dost-web:latest -f Containerfile .

# Run locally
podman run -d --name diet-dost -p 5240:8080 -e AI__GoogleAI__ApiKey="YOUR_KEY" diet-dost-web:latest

# Push to ACR
$TOKEN = az acr login --name crdietdostprod --expose-token --output tsv --query accessToken
podman login crdietdostprod.azurecr.io -u 00000000-0000-0000-0000-000000000000 -p $TOKEN
podman tag diet-dost-web:latest crdietdostprod.azurecr.io/diet-dost-web:v1.0
podman push crdietdostprod.azurecr.io/diet-dost-web:v1.0
```

### Domain & Certificate CLI
```bash
# Add custom domain
az containerapp hostname add -g rg-dietdost-prod -n diet-dost-web --hostname diet.yourdomain.com

# Bind managed cert
az containerapp hostname bind -g rg-dietdost-prod -n diet-dost-web --hostname diet.yourdomain.com --environment cae-dietdost-prod --validation-method CNAME
```
