using DistributedLeasing.Abstractions;
using DistributedLeasing.Core;
using DistributedLeasing.Core.Exceptions;

namespace DistributedLeasing.ChaosEngineering;

/// <summary>
/// Chaos engineering wrapper for <see cref="ILeaseProvider"/> that injects controlled failures.
/// </summary>
/// <remarks>
/// <para>
/// This provider wraps an existing lease provider and injects faults based on configured chaos policies.
/// Use this in testing/staging environments to validate resilience and failure handling.
/// </para>
/// <para>
/// <strong>IMPORTANT:</strong> This is for testing only. Never use in production.
/// </para>
/// <para>
/// <strong>Usage Example:</strong>
/// </para>
/// <code>
/// var realProvider = new BlobLeaseProvider(blobOptions);
/// var chaosProvider = new ChaosLeaseProvider(realProvider, new ChaosPolicy
/// {
///     FailureRate = 0.1, // 10% failure rate
///     MinDelay = TimeSpan.FromMilliseconds(100),
///     MaxDelay = TimeSpan.FromSeconds(2),
///     FaultTypes = ChaosFaultType.Timeout | ChaosFaultType.Exception
/// });
/// </code>
/// </remarks>
public class ChaosLeaseProvider : ILeaseProvider
{
    private readonly ILeaseProvider _inner;
    private readonly ChaosPolicy _policy;
    private readonly Random _random = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ChaosLeaseProvider"/> class.
    /// </summary>
    /// <param name="innerProvider">The actual lease provider to wrap.</param>
    /// <param name="policy">Chaos injection policy.</param>
    public ChaosLeaseProvider(ILeaseProvider innerProvider, ChaosPolicy policy)
    {
        _inner = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    /// <inheritdoc/>
    public async Task<ILease?> AcquireLeaseAsync(
        string leaseName,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        await MaybeInjectDelayAsync(cancellationToken).ConfigureAwait(false);
        MaybeInjectException("AcquireLeaseAsync");

        return await _inner.AcquireLeaseAsync(leaseName, duration, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task BreakLeaseAsync(string leaseName, CancellationToken cancellationToken = default)
    {
        await MaybeInjectDelayAsync(cancellationToken).ConfigureAwait(false);
        MaybeInjectException("BreakLeaseAsync");

        await _inner.BreakLeaseAsync(leaseName, cancellationToken).ConfigureAwait(false);
    }

    private async Task MaybeInjectDelayAsync(CancellationToken cancellationToken)
    {
        if (!_policy.FaultTypes.HasFlag(ChaosFaultType.Delay))
            return;

        if (_random.NextDouble() < _policy.FailureRate)
        {
            var delay = TimeSpan.FromMilliseconds(
                _random.Next(
                    (int)_policy.MinDelay.TotalMilliseconds,
                    (int)_policy.MaxDelay.TotalMilliseconds));

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private void MaybeInjectException(string operation)
    {
        if (!_policy.FaultTypes.HasFlag(ChaosFaultType.Exception))
            return;

        if (_random.NextDouble() < _policy.FailureRate)
        {
            throw new ProviderUnavailableException(
                $"Chaos fault injection in {operation}")
            {
                ProviderName = "ChaosLeaseProvider"
            };
        }
    }
}

/// <summary>
/// Chaos injection policy configuration.
/// </summary>
public class ChaosPolicy
{
    /// <summary>
    /// Probability of fault injection (0.0 to 1.0).
    /// </summary>
    /// <remarks>
    /// Examples:
    /// - 0.1 = 10% failure rate
    /// - 0.5 = 50% failure rate
    /// </remarks>
    public double FailureRate { get; set; } = 0.1;

    /// <summary>
    /// Minimum delay to inject.
    /// </summary>
    public TimeSpan MinDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Maximum delay to inject.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Types of faults to inject.
    /// </summary>
    public ChaosFaultType FaultTypes { get; set; } = ChaosFaultType.All;
}

/// <summary>
/// Types of chaos faults that can be injected.
/// </summary>
[Flags]
public enum ChaosFaultType
{
    /// <summary>No faults.</summary>
    None = 0,

    /// <summary>Inject delays/latency.</summary>
    Delay = 1,

    /// <summary>Throw exceptions.</summary>
    Exception = 2,

    /// <summary>Simulate timeout (delay + cancellation).</summary>
    Timeout = 4,

    /// <summary>All fault types.</summary>
    All = Delay | Exception | Timeout
}