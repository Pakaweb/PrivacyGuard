namespace PrivacyGuard.Models;

/// <summary>
/// Result of applying one or more privacy operations.
/// </summary>
public sealed class PrivacyOperationResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public long? RestorePointId { get; init; }

    public IReadOnlyList<ChangeRecord> Changes { get; init; } = [];

    public IReadOnlyList<string> Errors { get; init; } = [];

    public static PrivacyOperationResult Failed(string message) =>
        new() { Success = false, Message = message, Errors = [message] };
}
