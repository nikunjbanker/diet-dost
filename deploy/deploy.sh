#!/usr/bin/env bash
# ==============================================================================
# Copyright (c) 2026 diet-dost and/or its contributors.
# Licensed under the "GNU Affero General Public License v3.0 only" and
# the "Server Side Public License, v 1"; you may not use this file except
# in compliance with, at your election, the "GNU Affero General Public
# License v3.0 only" or the "Server Side Public License, v 1".
# ==============================================================================
# Automated Azure Container Apps deployment and Podman packaging script
# Target: Azure Container Apps (ACA) + Azure Files SMB Volume Mount (/app/data)
# ==============================================================================

set -euo pipefail

RESOURCE_GROUP="${1:-rg-dietdost-dev}"
LOCATION="${2:-centralindia}"
CUSTOM_DOMAIN="${3:-dev.diet-dost.in}"
ACR_NAME="${4:-}"
ALLOW_DEMO_USERS="${5:-true}"

echo "=== Diet-Dost: Azure Container Apps Deployment (Podman Engine) ==="

# 1. Check Azure CLI
if ! az account show > /dev/null 2>&1; then
    echo "Error: Not logged into Azure CLI. Run 'az login' first." >&2
    exit 1
fi

SUBSCRIPTION_NAME=$(az account show --query name -o tsv)
echo "Active Subscription: $SUBSCRIPTION_NAME"

# 2. Resource Group
echo "Ensuring Resource Group '$RESOURCE_GROUP' in '$LOCATION'..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" -o table

# 3. ACR Setup
if [ -z "$ACR_NAME" ]; then
    RANDOM_SUFFIX=$(( RANDOM % 9000 + 1000 ))
    ACR_NAME="crdietdost${RANDOM_SUFFIX}"
fi

if ! az acr show --name "$ACR_NAME" --resource-group "$RESOURCE_GROUP" > /dev/null 2>&1; then
    echo "Creating ACR '$ACR_NAME'..."
    az acr create --resource-group "$RESOURCE_GROUP" --name "$ACR_NAME" --sku Basic --admin-enabled true -o table
fi

ACR_LOGIN_SERVER=$(az acr show --name "$ACR_NAME" --resource-group "$RESOURCE_GROUP" --query "loginServer" -o tsv)
ACR_PASSWORD=$(az acr credential show --name "$ACR_NAME" --query "passwords[0].value" -o tsv)
IMAGE_TAG="${ACR_LOGIN_SERVER}/diet-dost-web:latest"

# 4. Build and Push with Podman
echo "Building container image with Podman: $IMAGE_TAG"
podman build -t "$IMAGE_TAG" -f Containerfile .

echo "Logging in to ACR with Podman..."
echo "$ACR_PASSWORD" | podman login "$ACR_LOGIN_SERVER" -u "$ACR_NAME" --password-stdin

echo "Pushing image to ACR..."
podman push "$IMAGE_TAG"

# 5. Deploy Bicep
echo "Deploying infrastructure via Bicep..."
DEPLOY_JSON=$(az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --template-file infra/main.bicep \
    --parameters \
        appName="app-dietdost-web" \
        containerImage="$IMAGE_TAG" \
        customDomain="$CUSTOM_DOMAIN" \
        allowDemoUsers="$ALLOW_DEMO_USERS" \
    --output json)

APP_FQDN=$(echo "$DEPLOY_JSON" | grep -o '"appFqdn": *{[^}]*"value": *"[^"]*"' | sed 's/.*"value": *"//;s/"//')
VERIFICATION_ID=$(echo "$DEPLOY_JSON" | grep -o '"customDomainVerificationId": *{[^}]*"value": *"[^"]*"' | sed 's/.*"value": *"//;s/"//')

echo "=================================================================="
echo "DEPLOYMENT COMPLETED!"
echo "Target FQDN: https://$APP_FQDN"
echo ""
echo "DNS Verification Records for $CUSTOM_DOMAIN:"
echo "  CNAME: dev -> $APP_FQDN"
echo "  TXT:   asuid.dev -> $VERIFICATION_ID"
echo "=================================================================="
