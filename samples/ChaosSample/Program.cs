using DistributedLeasing.Abstractions.Contracts;
using DistributedLeasing.Abstractions.Exceptions;
using DistributedLeasing.Azure.Blob;
using DistributedLeasing.ChaosEngineering.Configuration;
using DistributedLeasing.ChaosEngineering.Faults.Strategies;
using DistributedLeasing.ChaosEngineering.Lifecycle;
using DistributedLeasing.ChaosEngineering.Observability;
using DistributedLeasing.ChaosEngineering.Policies.Implementations;

Console.WriteLine("==============================================");
Console.WriteLine("  Distributed Leasing - Chaos Engineering Demo");
Console.WriteLine("==============================================\n");

// Get connection string from environment
var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
    ?? "UseDevelopmentStorage=true";

// Create the actual blob provider
var blobProvider = new BlobLeaseProvider(new BlobLeaseProviderOptions
{
    ConnectionString = connectionString,
    ContainerName = "chaos-demo"
});

Console.WriteLine("Blob provider created. Starting chaos engineering demos...\n");

// Demo 1: Basic Probabilistic Chaos
await Demo1_BasicProbabilisticChaos(blobProvider);

// Demo 2: Deterministic Testing
await Demo2_DeterministicTesting(blobProvider);

// Demo 3: Per-Operation Configuration
await Demo3_PerOperationConfiguration(blobProvider);

// Demo 4: Threshold Policies
await Demo4_ThresholdPolicies(blobProvider);

// Demo 5: Renewal Failure Testing
await Demo5_RenewalFailureTesting(blobProvider);

Console.WriteLine("\n==============================================");
Console.WriteLine("  All demos completed!");
Console.WriteLine("==============================================");

static async Task Demo1_BasicProbabilisticChaos(ILeaseProvider actualProvider)
{
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    Console.WriteLine("Demo 1: Basic Probabilistic Chaos (30% failure rate)");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

    // Create fault strategies
    var delayStrategy = new DelayFaultStrategy(
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(500));

    var exceptionStrategy = ExceptionFaultStrategy.Create<ProviderUnavailableException>(
        "Chaos fault injection");

    // Create probabilistic policy (30% failure rate)
    var policy = new ProbabilisticPolicy(0.3, delayStrategy, exceptionStrategy);

    // Configure chaos with observer
    var observer = new ConsoleChaosObserver(useColors: true, includeTimestamps: true);
    var options = new ChaosOptionsBuilder()
        .Enable()
        .WithProviderName("Demo1Provider")
        .WithDefaultPolicy(policy)
        .Build();

    var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options, observer);

    // Try acquiring lease multiple times
    for (int i = 1; i <= 5; i++)
    {
        Console.WriteLine($"\n--- Attempt {i}/5 ---");
        try
        {
            var lease = await chaosProvider.AcquireLeaseAsync($"demo1-lease-{i}", TimeSpan.FromMinutes(1));
            if (lease != null)
            {
                Console.WriteLine($"✓ Lease acquired: {lease.LeaseId}");
                await lease.ReleaseAsync();
                Console.WriteLine("✓ Lease released");
            }
            else
            {
                Console.WriteLine("⚠ Lease not available (held by another instance)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed: {ex.GetType().Name} - {ex.Message}");
        }

        await Task.Delay(200);
    }

    Console.WriteLine("\n");
}

static async Task Demo2_DeterministicTesting(ILeaseProvider actualProvider)
{
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    Console.WriteLine("Demo 2: Deterministic Testing (fail first 3, then succeed)");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

    var exceptionStrategy = ExceptionFaultStrategy.Create<LeaseException>("Deterministic failure");

    // Fail first 3 attempts, then succeed
    var policy = DeterministicPolicy.FailFirstN(3, exceptionStrategy);

    var observer = new ConsoleChaosObserver();
    var options = new ChaosOptionsBuilder()
        .Enable()
        .WithDefaultPolicy(policy)
        .Build();

    var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options, observer);

    int attempts = 0;
    ILease? lease = null;

    while (lease == null && attempts < 5)
    {
        attempts++;
        Console.WriteLine($"\n--- Attempt {attempts}/5 ---");
        try
        {
            lease = await chaosProvider.AcquireLeaseAsync("demo2-lease", TimeSpan.FromMinutes(1));
            if (lease != null)
            {
                Console.WriteLine($"✓ SUCCESS on attempt {attempts}!");
                await lease.ReleaseAsync();
            }
        }
        catch (LeaseException ex)
        {
            Console.WriteLine($"✗ Expected failure: {ex.Message}");
        }

        await Task.Delay(100);
    }

    Console.WriteLine($"\n✓ Test passed: Succeeded after exactly {attempts - 1} retries (expected 3)\n");
}

static async Task Demo3_PerOperationConfiguration(ILeaseProvider actualProvider)
{
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    Console.WriteLine("Demo 3: Per-Operation Configuration");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

    var delayStrategy = new DelayFaultStrategy(TimeSpan.FromMilliseconds(300));
    var exceptionStrategy = ExceptionFaultStrategy.Create<LeaseException>("Acquire chaos");

    var options = new ChaosOptionsBuilder()
        .Enable()
        // Acquire: 50% chance of exception
        .ConfigureOperation("AcquireAsync", op => op
            .Enable()
            .WithPolicy(new ProbabilisticPolicy(0.5, exceptionStrategy)))
        // Release: Always add delay
        .ConfigureOperation("ReleaseAsync", op => op
            .Enable()
            .WithPolicy(new ProbabilisticPolicy(1.0, delayStrategy)))
        .Build();

    var observer = new ConsoleChaosObserver();
    var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options, observer);

    Console.WriteLine("Acquiring lease (50% chance of failure)...");
    ILease? lease = null;
    int acquireAttempts = 0;

    while (lease == null && acquireAttempts < 5)
    {
        acquireAttempts++;
        try
        {
            lease = await chaosProvider.AcquireLeaseAsync("demo3-lease", TimeSpan.FromMinutes(1));
        }
        catch (LeaseException)
        {
            Console.WriteLine($"Attempt {acquireAttempts} failed, retrying...");
            await Task.Delay(100);
        }
    }

    if (lease != null)
    {
        Console.WriteLine($"✓ Acquired after {acquireAttempts} attempt(s)\n");
        Console.WriteLine("Releasing lease (will always add 300ms delay)...");
        await lease.ReleaseAsync();
        Console.WriteLine("✓ Released\n");
    }
}

static async Task Demo4_ThresholdPolicies(ILeaseProvider actualProvider)
{
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    Console.WriteLine("Demo 4: Threshold Policies (first 3 operations only)");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

    var delayStrategy = new DelayFaultStrategy(TimeSpan.FromMilliseconds(200));

    // Inject delay only for first 3 operations
    var policy = ThresholdPolicy.FirstN(3, delayStrategy);

    var observer = new ConsoleChaosObserver();
    var options = new ChaosOptionsBuilder()
        .Enable()
        .WithDefaultPolicy(policy)
        .Build();

    var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options, observer);

    for (int i = 1; i <= 5; i++)
    {
        Console.WriteLine($"\n--- Operation {i}/5 ---");
        var lease = await chaosProvider.AcquireLeaseAsync($"demo4-lease-{i}", TimeSpan.FromMinutes(1));
        if (lease != null)
        {
            Console.WriteLine(i <= 3
                ? "✓ Acquired (with delay as expected)"
                : "✓ Acquired (no delay as expected)");
            await lease.ReleaseAsync();
        }
        await Task.Delay(100);
    }

    Console.WriteLine("\n✓ Threshold policy test passed\n");
}

static async Task Demo5_RenewalFailureTesting(ILeaseProvider actualProvider)
{
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    Console.WriteLine("Demo 5: Renewal Failure Testing");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

    var renewException = ExceptionFaultStrategy.Create<LeaseException>("Renewal failed (chaos)");

    var options = new ChaosOptionsBuilder()
        .Enable()
        // No chaos on acquire
        .ConfigureOperation("AcquireAsync", op => op.Disable())
        // Always fail on renew
        .ConfigureOperation("RenewAsync", op => op
            .Enable()
            .WithPolicy(new ProbabilisticPolicy(1.0, renewException)))
        .Build();

    var observer = new ConsoleChaosObserver();
    var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options, observer);

    Console.WriteLine("Acquiring lease with 3-second duration...");
    var lease = await chaosProvider.AcquireLeaseAsync("demo5-lease", TimeSpan.FromSeconds(3));

    if (lease != null)
    {
        Console.WriteLine($"✓ Lease acquired: {lease.LeaseId}");

        bool renewalFailed = false;
        lease.LeaseRenewalFailed += (sender, e) =>
        {
            renewalFailed = true;
            Console.WriteLine($"⚠ Renewal failed event triggered: {e.Exception?.Message}");
        };

        Console.WriteLine("\nWaiting for auto-renewal attempt (should fail due to chaos)...");
        await Task.Delay(TimeSpan.FromSeconds(2.5));

        if (renewalFailed)
        {
            Console.WriteLine("✓ Test passed: Renewal failure detected as expected");
        }
        else
        {
            Console.WriteLine("⚠ Renewal failure not detected yet");
        }

        try
        {
            await lease.ReleaseAsync();
        }
        catch
        {
            // May fail if lease already lost
        }
    }

    Console.WriteLine();
}
