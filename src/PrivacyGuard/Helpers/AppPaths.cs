namespace PrivacyGuard.Helpers;

/// <summary>
/// Well-known application data locations under the current user's LocalAppData.
/// </summary>
public static class AppPaths
{
    public const string AppFolderName = "PrivacyGuard";

    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppFolderName);

    public static string LogsDirectory { get; } = Path.Combine(Root, "logs");

    public static string DatabasePath { get; } = Path.Combine(Root, "privacyguard.db");

    public static string PreferencesPath { get; } = Path.Combine(Root, "preferences.json");

    public static string CustomProfilesPath { get; } = Path.Combine(Root, "custom-profiles.json");

    public static string AppliedBaselinePath { get; } = Path.Combine(Root, "applied-baseline.json");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LogsDirectory);
    }
}
