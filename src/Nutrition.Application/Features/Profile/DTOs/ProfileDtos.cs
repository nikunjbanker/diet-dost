using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Profile;

namespace Nutrition.Application.Features.Profile.DTOs;

public record ProfileResponseDto(
    UserProfile Profile,
    BmrTdeeResult Budget,
    MacroDistribution Macros,
    string? Message = null
);
