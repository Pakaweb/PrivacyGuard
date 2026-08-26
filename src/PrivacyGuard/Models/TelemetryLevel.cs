namespace PrivacyGuard.Models;

/// <summary>
/// Windows diagnostic data (telemetry) levels documented by Microsoft.
/// Stored as DWORD <c>AllowTelemetry</c>.
/// </summary>
/// <remarks>
/// Security (0) is honored only on Enterprise, Education, IoT, and Server SKUs.
/// On Windows 10/11 Home and Pro, Windows treats 0 as Basic (1).
/// </remarks>
public enum TelemetryLevel
{
    /// <summary>Security-only diagnostic data. Enterprise/Education SKUs only.</summary>
    Security = 0,

    /// <summary>Required / Basic diagnostic data.</summary>
    Basic = 1,

    /// <summary>Enhanced diagnostic data (legacy; maps to Optional on newer Windows).</summary>
    Enhanced = 2,

    /// <summary>Full / Optional diagnostic data. Windows default.</summary>
    Full = 3
}
