using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Ledger;

namespace Nutrition.Application.Features.Analytics.Queries.GetDailyLedger;

public record GetDailyLedgerQuery(
    string CurrentUserId,
    string? TargetUserId,
    bool IsAdminOrSuper,
    string? Date
) : IQuery<Result<DailyCalorieLedger>>;

public class GetDailyLedgerQueryHandler : IQueryHandler<GetDailyLedgerQuery, Result<DailyCalorieLedger>>
{
    private readonly ClinicalDietitianService _dietitianService;

    public GetDailyLedgerQueryHandler(ClinicalDietitianService dietitianService)
    {
        _dietitianService = dietitianService;
    }

    public async Task<Result<DailyCalorieLedger>> HandleAsync(GetDailyLedgerQuery request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
            return Result<DailyCalorieLedger>.Unauthorized();

        var targetUserId = string.IsNullOrWhiteSpace(request.TargetUserId) ? request.CurrentUserId : request.TargetUserId;
        if (targetUserId != request.CurrentUserId && !request.IsAdminOrSuper)
        {
            return Result<DailyCalorieLedger>.Forbidden("Access denied to inspect another user's ledger.");
        }

        var dateOnly = !string.IsNullOrWhiteSpace(request.Date) && DateOnly.TryParse(request.Date, out var parsed)
            ? parsed
            : DateOnly.FromDateTime(DateTime.UtcNow);

        var ledger = await _dietitianService.GetOrCreateDailyLedgerAsync(targetUserId, dateOnly, ct);
        return Result<DailyCalorieLedger>.Success(ledger);
    }
}
