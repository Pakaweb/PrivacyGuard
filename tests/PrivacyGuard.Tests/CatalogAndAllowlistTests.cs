using PrivacyGuard.Helpers;
using PrivacyGuard.Models;

namespace PrivacyGuard.Tests;

public sealed class PrivacyCatalogTests
{
    [Fact]
    public void HealthFor_TelemetrySecurityAndBasic_AreProtected()
    {
        Assert.Equal(PrivacyHealth.Protected, PrivacyCatalog.HealthFor(PrivacySettingKeys.TelemetryLevel, "0"));
        Assert.Equal(PrivacyHealth.Protected, PrivacyCatalog.HealthFor(PrivacySettingKeys.TelemetryLevel, "1"));
        Assert.Equal(PrivacyHealth.Partial, PrivacyCatalog.HealthFor(PrivacySettingKeys.TelemetryLevel, "2"));
        Assert.Equal(PrivacyHealth.Collecting, PrivacyCatalog.HealthFor(PrivacySettingKeys.TelemetryLevel, "3"));
    }

    [Fact]
    public void ScoreFromValues_RecommendedShape_IsInProtectedRange()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PrivacySettingKeys.TelemetryLevel] = "1",
            [PrivacySettingKeys.DiagTrack] = "RunningAutomatic",
            [PrivacySettingKeys.DmwAppPush] = "StoppedDisabled",
            [PrivacySettingKeys.AdvertisingId] = "0",
            [PrivacySettingKeys.ActivityHistory] = "0",
            [PrivacySettingKeys.Cortana] = "0",
            [PrivacySettingKeys.Copilot] = "0",
            [PrivacySettingKeys.Feedback] = "0",
            [PrivacySettingKeys.TailoredExperiences] = "0"
        };

        var score = PrivacyCatalog.ScoreFromValues(values);
        Assert.True(score >= 70, $"Expected a protected-range score, got {score}.");
    }

    [Fact]
    public void ScoreFromValues_WindowsDefaults_IsLow()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PrivacySettingKeys.TelemetryLevel] = "3",
            [PrivacySettingKeys.AdvertisingId] = "1",
            [PrivacySettingKeys.ActivityHistory] = "1"
        };

        Assert.True(PrivacyCatalog.ScoreFromValues(values) < 40);
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("3", true)]
    [InlineData("9", false)]
    public void IsValidCanonical_Telemetry(string value, bool expected) =>
        Assert.Equal(expected, PrivacyCatalog.IsValidCanonical(PrivacySettingKeys.TelemetryLevel, value));
}

public sealed class WindowsServiceHelperTests
{
    [Fact]
    public void CanControl_AllowsOnlyPrivacyServices()
    {
        Assert.True(WindowsServiceHelper.CanControl("DiagTrack"));
        Assert.True(WindowsServiceHelper.CanControl("dmwappushservice"));
    }

    [Theory]
    [InlineData("Winlogon")]
    [InlineData("RpcSs")]
    [InlineData("wuauserv")]
    [InlineData("")]
    public void CanControl_RefusesUnknownAndProtected(string name) =>
        Assert.False(WindowsServiceHelper.CanControl(name));
}
