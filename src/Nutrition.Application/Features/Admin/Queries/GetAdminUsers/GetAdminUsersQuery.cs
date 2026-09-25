using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Admin.DTOs;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Features.Admin.Queries.GetAdminUsers;

public record GetAdminUsersQuery(
    string? Search,
    UserTier? Tier,
    UserRole? Role
) : IQuery<Result<List<AdminUserSummaryDto>>>;

public class GetAdminUsersQueryHandler : IQueryHandler<GetAdminUsersQuery, Result<List<AdminUserSummaryDto>>>
{
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IRepository<AiUsageLog> _aiLogRepo;

    public GetAdminUsersQueryHandler(
        IRepository<ApplicationUser> userRepo,
        IRepository<AiUsageLog> aiLogRepo)
    {
        _userRepo = userRepo;
        _aiLogRepo = aiLogRepo;
    }

    public async Task<Result<List<AdminUserSummaryDto>>> HandleAsync(GetAdminUsersQuery request, CancellationToken ct = default)
    {
        var users = await _userRepo.GetAllAsync(ct);
        IEnumerable<ApplicationUser> filtered = users;

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchLower = request.Search.Trim().ToLowerInvariant();
            filtered = filtered.Where(u => u.Email.ToLower().Contains(searchLower) ||
                                           u.MobileNumber.Contains(searchLower));
        }

        if (request.Tier.HasValue)
        {
            filtered = filtered.Where(u => u.Tier == request.Tier.Value);
        }

        if (request.Role.HasValue)
        {
            filtered = filtered.Where(u => u.Role == request.Role.Value);
        }

        var orderedUsers = filtered.OrderByDescending(u => u.CreatedAtUtc).ToList();

        var todayUtc = DateTime.UtcNow.Date;
        var todayAiLogs = await _aiLogRepo.FindAsync(l => l.TimestampUtc >= todayUtc && l.IsSuccess, ct);
        var todayCounts = todayAiLogs
            .GroupBy(l => l.UserId)
            .ToDictionary(g => g.Key, g => g.Count());

        var dtos = orderedUsers.Select(u => new AdminUserSummaryDto(
            Id: u.Id,
            Email: u.Email,
            MobileNumber: u.MobileNumber,
            Role: u.Role,
            Tier: u.Tier,
            IsEmailVerified: u.IsEmailVerified,
            IsMobileVerified: u.IsMobileVerified,
            IsActive: u.IsActive,
            TermsAcceptedAtUtc: u.TermsAcceptedAtUtc,
            TermsVersionAccepted: u.TermsVersionAccepted,
            HealthConsentAcceptedAtUtc: u.HealthConsentAcceptedAtUtc,
            HealthConsentVersionAccepted: u.HealthConsentVersionAccepted,
            ConsentIpAddress: u.ConsentIpAddress,
            CreatedAtUtc: u.CreatedAtUtc,
            LastLoginAtUtc: u.LastLoginAtUtc,
            TodayAiDetectionsCount: todayCounts.TryGetValue(u.Id, out var count) ? count : 0
        )).ToList();

        return Result<List<AdminUserSummaryDto>>.Success(dtos);
    }
}
