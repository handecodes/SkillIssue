using System.Threading.RateLimiting;

namespace SkillIssue.Web.Security;

public interface IForkSubmissionLimiter
{
    bool TryAcquire(int userId);
}

// Singleton — shared across all Blazor circuits so limits are per-user, not per-circuit.
// Uses PartitionedRateLimiter (same BCL type as AddRateLimiter middleware) partitioned
// by authenticated user ID: 3 permits per 30-second sliding window.
public sealed class ForkSubmissionLimiter : IForkSubmissionLimiter, IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter =
        PartitionedRateLimiter.Create<string, string>(userId =>
            RateLimitPartition.GetSlidingWindowLimiter(userId, _ =>
                new SlidingWindowRateLimiterOptions
                {
                    PermitLimit          = 3,
                    Window               = TimeSpan.FromSeconds(30),
                    SegmentsPerWindow    = 3,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit           = 0
                }));

    public bool TryAcquire(int userId)
    {
        using var lease = _limiter.AttemptAcquire(userId.ToString());
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}
