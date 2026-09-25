namespace Nutrition.Application.Common.Interfaces;

/// <summary>
/// Port abstraction for retrieving and managing application secrets and credentials,
/// decoupling business use cases and infrastructure from specific secret storage implementations.
/// </summary>
public interface ISecretStore
{
    Task<string?> GetSecretAsync(string key, CancellationToken ct = default);
    Task<string> GetRequiredSecretAsync(string key, CancellationToken ct = default);
    Task SetSecretAsync(string key, string value, string? description = null, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, string>> GetAllSecretsAsync(CancellationToken ct = default);
}

public static class SecretKeys
{
    public const string JwtSigningKey = "Jwt:Key";
    public const string DemoPassword = "Auth:DemoPassword";
    public const string GoogleAiApiKey = "AI:GoogleAI:ApiKey";
    public const string AzureOpenAiApiKey = "AI:AzureOpenAI:ApiKey";
}
