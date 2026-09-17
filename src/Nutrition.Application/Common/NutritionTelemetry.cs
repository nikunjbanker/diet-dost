using System.Diagnostics;

namespace Nutrition.Application.Common;

/// <summary>
/// Central OpenTelemetry activity source and semantic attribute definitions for Diet Dost.
/// Emits spans and attributes for AI meal detection, ICMR-NIN calculations, and HTTP payloads.
/// </summary>
public static class NutritionTelemetry
{
    public const string ServiceName = "Nutrition.DietDost";
    public const string ServiceVersion = "1.0.0";

    /// <summary>
    /// The application-wide ActivitySource registered with OpenTelemetry tracing in Program.cs.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);

    // Activity / Span Names
    public const string SpanAiFoodDetection = "ai.food_detection";
    public const string SpanAiVisionAnalysis = "ai.food_vision_analysis";
    public const string SpanAiTextAnalysis = "ai.food_description_analysis";

    // Semantic Attribute Keys (Conforming to OpenTelemetry GenAI & Custom Domain Conventions)
    public const string TagGenAiSystem = "gen_ai.system";
    public const string TagGenAiOperation = "gen_ai.operation.name";
    public const string TagGenAiRequestModel = "gen_ai.request.model";
    public const string TagGenAiResponseModel = "gen_ai.response.model";
    public const string TagGenAiSystemPrompt = "gen_ai.system_prompt";
    public const string TagGenAiUserPrompt = "gen_ai.user_prompt";
    public const string TagGenAiTemperature = "gen_ai.request.temperature";
    public const string TagGenAiMaxTokens = "gen_ai.request.max_tokens";

    // User & Clinical Context Keys
    public const string TagUserId = "user.id";
    public const string TagUserName = "user.name";
    public const string TagUserConditions = "user.diagnosed_conditions";
    public const string TagUserMedications = "user.medications";
    public const string TagUserDietaryPreference = "user.dietary_preference";
    public const string TagUserRegionalCuisine = "user.regional_cuisine";
    public const string TagUserLearnedCorrectionsCount = "diet.learned_corrections_count";
    public const string TagUserLearnedCorrectionsSummary = "diet.learned_corrections_summary";

    // Detection Output Keys
    public const string TagResponseDishName = "gen_ai.response.dish_name";
    public const string TagResponseMealType = "gen_ai.response.meal_type";
    public const string TagResponseTotalCalories = "gen_ai.response.total_calories";
    public const string TagResponseTotalProtein = "gen_ai.response.total_protein_g";
    public const string TagResponseTotalCarbs = "gen_ai.response.total_carbs_g";
    public const string TagResponseTotalFat = "gen_ai.response.total_fat_g";
    public const string TagResponseConfidence = "gen_ai.response.confidence_score";
    public const string TagResponseConfidenceGated = "gen_ai.response.confidence_gated_passed";
    public const string TagResponseItemsCount = "gen_ai.response.items_count";
    public const string TagResponseItemsSummary = "gen_ai.response.items_summary";
    public const string TagResponseDietitianAdvice = "gen_ai.response.dietitian_advice";

    // HTTP Payload Keys
    public const string TagHttpRequestPayload = "http.request.body";
    public const string TagHttpResponsePayload = "http.response.body";
    public const string TagHttpResponseStatusCode = "http.response.status_code";
}
