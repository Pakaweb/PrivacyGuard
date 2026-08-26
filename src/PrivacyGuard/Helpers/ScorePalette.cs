using Microsoft.UI;
using Windows.UI;

namespace PrivacyGuard.Helpers;

/// <summary>
/// Shared palette for the privacy score ring and related chrome.
/// </summary>
public static class ScorePalette
{
    public static Color ColorForScore(double normalized)
    {
        var t = Math.Clamp(normalized, 0, 1);
        Color a;
        Color b;
        double local;

        switch (t)
        {
            case < 0.33:
                a = ColorHelper.FromArgb(255, 196, 43, 28);
                b = ColorHelper.FromArgb(255, 232, 138, 26);
                local = t / 0.33;
                break;
            case < 0.66:
                a = ColorHelper.FromArgb(255, 232, 138, 26);
                b = ColorHelper.FromArgb(255, 212, 176, 24);
                local = (t - 0.33) / 0.33;
                break;
            default:
                a = ColorHelper.FromArgb(255, 212, 176, 24);
                b = ColorHelper.FromArgb(255, 22, 163, 94);
                local = (t - 0.66) / 0.34;
                break;
        }

        return ColorHelper.FromArgb(
            255,
            Lerp(a.R, b.R, local),
            Lerp(a.G, b.G, local),
            Lerp(a.B, b.B, local));
    }

    private static byte Lerp(byte from, byte to, double t) =>
        (byte)Math.Clamp(from + ((to - from) * t), 0, 255);
}
