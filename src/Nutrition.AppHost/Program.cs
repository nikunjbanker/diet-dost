var builder = DistributedApplication.CreateBuilder(args);

var geminiKey = builder.Configuration["AI:GoogleAI:ApiKey"]
    ?? builder.Configuration["AI:ApiKey"] 
    ?? builder.Configuration["Gemini:ApiKey"]
    ?? Environment.GetEnvironmentVariable("AI__GoogleAI__ApiKey")
    ?? Environment.GetEnvironmentVariable("AI__ApiKey")
    ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

var aiProvider = builder.Configuration["AI:Provider"] 
    ?? Environment.GetEnvironmentVariable("AI__Provider") 
    ?? "GoogleAI";

var azureKey = builder.Configuration["AI:AzureOpenAI:ApiKey"]
    ?? Environment.GetEnvironmentVariable("AI__AzureOpenAI__ApiKey");
var azureEndpoint = builder.Configuration["AI:AzureOpenAI:Endpoint"]
    ?? Environment.GetEnvironmentVariable("AI__AzureOpenAI__Endpoint");
var azureDeployment = builder.Configuration["AI:AzureOpenAI:DeploymentName"]
    ?? Environment.GetEnvironmentVariable("AI__AzureOpenAI__DeploymentName")
    ?? "gpt-5.6-luna";

// Web Gateway hosting the Linear.app PWA and microservice endpoints
var webGateway = builder.AddProject<Projects.Nutrition_WebGateway>("web-gateway")
       .WithHttpEndpoint(port: 5240, isProxied: false)
       .WithExternalHttpEndpoints()
       .WithEnvironment("Database__Provider", "Sqlite")
       .WithEnvironment("ConnectionStrings__DefaultConnection", "Data Source=diettracker.db")
       .WithEnvironment("AI__Provider", aiProvider)
       .WithEnvironment("AI__GoogleAI__ApiKey", geminiKey ?? "")
       .WithEnvironment("AI__ApiKey", geminiKey ?? "")
       .WithEnvironment("AI__GoogleAI__ModelId", builder.Configuration["AI:GoogleAI:ModelId"] ?? builder.Configuration["AI:ModelId"] ?? "gemini-3-flash-preview")
       .WithEnvironment("AI__GoogleAI__FallbackModelId", builder.Configuration["AI:GoogleAI:FallbackModelId"] ?? builder.Configuration["AI:FallbackModelId"] ?? "gemini-3.6-flash");

if (!string.IsNullOrWhiteSpace(azureKey))
{
    webGateway.WithEnvironment("AI__AzureOpenAI__ApiKey", azureKey);
}
if (!string.IsNullOrWhiteSpace(azureEndpoint))
{
    webGateway.WithEnvironment("AI__AzureOpenAI__Endpoint", azureEndpoint);
}
if (!string.IsNullOrWhiteSpace(azureDeployment))
{
    webGateway.WithEnvironment("AI__AzureOpenAI__DeploymentName", azureDeployment);
}

builder.Build().Run();
