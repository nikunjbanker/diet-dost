// Aspire Bicep template for Diet-Dost Azure Container App
// Uses Aspire Bicep helpers (azurerm.containerapp, azureDefaultCredential)

param containerAppName string = 'diet-dost-app'
param imageTag string = 'latest'
param location string = resourceGroup().location
param containerRegistry string = 'dietdost.azurecr.io'

resource logAnalytics 'Microsoft.Insights/components@2020-02-02' = {
  name: '${containerAppName}-log'
  location: location
  kind: 'web'
  sku: { name: 'PerGB2018' }
}

resource containerApp 'Microsoft.Web/containerApps@2022-03-01' = {
  name: containerAppName
  location: location
  properties: {
    kubeEnvironmentId: azurerm.kubeEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 80
        traffic: [{ latestRevision: true weight: 100 }]
      }
    }
    template: {
      containers: [
        {
          name: 'webgateway'
          image: '${containerRegistry}/diet-dost:${imageTag}'
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT' value: 'Production' }
          ]
        }
      ]
    }
  }
}
