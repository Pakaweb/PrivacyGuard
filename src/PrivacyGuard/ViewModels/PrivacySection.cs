namespace PrivacyGuard.ViewModels;

/// <summary>
/// A labeled group of dashboard setting cards.
/// </summary>
public partial class PrivacySection : ObservableObject
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public ObservableCollection<PrivacyStatusItem> Items { get; } = [];

    [ObservableProperty]
    private bool _isVisible = true;
}
