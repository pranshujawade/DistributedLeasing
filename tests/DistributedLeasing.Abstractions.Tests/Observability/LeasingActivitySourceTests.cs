using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DistributedLeasing.Abstractions.Observability;
using FluentAssertions;
using Xunit;

namespace DistributedLeasing.Abstractions.Tests.Observability;

#if NET5_0_OR_GREATER
public class LeasingActivitySourceTests : IDisposable
{
    private readonly ActivityListener _activityListener;
    private readonly List<Activity> _activities;

    public LeasingActivitySourceTests()
    {
        _activities = new List<Activity>();

        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "DistributedLeasing",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => _activities.Add(activity)
        };

        ActivitySource.AddActivityListener(_activityListener);
    }

    public void Dispose()
    {
        _activityListener?.Dispose();
    }

    [Fact]
    public void Source_HasCorrectName()
    {
        // Assert
        LeasingActivitySource.Source.Should().NotBeNull();
        LeasingActivitySource.Source.Name.Should().Be("DistributedLeasing");
    }

    [Fact]
    public void Source_HasCorrectVersion()
    {
        // Assert
        LeasingActivitySource.Source.Version.Should().Be("1.0.1");
    }

    [Fact]
    public void Operations_Acquire_HasCorrectValue()
    {
        // Assert
        LeasingActivitySource.Operations.Acquire.Should().Be("Lease.Acquire");
    }

    [Fact]
    public void Operations_TryAcquire_HasCorrectValue()
    {
        // Assert
        LeasingActivitySource.Operations.TryAcquire.Should().Be("Lease.TryAcquire");
    }

    [Fact]
    public void Operations_Renew_HasCorrectValue()
    {
        // Assert
        LeasingActivitySource.Operations.Renew.Should().Be("Lease.Renew");
    }

    [Fact]
    public void Operations_Release_HasCorrectValue()
    {
        // Assert
        LeasingActivitySource.Operations.Release.Should().Be("Lease.Release");
    }

    [Fact]
    public void Operations_Break_HasCorrectValue()
    {
        // Assert
        LeasingActivitySource.Operations.Break.Should().Be("Lease.Break");
    }

    [Fact]
    public void Operations_AutoRenewal_HasCorrectValue()
    {
        // Assert
        LeasingActivitySource.Operations.AutoRenewal.Should().Be("Lease.AutoRenewal");
    }

    [Fact]
    public void Tags_LeaseName_HasCorrectKey()
    {
        // Assert
        LeasingActivitySource.Tags.LeaseName.Should().Be("lease.name");
    }

    [Fact]
    public void Tags_LeaseId_HasCorrectKey()
    {
        // Assert
        LeasingActivitySource.Tags.LeaseId.Should().Be("lease.id");
    }

    [Fact]
    public void Tags_Provider_HasCorrectKey()
    {
        // Assert
        LeasingActivitySource.Tags.Provider.Should().Be("lease.provider");
    }

    [Fact]
    public void Tags_Duration_HasCorrectKey()
    {
        // Assert
        LeasingActivitySource.Tags.Duration.Should().Be("lease.duration_seconds");
    }

    [Fact]
    public void Tags_Timeout_HasCorrectKey()
    {
        // Assert
        LeasingActivitySource.Tags.Timeout.Should().Be("lease.timeout_seconds");
    }

    [Fact]
    public void Tags_Result_HasCorrectKey()
    {
        // Assert
        LeasingActivitySource.Tags.Result.Should().Be("lease.result");
    }

    [Fact]
    public void Tags_AutoRenew_HasCorrectKey()
    {
        // Assert
        LeasingActivitySource.Tags.AutoRenew.Should().Be("lease.auto_renew");
    }

    [Fact]
    public void Tags_RetryAttempts_HasCorrectKey()
    {
        // Assert
        LeasingActivitySource.Tags.RetryAttempts.Should().Be("lease.retry_attempts");
    }

    [Fact]
    public void Tags_LossReason_HasCorrectKey()
    {
        // Assert
        LeasingActivitySource.Tags.LossReason.Should().Be("lease.loss_reason");
    }

    [Fact]
    public void Tags_ExceptionType_HasCorrectKey()
    {
        // Assert
        LeasingActivitySource.Tags.ExceptionType.Should().Be("exception.type");
    }

    [Fact]
    public void Tags_ExceptionMessage_HasCorrectKey()
    {
        // Assert
        LeasingActivitySource.Tags.ExceptionMessage.Should().Be("exception.message");
    }

    [Fact]
    public void Results_Success_HasCorrectValue()
    {
        // Assert
        LeasingActivitySource.Results.Success.Should().Be("success");
    }

    [Fact]
    public void Results_Failure_HasCorrectValue()
    {
        // Assert
        LeasingActivitySource.Results.Failure.Should().Be("failure");
    }

    [Fact]
    public void Results_Timeout_HasCorrectValue()
    {
        // Assert
        LeasingActivitySource.Results.Timeout.Should().Be("timeout");
    }

    [Fact]
    public void Results_AlreadyHeld_HasCorrectValue()
    {
        // Assert
        LeasingActivitySource.Results.AlreadyHeld.Should().Be("already_held");
    }

    [Fact]
    public void Results_Lost_HasCorrectValue()
    {
        // Assert
        LeasingActivitySource.Results.Lost.Should().Be("lost");
    }

    [Fact]
    public void Source_CanStartActivity()
    {
        // Arrange
        _activities.Clear();

        // Act
        using var activity = LeasingActivitySource.Source.StartActivity(LeasingActivitySource.Operations.Acquire);

        // Assert
        activity.Should().NotBeNull();
        activity!.DisplayName.Should().Be(LeasingActivitySource.Operations.Acquire);
        _activities.Should().ContainSingle();
    }

    [Fact]
    public void Source_CanStartActivityWithTags()
    {
        // Arrange
        _activities.Clear();
        var tags = new ActivityTagsCollection
        {
            { LeasingActivitySource.Tags.LeaseName, "test-lease" },
            { LeasingActivitySource.Tags.Provider, "BlobLeaseProvider" },
            { LeasingActivitySource.Tags.Result, LeasingActivitySource.Results.Success }
        };

        // Act
        using var activity = LeasingActivitySource.Source.StartActivity(
            LeasingActivitySource.Operations.Acquire,
            ActivityKind.Client,
            parentContext: default(ActivityContext),
            tags);

        // Assert
        activity.Should().NotBeNull();
        var activityTags = activity!.Tags.ToList();
        activityTags.Should().Contain(new KeyValuePair<string, string?>(LeasingActivitySource.Tags.LeaseName, "test-lease"));
        activityTags.Should().Contain(new KeyValuePair<string, string?>(LeasingActivitySource.Tags.Provider, "BlobLeaseProvider"));
    }

    [Fact]
    public void Source_CanStartActivityForEachOperation()
    {
        // Arrange
        _activities.Clear();

        // Act
        using (var acquire = LeasingActivitySource.Source.StartActivity(LeasingActivitySource.Operations.Acquire)) { }
        using (var tryAcquire = LeasingActivitySource.Source.StartActivity(LeasingActivitySource.Operations.TryAcquire)) { }
        using (var renew = LeasingActivitySource.Source.StartActivity(LeasingActivitySource.Operations.Renew)) { }
        using (var release = LeasingActivitySource.Source.StartActivity(LeasingActivitySource.Operations.Release)) { }
        using (var breakLease = LeasingActivitySource.Source.StartActivity(LeasingActivitySource.Operations.Break)) { }
        using (var autoRenewal = LeasingActivitySource.Source.StartActivity(LeasingActivitySource.Operations.AutoRenewal)) { }

        // Assert
        _activities.Should().HaveCount(6);
        _activities.Should().Contain(a => a.DisplayName == LeasingActivitySource.Operations.Acquire);
        _activities.Should().Contain(a => a.DisplayName == LeasingActivitySource.Operations.TryAcquire);
        _activities.Should().Contain(a => a.DisplayName == LeasingActivitySource.Operations.Renew);
        _activities.Should().Contain(a => a.DisplayName == LeasingActivitySource.Operations.Release);
        _activities.Should().Contain(a => a.DisplayName == LeasingActivitySource.Operations.Break);
        _activities.Should().Contain(a => a.DisplayName == LeasingActivitySource.Operations.AutoRenewal);
    }

    [Fact]
    public void Activity_CanSetAllTags()
    {
        // Act
        using var activity = LeasingActivitySource.Source.StartActivity(LeasingActivitySource.Operations.Acquire);
        activity?.SetTag(LeasingActivitySource.Tags.LeaseName, "my-lease");
        activity?.SetTag(LeasingActivitySource.Tags.LeaseId, "lease-123");
        activity?.SetTag(LeasingActivitySource.Tags.Provider, "RedisLeaseProvider");
        activity?.SetTag(LeasingActivitySource.Tags.Duration, "60");
        activity?.SetTag(LeasingActivitySource.Tags.Timeout, "30");
        activity?.SetTag(LeasingActivitySource.Tags.Result, LeasingActivitySource.Results.Success);
        activity?.SetTag(LeasingActivitySource.Tags.AutoRenew, "true");
        activity?.SetTag(LeasingActivitySource.Tags.RetryAttempts, "3");

        // Assert
        activity.Should().NotBeNull();
        var activityTags = activity!.Tags.ToList();
        activityTags.Should().Contain(new KeyValuePair<string, string?>(LeasingActivitySource.Tags.LeaseName, "my-lease"));
        activityTags.Should().Contain(new KeyValuePair<string, string?>(LeasingActivitySource.Tags.LeaseId, "lease-123"));
        activityTags.Should().Contain(new KeyValuePair<string, string?>(LeasingActivitySource.Tags.Provider, "RedisLeaseProvider"));
        activityTags.Should().Contain(new KeyValuePair<string, string?>(LeasingActivitySource.Tags.Duration, "60"));
        activityTags.Should().Contain(new KeyValuePair<string, string?>(LeasingActivitySource.Tags.AutoRenew, "true"));
    }

    [Fact]
    public void Activity_CanSetExceptionTags()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");

        // Act
        using var activity = LeasingActivitySource.Source.StartActivity(LeasingActivitySource.Operations.Acquire);
        activity?.SetTag(LeasingActivitySource.Tags.ExceptionType, exception.GetType().Name);
        activity?.SetTag(LeasingActivitySource.Tags.ExceptionMessage, exception.Message);
        activity?.SetTag(LeasingActivitySource.Tags.Result, LeasingActivitySource.Results.Failure);

        // Assert
        activity.Should().NotBeNull();
        var activityTags = activity!.Tags.ToList();
        activityTags.Should().Contain(new KeyValuePair<string, string?>(LeasingActivitySource.Tags.ExceptionType, "InvalidOperationException"));
        activityTags.Should().Contain(new KeyValuePair<string, string?>(LeasingActivitySource.Tags.ExceptionMessage, "Test error"));
        activityTags.Should().Contain(new KeyValuePair<string, string?>(LeasingActivitySource.Tags.Result, LeasingActivitySource.Results.Failure));
    }

    [Fact]
    public void Activity_CanSetLossReasonTag()
    {
        // Act
        using var activity = LeasingActivitySource.Source.StartActivity(LeasingActivitySource.Operations.Renew);
        activity?.SetTag(LeasingActivitySource.Tags.LossReason, "lease_expired");
        activity?.SetTag(LeasingActivitySource.Tags.Result, LeasingActivitySource.Results.Lost);

        // Assert
        activity.Should().NotBeNull();
        var activityTags = activity!.Tags.ToList();
        activityTags.Should().Contain(new KeyValuePair<string, string?>(LeasingActivitySource.Tags.LossReason, "lease_expired"));
    }

    [Fact]
    public void TagKeys_FollowOpenTelemetryConventions()
    {
        // Assert - tag keys should use lowercase with dots/underscores
        LeasingActivitySource.Tags.LeaseName.Should().MatchRegex(@"^[a-z][a-z0-9._]*$");
        LeasingActivitySource.Tags.LeaseId.Should().MatchRegex(@"^[a-z][a-z0-9._]*$");
        LeasingActivitySource.Tags.Provider.Should().MatchRegex(@"^[a-z][a-z0-9._]*$");
        LeasingActivitySource.Tags.Duration.Should().MatchRegex(@"^[a-z][a-z0-9._]*$");
        LeasingActivitySource.Tags.ExceptionType.Should().MatchRegex(@"^[a-z][a-z0-9._]*$");
    }

    [Fact]
    public void OperationNames_FollowNamingConventions()
    {
        // Assert - operation names should use PascalCase with dot separator
        LeasingActivitySource.Operations.Acquire.Should().MatchRegex(@"^[A-Z][a-zA-Z0-9.]*$");
        LeasingActivitySource.Operations.TryAcquire.Should().MatchRegex(@"^[A-Z][a-zA-Z0-9.]*$");
        LeasingActivitySource.Operations.Renew.Should().MatchRegex(@"^[A-Z][a-zA-Z0-9.]*$");
        LeasingActivitySource.Operations.Release.Should().MatchRegex(@"^[A-Z][a-zA-Z0-9.]*$");
    }

    [Fact]
    public void AllOperations_StartWithLeasePrefix()
    {
        // Assert
        LeasingActivitySource.Operations.Acquire.Should().StartWith("Lease.");
        LeasingActivitySource.Operations.TryAcquire.Should().StartWith("Lease.");
        LeasingActivitySource.Operations.Renew.Should().StartWith("Lease.");
        LeasingActivitySource.Operations.Release.Should().StartWith("Lease.");
        LeasingActivitySource.Operations.Break.Should().StartWith("Lease.");
        LeasingActivitySource.Operations.AutoRenewal.Should().StartWith("Lease.");
    }

    [Fact]
    public void AllTags_StartWithLeaseOrExceptionPrefix()
    {
        // Assert
        LeasingActivitySource.Tags.LeaseName.Should().StartWith("lease.");
        LeasingActivitySource.Tags.LeaseId.Should().StartWith("lease.");
        LeasingActivitySource.Tags.Provider.Should().StartWith("lease.");
        LeasingActivitySource.Tags.Duration.Should().StartWith("lease.");
        LeasingActivitySource.Tags.Timeout.Should().StartWith("lease.");
        LeasingActivitySource.Tags.Result.Should().StartWith("lease.");
        LeasingActivitySource.Tags.AutoRenew.Should().StartWith("lease.");
        LeasingActivitySource.Tags.RetryAttempts.Should().StartWith("lease.");
        LeasingActivitySource.Tags.LossReason.Should().StartWith("lease.");
        LeasingActivitySource.Tags.ExceptionType.Should().StartWith("exception.");
        LeasingActivitySource.Tags.ExceptionMessage.Should().StartWith("exception.");
    }

    [Fact]
    public void ResultValues_UseLowerCaseSnakeCase()
    {
        // Assert
        LeasingActivitySource.Results.Success.Should().MatchRegex(@"^[a-z][a-z_]*$");
        LeasingActivitySource.Results.Failure.Should().MatchRegex(@"^[a-z][a-z_]*$");
        LeasingActivitySource.Results.Timeout.Should().MatchRegex(@"^[a-z][a-z_]*$");
        LeasingActivitySource.Results.AlreadyHeld.Should().MatchRegex(@"^[a-z][a-z_]*$");
        LeasingActivitySource.Results.Lost.Should().MatchRegex(@"^[a-z][a-z_]*$");
    }

    [Fact]
    public void Activity_CanBeStoppedAndDisposed()
    {
        // Arrange
        var activity = LeasingActivitySource.Source.StartActivity(LeasingActivitySource.Operations.Acquire);

        // Act
        activity?.SetTag(LeasingActivitySource.Tags.Result, LeasingActivitySource.Results.Success);
        activity?.Stop();
        activity?.Dispose();

        // Assert - should not throw
        activity.Should().NotBeNull();
    }
}
#endif
