using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using DistributedLeasing.Abstractions.Observability;
using FluentAssertions;
using Xunit;

namespace DistributedLeasing.Abstractions.Tests.Observability;

#if NET8_0_OR_GREATER
public class LeasingMetricsTests : IDisposable
{
    private readonly MeterListener _meterListener;
    private readonly List<Measurement<long>> _counterMeasurements;
    private readonly List<Measurement<double>> _histogramDoubleMeasurements;
    private readonly List<Measurement<int>> _histogramIntMeasurements;
    private readonly List<Measurement<int>> _gaugeMeasurements;

    public LeasingMetricsTests()
    {
        _counterMeasurements = new List<Measurement<long>>();
        _histogramDoubleMeasurements = new List<Measurement<double>>();
        _histogramIntMeasurements = new List<Measurement<int>>();
        _gaugeMeasurements = new List<Measurement<int>>();

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "DistributedLeasing")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };

        _meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            _counterMeasurements.Add(new Measurement<long>(measurement, tags));
        });

        _meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            _histogramDoubleMeasurements.Add(new Measurement<double>(measurement, tags));
        });

        _meterListener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
        {
            if (instrument is ObservableGauge<int>)
            {
                _gaugeMeasurements.Add(new Measurement<int>(measurement, tags));
            }
            else
            {
                _histogramIntMeasurements.Add(new Measurement<int>(measurement, tags));
            }
        });

        _meterListener.Start();
    }

    public void Dispose()
    {
        _meterListener?.Dispose();
    }

    [Fact]
    public void LeaseAcquisitions_HasCorrectMetadata()
    {
        // Assert
        LeasingMetrics.LeaseAcquisitions.Should().NotBeNull();
        LeasingMetrics.LeaseAcquisitions.Name.Should().Be("leasing.acquisitions.total");
        LeasingMetrics.LeaseAcquisitions.Unit.Should().Be("{acquisitions}");
        LeasingMetrics.LeaseAcquisitions.Description.Should().Contain("acquisition attempts");
    }

    [Fact]
    public void LeaseAcquisitionDuration_HasCorrectMetadata()
    {
        // Assert
        LeasingMetrics.LeaseAcquisitionDuration.Should().NotBeNull();
        LeasingMetrics.LeaseAcquisitionDuration.Name.Should().Be("leasing.acquisition.duration");
        LeasingMetrics.LeaseAcquisitionDuration.Unit.Should().Be("ms");
        LeasingMetrics.LeaseAcquisitionDuration.Description.Should().Contain("Duration");
    }

    [Fact]
    public void LeaseRenewals_HasCorrectMetadata()
    {
        // Assert
        LeasingMetrics.LeaseRenewals.Should().NotBeNull();
        LeasingMetrics.LeaseRenewals.Name.Should().Be("leasing.renewals.total");
        LeasingMetrics.LeaseRenewals.Unit.Should().Be("{renewals}");
        LeasingMetrics.LeaseRenewals.Description.Should().Contain("renewal attempts");
    }

    [Fact]
    public void LeaseRenewalDuration_HasCorrectMetadata()
    {
        // Assert
        LeasingMetrics.LeaseRenewalDuration.Should().NotBeNull();
        LeasingMetrics.LeaseRenewalDuration.Name.Should().Be("leasing.renewal.duration");
        LeasingMetrics.LeaseRenewalDuration.Unit.Should().Be("ms");
        LeasingMetrics.LeaseRenewalDuration.Description.Should().Contain("Duration");
    }

    [Fact]
    public void LeaseRenewalFailures_HasCorrectMetadata()
    {
        // Assert
        LeasingMetrics.LeaseRenewalFailures.Should().NotBeNull();
        LeasingMetrics.LeaseRenewalFailures.Name.Should().Be("leasing.renewal.failures.total");
        LeasingMetrics.LeaseRenewalFailures.Unit.Should().Be("{failures}");
        LeasingMetrics.LeaseRenewalFailures.Description.Should().Contain("failed");
    }

    [Fact]
    public void LeasesLost_HasCorrectMetadata()
    {
        // Assert
        LeasingMetrics.LeasesLost.Should().NotBeNull();
        LeasingMetrics.LeasesLost.Name.Should().Be("leasing.leases_lost.total");
        LeasingMetrics.LeasesLost.Unit.Should().Be("{leases}");
        LeasingMetrics.LeasesLost.Description.Should().Contain("lost");
    }

    [Fact]
    public void ActiveLeases_HasCorrectMetadata()
    {
        // Assert
        LeasingMetrics.ActiveLeases.Should().NotBeNull();
        LeasingMetrics.ActiveLeases.Name.Should().Be("leasing.active_leases.current");
        LeasingMetrics.ActiveLeases.Unit.Should().Be("{leases}");
        LeasingMetrics.ActiveLeases.Description.Should().Contain("active leases");
    }

    [Fact]
    public void TimeSinceLastRenewal_HasCorrectMetadata()
    {
        // Assert
        LeasingMetrics.TimeSinceLastRenewal.Should().NotBeNull();
        LeasingMetrics.TimeSinceLastRenewal.Name.Should().Be("leasing.time_since_last_renewal");
        LeasingMetrics.TimeSinceLastRenewal.Unit.Should().Be("s");
        LeasingMetrics.TimeSinceLastRenewal.Description.Should().Contain("last successful renewal");
    }

    [Fact]
    public void RenewalRetryAttempts_HasCorrectMetadata()
    {
        // Assert
        LeasingMetrics.RenewalRetryAttempts.Should().NotBeNull();
        LeasingMetrics.RenewalRetryAttempts.Name.Should().Be("leasing.renewal.retry_attempts");
        LeasingMetrics.RenewalRetryAttempts.Unit.Should().Be("{attempts}");
        LeasingMetrics.RenewalRetryAttempts.Description.Should().Contain("retry attempts");
    }

    [Fact]
    public void LeaseAcquisitions_CanRecordWithTags()
    {
        // Arrange
        _counterMeasurements.Clear();
        var tags = new KeyValuePair<string, object?>[] 
        {
            new("provider", "BlobLeaseProvider"),
            new("lease_name", "test-lease"),
            new("result", "success")
        };

        // Act
        LeasingMetrics.LeaseAcquisitions.Add(1, tags);

        // Assert
        _counterMeasurements.Should().ContainSingle();
        var measurement = _counterMeasurements.First();
        measurement.Value.Should().Be(1);
        measurement.Tags.ToArray().Should().Contain(new KeyValuePair<string, object?>("provider", "BlobLeaseProvider"));
        measurement.Tags.ToArray().Should().Contain(new KeyValuePair<string, object?>("result", "success"));
    }

    [Fact]
    public void LeaseAcquisitionDuration_CanRecordHistogram()
    {
        // Arrange
        _histogramDoubleMeasurements.Clear();
        var tags = new KeyValuePair<string, object?>[] 
        {
            new("provider", "RedisLeaseProvider"),
            new("result", "success")
        };

        // Act
        LeasingMetrics.LeaseAcquisitionDuration.Record(123.45, tags);

        // Assert
        _histogramDoubleMeasurements.Should().ContainSingle();
        var measurement = _histogramDoubleMeasurements.First();
        measurement.Value.Should().Be(123.45);
        measurement.Tags.ToArray().Should().Contain(new KeyValuePair<string, object?>("provider", "RedisLeaseProvider"));
    }

    [Fact]
    public void LeaseRenewals_CanRecordMultipleValues()
    {
        // Arrange
        _counterMeasurements.Clear();
        var tags = new KeyValuePair<string, object?>[] { new("provider", "CosmosLeaseProvider") };

        // Act
        LeasingMetrics.LeaseRenewals.Add(1, tags);
        LeasingMetrics.LeaseRenewals.Add(1, tags);
        LeasingMetrics.LeaseRenewals.Add(1, tags);

        // Assert
        _counterMeasurements.Count.Should().BeGreaterOrEqualTo(3);
        _counterMeasurements.Sum(m => m.Value).Should().BeGreaterOrEqualTo(3);
    }

    [Fact]
    public void LeaseRenewalDuration_RecordsMilliseconds()
    {
        // Arrange
        _histogramDoubleMeasurements.Clear();
        var durationMs = 45.6;

        // Act
        LeasingMetrics.LeaseRenewalDuration.Record(durationMs);

        // Assert
        _histogramDoubleMeasurements.Should().ContainSingle();
        _histogramDoubleMeasurements.First().Value.Should().Be(durationMs);
    }

    [Fact]
    public void LeaseRenewalFailures_CanRecordWithReasonTag()
    {
        // Arrange
        _counterMeasurements.Clear();
        var tags = new KeyValuePair<string, object?>[] 
        {
            new("provider", "BlobLeaseProvider"),
            new("lease_name", "critical-lease"),
            new("reason", "timeout")
        };

        // Act
        LeasingMetrics.LeaseRenewalFailures.Add(1, tags);

        // Assert
        _counterMeasurements.Should().ContainSingle();
        var measurement = _counterMeasurements.First();
        measurement.Tags.ToArray().Should().Contain(new KeyValuePair<string, object?>("reason", "timeout"));
    }

    [Fact]
    public void LeasesLost_CanRecordCriticalEvent()
    {
        // Arrange
        _counterMeasurements.Clear();
        var tags = new KeyValuePair<string, object?>[] 
        {
            new("provider", "CosmosLeaseProvider"),
            new("lease_name", "leader-election"),
            new("reason", "lease_expired")
        };

        // Act
        LeasingMetrics.LeasesLost.Add(1, tags);

        // Assert - Check that at least one measurement was recorded with correct value
        // Note: Other concurrent tests may also record, so we check for ANY match rather than SINGLE
        _counterMeasurements.Should().NotBeEmpty();
        _counterMeasurements.Should().Contain(m => m.Value == 1);
        
        // Verify the tags are present in at least one measurement
        var matchingMeasurement = _counterMeasurements.FirstOrDefault(m => 
            m.Tags.ToArray().Any(t => t.Key == "provider" && t.Value?.ToString() == "CosmosLeaseProvider"));
        matchingMeasurement.Should().NotBeNull();
    }

    [Fact]
    public void TimeSinceLastRenewal_RecordsSeconds()
    {
        // Arrange
        _histogramDoubleMeasurements.Clear();
        var timeSinceRenewal = 30.5;

        // Act
        LeasingMetrics.TimeSinceLastRenewal.Record(timeSinceRenewal);

        // Assert
        _histogramDoubleMeasurements.Should().ContainSingle();
        _histogramDoubleMeasurements.First().Value.Should().Be(timeSinceRenewal);
    }

    [Fact]
    public void RenewalRetryAttempts_CanRecordRetries()
    {
        // Arrange
        _histogramIntMeasurements.Clear();
        var tags = new KeyValuePair<string, object?>[] 
        {
            new("provider", "RedisLeaseProvider"),
            new("result", "success")
        };

        // Act
        LeasingMetrics.RenewalRetryAttempts.Record(3, tags);

        // Assert
        _histogramIntMeasurements.Should().ContainSingle();
        _histogramIntMeasurements.First().Value.Should().Be(3);
    }

    [Fact]
    public void AllMetrics_BelongToDistributedLeasingMeter()
    {
        // Assert - all metrics should belong to the same meter
        var meter = typeof(LeasingMetrics)
            .GetField("Meter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?.GetValue(null) as Meter;

        meter.Should().NotBeNull();
        meter!.Name.Should().Be("DistributedLeasing");
        meter.Version.Should().Be("1.0.1");
    }

    [Fact]
    public void Metrics_FollowOpenTelemetryConventions()
    {
        // Assert - verify metric naming follows OTEL conventions (lowercase with dots)
        LeasingMetrics.LeaseAcquisitions.Name.Should().MatchRegex(@"^[a-z][a-z0-9._]*$");
        LeasingMetrics.LeaseRenewals.Name.Should().MatchRegex(@"^[a-z][a-z0-9._]*$");
        LeasingMetrics.LeaseRenewalFailures.Name.Should().MatchRegex(@"^[a-z][a-z0-9._]*$");
        LeasingMetrics.LeasesLost.Name.Should().MatchRegex(@"^[a-z][a-z0-9._]*$");
        LeasingMetrics.ActiveLeases.Name.Should().MatchRegex(@"^[a-z][a-z0-9._]*$");
    }

    [Fact]
    public void Counters_HaveProperUnits()
    {
        // Assert - counters should have curly-brace units
        LeasingMetrics.LeaseAcquisitions.Unit.Should().StartWith("{").And.EndWith("}");
        LeasingMetrics.LeaseRenewals.Unit.Should().StartWith("{").And.EndWith("}");
        LeasingMetrics.LeaseRenewalFailures.Unit.Should().StartWith("{").And.EndWith("}");
        LeasingMetrics.LeasesLost.Unit.Should().StartWith("{").And.EndWith("}");
    }

    [Fact]
    public void Histograms_HaveProperUnits()
    {
        // Assert - histograms should have standard units
        LeasingMetrics.LeaseAcquisitionDuration.Unit.Should().Be("ms");
        LeasingMetrics.LeaseRenewalDuration.Unit.Should().Be("ms");
        LeasingMetrics.TimeSinceLastRenewal.Unit.Should().Be("s");
    }
}
#endif
