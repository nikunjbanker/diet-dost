using System.Threading.RateLimiting;
using Polly;
using Polly.RateLimiting;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class PollyRateLimitingTests
{
    [Fact]
    public async Task PollyRateLimiter_AllowsRequestsWithinLimit()
    {
        var pipeline = new ResiliencePipelineBuilder()
            .AddRateLimiter(new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 1,
                QueueLimit = 0
            }))
            .Build();

        int executedCount = 0;
        for (int i = 0; i < 3; i++)
        {
            await pipeline.ExecuteAsync(async _ =>
            {
                executedCount++;
                await Task.Yield();
            });
        }

        Assert.Equal(3, executedCount);
    }

    [Fact]
    public async Task PollyRateLimiter_ExceedingLimit_ThrowsRateLimiterRejectedException()
    {
        var pipeline = new ResiliencePipelineBuilder()
            .AddRateLimiter(new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 2,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 1,
                QueueLimit = 0
            }))
            .Build();

        // 2 successful executions
        await pipeline.ExecuteAsync(async _ => await Task.Yield());
        await pipeline.ExecuteAsync(async _ => await Task.Yield());

        // 3rd attempt exceeds permit limit
        await Assert.ThrowsAsync<RateLimiterRejectedException>(async () =>
        {
            await pipeline.ExecuteAsync(async _ => await Task.Yield());
        });
    }

    [Fact]
    public async Task PollyRateLimiter_FixedWindow_EnforcesExactPermitCeiling()
    {
        var pipeline = new ResiliencePipelineBuilder()
            .AddRateLimiter(new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }))
            .Build();

        int succeeded = 0;
        int rejected = 0;

        for (int i = 0; i < 7; i++)
        {
            try
            {
                await pipeline.ExecuteAsync(async _ =>
                {
                    succeeded++;
                    await Task.Yield();
                });
            }
            catch (RateLimiterRejectedException)
            {
                rejected++;
            }
        }

        Assert.Equal(5, succeeded);
        Assert.Equal(2, rejected);
    }
}
