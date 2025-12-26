# Chaos Engineering Sample

This sample demonstrates the new chaos engineering capabilities of DistributedLeasing version 5.x.

## What This Sample Demonstrates

1. **Basic Chaos Injection** - Simple fault injection with observability
2. **Deterministic Testing** - Reproducible test scenarios
3. **Per-Operation Configuration** - Different chaos settings per operation
4. **Renewal Failure Testing** - Testing auto-renewal failures
5. **Threshold Policies** - Time and count-based fault injection

## Prerequisites

- .NET 8.0 or later
- Azure Storage Account (for blob provider)

## Running the Sample

```bash
cd samples/ChaosSample
dotnet run
```

## Configuration

Set your Azure Storage connection string:

```bash
export AZURE_STORAGE_CONNECTION_STRING="your-connection-string"
```

Or use development storage:

```bash
export AZURE_STORAGE_CONNECTION_STRING="UseDevelopmentStorage=true"
```

## Code Examples

See `Program.cs` for complete examples of:

- Creating fault strategies
- Configuring policies
- Setting up observers
- Testing different failure scenarios
- Validating retry logic
- Testing auto-renewal failures

## Expected Output

The sample will show colorful console output demonstrating:
- Fault injection decisions
- Strategy executions
- Timing information
- Success/failure indicators

Example:
```
[2024-12-26 10:30:45.123] [INJECT] Decision by 'ProbabilisticPolicy' for AcquireAsync on 'demo-lease'
[2024-12-26 10:30:45.150] [EXECUTING] Fault 'DelayFaultStrategy' (Severity: Low)
[2024-12-26 10:30:45.652] [EXECUTED] Fault 'DelayFaultStrategy' completed in 502.35ms
```
