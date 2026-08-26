namespace PrivacyGuard.Models;

/// <summary>
/// User-authored profile persisted as JSON under LocalAppData.
/// Values are canonical strings used by <c>PrivacyService</c>.
/// </summary>
public sealed class CustomProfileDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}
