namespace DistributedLeasing.Tests.Shared.Fixtures;

/// <summary>
/// Controllable time provider for deterministic time-dependent testing.
/// </summary>
public class TimeProviderFixture
{
    private DateTimeOffset _currentTime;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeProviderFixture"/> class.
    /// </summary>
    /// <param name="initialTime">The initial time, or null to use current UTC time.</param>
    public TimeProviderFixture(DateTimeOffset? initialTime = null)
    {
        _currentTime = initialTime ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets the current virtual time.
    /// </summary>
    public DateTimeOffset UtcNow
    {
        get
        {
            lock (_lock)
            {
                return _currentTime;
            }
        }
    }

    /// <summary>
    /// Advances time by the specified duration.
    /// </summary>
    /// <param name="duration">The duration to advance.</param>
    public void Advance(TimeSpan duration)
    {
        lock (_lock)
        {
            _currentTime = _currentTime.Add(duration);
        }
    }

    /// <summary>
    /// Sets the current time to a specific value.
    /// </summary>
    /// <param name="time">The time to set.</param>
    public void SetTime(DateTimeOffset time)
    {
        lock (_lock)
        {
            _currentTime = time;
        }
    }

    /// <summary>
    /// Resets the time to the current actual UTC time.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _currentTime = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Resets the time to a specific value.
    /// </summary>
    /// <param name="time">The time to reset to.</param>
    public void Reset(DateTimeOffset time)
    {
        lock (_lock)
        {
            _currentTime = time;
        }
    }
}
