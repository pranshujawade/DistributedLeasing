using System.Diagnostics;

namespace DistributedLeasing.Tests.Shared;

/// <summary>
/// Utility methods for common test operations.
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Polls a condition with timeout and interval, returning true if condition becomes true within timeout.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="timeout">Maximum time to wait for condition.</param>
    /// <param name="pollInterval">Interval between condition checks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if condition became true within timeout, false otherwise.</returns>
    public static async Task<bool> WaitForConditionAsync(
        Func<bool> condition,
        TimeSpan timeout,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(100);
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            if (condition())
            {
                return true;
            }

            try
            {
                await Task.Delay(interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        return condition(); // Final check
    }

    /// <summary>
    /// Polls an async condition with timeout and interval.
    /// </summary>
    /// <param name="condition">The async condition to evaluate.</param>
    /// <param name="timeout">Maximum time to wait for condition.</param>
    /// <param name="pollInterval">Interval between condition checks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if condition became true within timeout, false otherwise.</returns>
    public static async Task<bool> WaitForConditionAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(100);
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            if (await condition())
            {
                return true;
            }

            try
            {
                await Task.Delay(interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        return await condition(); // Final check
    }

    /// <summary>
    /// Creates a cancellation token that cancels after the specified duration.
    /// </summary>
    /// <param name="timeout">The timeout duration.</param>
    /// <returns>A cancellation token source that will cancel after timeout.</returns>
    public static CancellationTokenSource CreateCancellationTokenWithTimeout(TimeSpan timeout)
    {
        var cts = new CancellationTokenSource();
        cts.CancelAfter(timeout);
        return cts;
    }

    /// <summary>
    /// Generates a unique lease ID for test isolation.
    /// </summary>
    /// <param name="prefix">Optional prefix for the lease ID.</param>
    /// <returns>A unique lease identifier.</returns>
    public static string GenerateUniqueLeaseId(string prefix = "lease")
    {
        return $"{prefix}-{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Generates a unique lease name for test isolation.
    /// </summary>
    /// <param name="testName">The test name or identifier.</param>
    /// <returns>A unique lease name.</returns>
    public static string GenerateUniqueLeaseName(string testName)
    {
        return $"{testName}-{Guid.NewGuid():N}".ToLowerInvariant();
    }

    /// <summary>
    /// Simulates a delay in an async-friendly way that can be controlled by tests.
    /// </summary>
    /// <param name="duration">The duration to delay.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task SimulateDelayAsync(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        return Task.Delay(duration, cancellationToken);
    }

    /// <summary>
    /// Waits for an event to be raised within a timeout period.
    /// </summary>
    /// <typeparam name="TEventArgs">The type of event arguments.</typeparam>
    /// <param name="subscribe">Action to subscribe to the event.</param>
    /// <param name="unsubscribe">Action to unsubscribe from the event.</param>
    /// <param name="trigger">Action that triggers the event.</param>
    /// <param name="timeout">Maximum time to wait for the event.</param>
    /// <returns>The event arguments if event was raised, null otherwise.</returns>
    public static async Task<TEventArgs?> WaitForEventAsync<TEventArgs>(
        Action<EventHandler<TEventArgs>> subscribe,
        Action<EventHandler<TEventArgs>> unsubscribe,
        Action trigger,
        TimeSpan? timeout = null)
        where TEventArgs : EventArgs
    {
        var timeoutDuration = timeout ?? TestConstants.Timeouts.Standard;
        var tcs = new TaskCompletionSource<TEventArgs>();
        var cts = new CancellationTokenSource(timeoutDuration);

        void Handler(object? sender, TEventArgs args)
        {
            tcs.TrySetResult(args);
        }

        EventHandler<TEventArgs> eventHandler = Handler;

        try
        {
            subscribe(eventHandler);
            
            // Register cancellation
            using (cts.Token.Register(() => tcs.TrySetCanceled()))
            {
                trigger();
                return await tcs.Task;
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            unsubscribe(eventHandler);
        }
    }

    /// <summary>
    /// Executes an action multiple times in parallel and returns all results.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="action">The action to execute.</param>
    /// <param name="count">Number of parallel executions.</param>
    /// <returns>Array of results.</returns>
    public static async Task<T[]> ExecuteInParallelAsync<T>(Func<Task<T>> action, int count)
    {
        var tasks = new Task<T>[count];
        for (int i = 0; i < count; i++)
        {
            tasks[i] = action();
        }

        return await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Executes an action multiple times in parallel.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="count">Number of parallel executions.</param>
    public static async Task ExecuteInParallelAsync(Func<Task> action, int count)
    {
        var tasks = new Task[count];
        for (int i = 0; i < count; i++)
        {
            tasks[i] = action();
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Retries an action until it succeeds or max attempts is reached.
    /// </summary>
    /// <param name="action">The action to retry.</param>
    /// <param name="maxAttempts">Maximum number of attempts.</param>
    /// <param name="retryInterval">Interval between retries.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if action succeeded, false if max attempts reached.</returns>
    public static async Task<bool> RetryAsync(
        Func<Task<bool>> action,
        int maxAttempts,
        TimeSpan retryInterval,
        CancellationToken cancellationToken = default)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (await action())
            {
                return true;
            }

            if (attempt < maxAttempts)
            {
                await Task.Delay(retryInterval, cancellationToken);
            }
        }

        return false;
    }
}
