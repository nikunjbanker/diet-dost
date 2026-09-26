/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Extensions.Logging;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;
using Nutrition.Domain.Model.Progress;

namespace Nutrition.Application.Features.Auth.Commands.DeleteAccount;

public record DeleteAccountCommand(string UserId) : ICommand<Result<string>>;

public class DeleteAccountCommandHandler : ICommandHandler<DeleteAccountCommand, Result<string>>
{
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IRepository<UserProfile> _profileRepo;
    private readonly IRepository<MealLog> _mealRepo;
    private readonly IRepository<DailyCalorieLedger> _ledgerRepo;
    private readonly IRepository<ProgressPhoto> _photoRepo;
    private readonly IRepository<AiDetectionFeedbackRecord> _feedbackRepo;
    private readonly IRepository<UserCorrectionRecord> _correctionRepo;
    private readonly IRepository<VerificationOtp> _otpRepo;
    private readonly IRepository<AiUsageLog> _aiLogRepo;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<DeleteAccountCommandHandler> _logger;

    public DeleteAccountCommandHandler(
        IRepository<ApplicationUser> userRepo,
        IRepository<UserProfile> profileRepo,
        IRepository<MealLog> mealRepo,
        IRepository<DailyCalorieLedger> ledgerRepo,
        IRepository<ProgressPhoto> photoRepo,
        IRepository<AiDetectionFeedbackRecord> feedbackRepo,
        IRepository<UserCorrectionRecord> correctionRepo,
        IRepository<VerificationOtp> otpRepo,
        IRepository<AiUsageLog> aiLogRepo,
        IUnitOfWork uow,
        ILogger<DeleteAccountCommandHandler> logger)
    {
        _userRepo = userRepo;
        _profileRepo = profileRepo;
        _mealRepo = mealRepo;
        _ledgerRepo = ledgerRepo;
        _photoRepo = photoRepo;
        _feedbackRepo = feedbackRepo;
        _correctionRepo = correctionRepo;
        _otpRepo = otpRepo;
        _aiLogRepo = aiLogRepo;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<string>> HandleAsync(DeleteAccountCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return Result<string>.Unauthorized();

        var user = await _userRepo.GetByIdAsync(request.UserId, ct);
        if (user == null)
            return Result<string>.NotFound("User account not found.");

        if (user.Role == UserRole.SuperAdmin)
        {
            return Result<string>.Failure("The primary SuperAdmin account cannot be deleted.", "CannotDeleteSuperAdmin", 400);
        }

        // DPDPA 2023 Right to Erasure: Cascade purge of all user records
        var meals = await _mealRepo.FindAsync(m => m.UserId == request.UserId, ct);
        foreach (var m in meals) await _mealRepo.DeleteAsync(m.Id, ct);

        var ledgers = await _ledgerRepo.FindAsync(l => l.UserId == request.UserId, ct);
        foreach (var l in ledgers) await _ledgerRepo.DeleteAsync(l.Id, ct);

        var photos = await _photoRepo.FindAsync(p => p.UserId == request.UserId, ct);
        foreach (var p in photos) await _photoRepo.DeleteAsync(p.Id, ct);

        var feedbacks = await _feedbackRepo.FindAsync(f => f.UserId == request.UserId, ct);
        foreach (var f in feedbacks) await _feedbackRepo.DeleteAsync(f.Id, ct);

        var corrections = await _correctionRepo.FindAsync(c => c.UserId == request.UserId, ct);
        foreach (var c in corrections) await _correctionRepo.DeleteAsync(c.Id, ct);

        var otps = await _otpRepo.FindAsync(o => o.UserId == request.UserId, ct);
        foreach (var o in otps) await _otpRepo.DeleteAsync(o.Id, ct);

        var aiLogs = await _aiLogRepo.FindAsync(l => l.UserId == request.UserId, ct);
        foreach (var l in aiLogs) await _aiLogRepo.DeleteAsync(l.Id, ct);

        var profile = await _profileRepo.GetByIdAsync(request.UserId, ct);
        if (profile != null) await _profileRepo.DeleteAsync(profile.Id, ct);

        await _userRepo.DeleteAsync(user.Id, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning("[AUTH] User account and all health data purged under DPDPA Right to Erasure: {UserId}", request.UserId);

        return Result<string>.Success("Your account and all associated health records have been permanently deleted per DPDPA guidelines.");
    }
}
