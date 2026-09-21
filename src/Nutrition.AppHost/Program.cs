var builder = DistributedApplication.CreateBuilder(args);

var geminiKey = builder.Configuration["AI:ApiKey"] 
    ?? builder.Configuration["Gemini:ApiKey"]
    ?? Environment.GetEnvironmentVariable("AI__ApiKey")
    ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

// Web Gateway hosting the Linear.app PWA and microservice endpoints
builder.AddProject<Projects.Nutrition_WebGateway>("web-gateway")
       .WithHttpEndpoint(port: 5240, isProxied: false)
       .WithExternalHttpEndpoints()
       .WithEnvironment("Database__Provider", "Sqlite")
       .WithEnvironment("ConnectionStrings__DefaultConnection", "Data Source=diettracker.db")
       .WithEnvironment("AI__ApiKey", geminiKey)
       .WithEnvironment("AI__ModelId", "gemini-3-flash-preview")
       .WithEnvironment("AI__FallbackModelId", "gemini-3.6-flash");

builder.Build().Run();
