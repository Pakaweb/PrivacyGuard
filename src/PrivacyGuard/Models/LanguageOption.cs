using Microsoft.UI.Xaml;

namespace PrivacyGuard.Models;

/// <summary>
/// A selectable UI culture from the operating system's culture catalog.
/// </summary>
public sealed class LanguageOption
{
    public required string Code { get; init; }

    public required string NativeName { get; init; }

    public required string EnglishName { get; init; }

    public string SecondaryLabel => $"{EnglishName} · {Code}";

    public override string ToString() => NativeName;

    public override bool Equals(object? obj) =>
        obj is LanguageOption other && Code.Equals(other.Code, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Code);
}

/// <summary>
/// A selectable app theme with a friendly label for the Settings combo.
/// </summary>
public sealed class ThemeOption
{
    public required ElementTheme Theme { get; init; }

    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public override string ToString() => Title;
}
