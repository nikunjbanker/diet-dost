using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Domain.Model.Meal;

namespace Nutrition.Application.Features.Meals.Queries.GetAiFeedbacks;

public record GetAiFeedbacksQuery(
    string CurrentUserId,
    string? TargetUserId,
    bool IsAdminOrSuper
) : IQuery<Result<List<AiDetectionFeedbackRecord>>>;

public class GetAiFeedbacksQueryHandler : IQueryHandler<GetAiFeedbacksQuery, Result<List<AiDetectionFeedbackRecord>>>
{
    private readonly IRepository<AiDetectionFeedbackRecord> _feedbackRepo;

    public GetAiFeedbacksQueryHandler(IRepository<AiDetectionFeedbackRecord> feedbackRepo)
    {
        _feedbackRepo = feedbackRepo;
    }

    public async Task<Result<List<AiDetectionFeedbackRecord>>> HandleAsync(GetAiFeedbacksQuery request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
            return Result<List<AiDetectionFeedbackRecord>>.Unauthorized();

        var effectiveUserId = (!string.IsNullOrWhiteSpace(request.TargetUserId) && request.IsAdminOrSuper)
            ? request.TargetUserId
            : request.CurrentUserId;

        var feedbacks = await _feedbackRepo.FindAsync(f => f.UserId == effectiveUserId, ct);
        return Result<List<AiDetectionFeedbackRecord>>.Success(feedbacks.OrderByDescending(f => f.CreatedAtUtc).ToList());
    }
}
