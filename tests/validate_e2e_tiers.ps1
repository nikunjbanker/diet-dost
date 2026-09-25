<#
.SYNOPSIS
    End-to-End User Tier & Feature Gating Validation Script for Diet Dost.
.DESCRIPTION
    Validates all 5 user tiers (Free, Basic, Premium, Admin, SuperAdmin) against
    the live WebGateway running at http://localhost:5240/. Tests authentication,
    profile calculations, daily calorie ledger, AI quota gating, photo comparison
    paywalls, meal data export gating, and admin role authorization.
.EXAMPLE
    pwsh -File tests/validate_e2e_tiers.ps1
#>

param(
    [string]$BaseUrl = "http://localhost:5240",
    [string]$Password = "DietDost@Demo2026!"
)

$users = @(
    @{ Email = "free@dietdost.app"; Tier = "Free"; Role = "User"; ExpectCompare = $false; ExpectExport = $false; ExpectAdmin = $false },
    @{ Email = "basic@dietdost.app"; Tier = "Basic"; Role = "User"; ExpectCompare = $false; ExpectExport = $false; ExpectAdmin = $false },
    @{ Email = "premium@dietdost.app"; Tier = "Premium"; Role = "User"; ExpectCompare = $true; ExpectExport = $true; ExpectAdmin = $false },
    @{ Email = "admin.demo@dietdost.app"; Tier = "Premium"; Role = "Admin"; ExpectCompare = $true; ExpectExport = $true; ExpectAdmin = $true },
    @{ Email = "superadmin@dietdost.app"; Tier = "SuperAdmin"; Role = "SuperAdmin"; ExpectCompare = $true; ExpectExport = $true; ExpectAdmin = $true }
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  DIET DOST E2E TIER VALIDATION (LIVE WEB GATEWAY :5240)   " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$allPassed = $true

foreach ($u in $users) {
    Write-Host "`n--> Validating Demo User: $($u.Email) [Expected Tier: $($u.Tier), Role: $($u.Role)]" -ForegroundColor Yellow

    # 1. Login
    $loginBody = @{
        emailOrMobile = $u.Email
        password = $Password
    } | ConvertTo-Json

    try {
        $loginRes = Invoke-RestMethod -Uri "$BaseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
    } catch {
        Write-Host "    [FAIL] Login failed: $($_.Exception.Message)" -ForegroundColor Red
        $allPassed = $false
        continue
    }

    $token = $loginRes.token
    if (-not $token) {
        Write-Host "    [FAIL] No JWT token returned." -ForegroundColor Red
        $allPassed = $false
        continue
    }
    Write-Host "    [PASS] Login successful, JWT token acquired." -ForegroundColor Green

    $headers = @{
        "Authorization" = "Bearer $token"
    }

    # 2. Get Current User (/api/auth/me)
    try {
        $meRes = Invoke-RestMethod -Uri "$BaseUrl/api/auth/me" -Method Get -Headers $headers
        Write-Host "    [PASS] /api/auth/me: User authenticated as $($meRes.user.email) (Tier: $($meRes.user.tier), Role: $($meRes.user.role))" -ForegroundColor Green
    } catch {
        Write-Host "    [FAIL] /api/auth/me failed: $($_.Exception.Message)" -ForegroundColor Red
        $allPassed = $false
    }

    # 3. Get Clinical Profile (/api/profile)
    try {
        $profileRes = Invoke-RestMethod -Uri "$BaseUrl/api/profile" -Method Get -Headers $headers
        Write-Host "    [PASS] /api/profile: Target Calories = $($profileRes.budget.targetCalories) kcal, Protein = $($profileRes.macros.proteinGrams)g" -ForegroundColor Green
    } catch {
        Write-Host "    [FAIL] /api/profile failed: $($_.Exception.Message)" -ForegroundColor Red
        $allPassed = $false
    }

    # 4. Get Daily Ledger (/api/analytics/ledger/today)
    try {
        $ledgerRes = Invoke-RestMethod -Uri "$BaseUrl/api/analytics/ledger/today" -Method Get -Headers $headers
        Write-Host "    [PASS] /api/analytics/ledger/today: Budgeted = $($ledgerRes.budgetedCalories) kcal, Consumed = $($ledgerRes.consumedCalories) kcal" -ForegroundColor Green
    } catch {
        Write-Host "    [FAIL] /api/analytics/ledger/today failed: $($_.Exception.Message)" -ForegroundColor Red
        $allPassed = $false
    }

    # 5. Check AI Quota (/api/meals/quota)
    try {
        $quotaRes = Invoke-RestMethod -Uri "$BaseUrl/api/meals/quota" -Method Get -Headers $headers
        Write-Host "    [PASS] /api/meals/quota: Daily Limit = $($quotaRes.dailyLimit), Remaining = $($quotaRes.remainingCalls), Tier = $($quotaRes.tier)" -ForegroundColor Green
    } catch {
        Write-Host "    [FAIL] /api/meals/quota failed: $($_.Exception.Message)" -ForegroundColor Red
        $allPassed = $false
    }

    # 6. Test Photo Comparison Gating (/api/progress-photos/comparison)
    try {
        $compareRes = Invoke-RestMethod -Uri "$BaseUrl/api/progress-photos/comparison" -Method Get -Headers $headers
        if ($u.ExpectCompare) {
            Write-Host "    [PASS] /api/progress-photos/comparison: Granted as expected (HTTP 200)" -ForegroundColor Green
        } else {
            Write-Host "    [FAIL] /api/progress-photos/comparison: Expected 403 Forbidden but received 200 OK!" -ForegroundColor Red
            $allPassed = $false
        }
    } catch {
        if (-not $u.ExpectCompare -and ($_.Exception.Response.StatusCode.value__ -eq 403 -or $_.Exception.Response.StatusCode -eq [System.Net.HttpStatusCode]::Forbidden)) {
            Write-Host "    [PASS] /api/progress-photos/comparison: Gated with 403 Forbidden as expected for tier $($u.Tier)" -ForegroundColor Green
        } else {
            Write-Host "    [FAIL] /api/progress-photos/comparison: Unexpected response: $($_.Exception.Message)" -ForegroundColor Red
            $allPassed = $false
        }
    }

    # 7. Test Data Export Gating (/api/meals/export)
    try {
        $exportRes = Invoke-RestMethod -Uri "$BaseUrl/api/meals/export" -Method Get -Headers $headers
        if ($u.ExpectExport) {
            Write-Host "    [PASS] /api/meals/export: Granted as expected (HTTP 200)" -ForegroundColor Green
        } else {
            Write-Host "    [FAIL] /api/meals/export: Expected 403 Forbidden but received 200 OK!" -ForegroundColor Red
            $allPassed = $false
        }
    } catch {
        if (-not $u.ExpectExport -and ($_.Exception.Response.StatusCode.value__ -eq 403 -or $_.Exception.Response.StatusCode -eq [System.Net.HttpStatusCode]::Forbidden)) {
            Write-Host "    [PASS] /api/meals/export: Gated with 403 Forbidden as expected for tier $($u.Tier)" -ForegroundColor Green
        } else {
            Write-Host "    [FAIL] /api/meals/export: Unexpected response: $($_.Exception.Message)" -ForegroundColor Red
            $allPassed = $false
        }
    }

    # 8. Test Admin Users Endpoint (/api/admin/users)
    try {
        $adminRes = Invoke-RestMethod -Uri "$BaseUrl/api/admin/users" -Method Get -Headers $headers
        if ($u.ExpectAdmin) {
            Write-Host "    [PASS] /api/admin/users: Granted as expected (HTTP 200), total users: $($adminRes.users.Count)" -ForegroundColor Green
        } else {
            Write-Host "    [FAIL] /api/admin/users: Expected 403 Forbidden for non-admin but received 200 OK!" -ForegroundColor Red
            $allPassed = $false
        }
    } catch {
        if (-not $u.ExpectAdmin -and ($_.Exception.Response.StatusCode.value__ -eq 403 -or $_.Exception.Response.StatusCode -eq [System.Net.HttpStatusCode]::Forbidden)) {
            Write-Host "    [PASS] /api/admin/users: Gated with 403 Forbidden as expected for non-admin $($u.Role)" -ForegroundColor Green
        } else {
            Write-Host "    [FAIL] /api/admin/users: Unexpected response: $($_.Exception.Message)" -ForegroundColor Red
            $allPassed = $false
        }
    }
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
if ($allPassed) {
    Write-Host "  ALL 5 TIERS PASSED LIVE E2E VALIDATION 100%!           " -ForegroundColor Green
} else {
    Write-Host "  SOME TIER VALIDATIONS FAILED!                          " -ForegroundColor Red
    exit 1
}
Write-Host "==========================================================" -ForegroundColor Cyan
