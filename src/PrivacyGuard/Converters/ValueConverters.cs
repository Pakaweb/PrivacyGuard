using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using PrivacyGuard.Helpers;
using PrivacyGuard.Models;
using PrivacyGuard.Services;
using PrivacyGuard.ViewModels;

namespace PrivacyGuard.Converters;

public sealed class HealthToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var health = value is PrivacyHealth h ? h : PrivacyHealth.Partial;
        return health switch
        {
            PrivacyHealth.Protected => new SolidColorBrush(ColorHelper.FromArgb(255, 22, 163, 94)),
            PrivacyHealth.Partial => new SolidColorBrush(ColorHelper.FromArgb(255, 232, 138, 26)),
            _ => new SolidColorBrush(ColorHelper.FromArgb(255, 196, 43, 28))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class StatusDotBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is PrivacyStatusItem item)
        {
            if (item.Health == PrivacyHealth.Protected)
            {
                return new SolidColorBrush(ColorHelper.FromArgb(255, 22, 163, 94));
            }

            return new SolidColorBrush(item.IsHighImpact
                ? ColorHelper.FromArgb(255, 196, 43, 28)
                : ColorHelper.FromArgb(255, 232, 138, 26));
        }

        return new SolidColorBrush(ColorHelper.FromArgb(255, 232, 138, 26));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class HealthToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is PrivacyHealth health
            ? health switch
            {
                PrivacyHealth.Protected => LocalizationService.Current.Get("health.protected"),
                PrivacyHealth.Partial => LocalizationService.Current.Get("health.partial"),
                _ => LocalizationService.Current.Get("health.collecting")
            }
            : LocalizationService.Current.Get("health.unknown");

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class InfoKindToSeverityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is InfoMessageKind kind
            ? kind switch
            {
                InfoMessageKind.Success => InfoBarSeverity.Success,
                InfoMessageKind.Warning => InfoBarSeverity.Warning,
                InfoMessageKind.Error => InfoBarSeverity.Error,
                _ => InfoBarSeverity.Informational
            }
            : InfoBarSeverity.Informational;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is not true;
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var flag = value is true;
        if (Invert)
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is null or "" ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class ScoreToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var color = ScorePalette.ColorForScore(
            value switch
            {
                double d => d / 100d,
                int i => i / 100d,
                _ => 0d
            });

        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class TelemetryLevelToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is TelemetryLevel level
            ? level switch
            {
                TelemetryLevel.Security => LocalizationService.Current.Get("telemetry.security"),
                TelemetryLevel.Basic => LocalizationService.Current.Get("telemetry.basic"),
                TelemetryLevel.Enhanced => LocalizationService.Current.Get("telemetry.enhanced"),
                TelemetryLevel.Full => LocalizationService.Current.Get("telemetry.full"),
                _ => level.ToString()
            }
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class ProfileBorderBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string state)
        {
            return new SolidColorBrush(ThemePalette.Overlay(40, 24));
        }

        if (state.EndsWith(":1", StringComparison.Ordinal))
        {
            return new SolidColorBrush(ColorHelper.FromArgb(255, 22, 163, 94));
        }

        if (state.StartsWith("Recommended", StringComparison.Ordinal))
        {
            return new SolidColorBrush(ThemePalette.Accent(180, 160));
        }

        if (state.StartsWith("Maximum", StringComparison.Ordinal))
        {
            return new SolidColorBrush(ColorHelper.FromArgb(160, 232, 138, 26));
        }

        if (state.StartsWith("Restore", StringComparison.Ordinal))
        {
            return new SolidColorBrush(ThemePalette.Overlay(36, 22));
        }

        if (state.StartsWith("Custom", StringComparison.Ordinal))
        {
            return new SolidColorBrush(ThemePalette.Accent(140, 120));
        }

        return new SolidColorBrush(ThemePalette.Overlay(48, 28));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class ProfileIconBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not ProfileOption option)
        {
            return new SolidColorBrush(ThemePalette.Overlay(18, 10));
        }

        if (option.IsRecommended || option.IsCustom)
        {
            return new SolidColorBrush(ThemePalette.Accent(40, 28));
        }

        if (option.IsMaximum)
        {
            return new SolidColorBrush(ColorHelper.FromArgb(40, 232, 138, 26));
        }

        if (option.IsRestore)
        {
            return new SolidColorBrush(ThemePalette.Overlay(12, 8));
        }

        return new SolidColorBrush(ThemePalette.Overlay(18, 10));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class ProfileBorderThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string state)
        {
            return new Thickness(1);
        }

        if (state.StartsWith("Recommended", StringComparison.Ordinal) ||
            state.StartsWith("Custom", StringComparison.Ordinal) ||
            state.EndsWith(":1", StringComparison.Ordinal))
        {
            return new Thickness(2);
        }

        return new Thickness(1);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class ProfileIconForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is ProfileOption option)
        {
            if (option.IsRecommended || option.IsCustom)
            {
                return new SolidColorBrush(ThemePalette.IsLight()
                    ? ColorHelper.FromArgb(255, 0, 95, 184)
                    : ColorHelper.FromArgb(255, 96, 205, 255));
            }

            if (option.IsMaximum)
            {
                return new SolidColorBrush(ColorHelper.FromArgb(255, 232, 138, 26));
            }

            if (option.IsRestore)
            {
                return new SolidColorBrush(ThemePalette.IsLight()
                    ? ColorHelper.FromArgb(200, 80, 80, 80)
                    : ColorHelper.FromArgb(180, 200, 200, 200));
            }
        }

        return new SolidColorBrush(ThemePalette.IsLight()
            ? ColorHelper.FromArgb(230, 32, 32, 32)
            : ColorHelper.FromArgb(230, 255, 255, 255));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class ProfileWarningBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string state)
        {
            if (state.StartsWith("Maximum", StringComparison.Ordinal))
            {
                return new SolidColorBrush(ColorHelper.FromArgb(78, 232, 138, 26));
            }

            if (state.StartsWith("Restore", StringComparison.Ordinal))
            {
                return new SolidColorBrush(ThemePalette.Overlay(16, 10));
            }
        }

        return new SolidColorBrush(ColorHelper.FromArgb(32, 196, 127, 23));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class ProfileWarningAccentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string state)
        {
            if (state.StartsWith("Maximum", StringComparison.Ordinal))
            {
                return new SolidColorBrush(ColorHelper.FromArgb(255, 245, 158, 11));
            }

            if (state.StartsWith("Restore", StringComparison.Ordinal))
            {
                return new SolidColorBrush(ThemePalette.Overlay(56, 40));
            }
        }

        return new SolidColorBrush(ColorHelper.FromArgb(220, 196, 127, 23));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class HistoryAccentBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var state = value as string ?? string.Empty;
        if (state.StartsWith("reverted", StringComparison.Ordinal))
        {
            return new SolidColorBrush(ColorHelper.FromArgb(255, 22, 163, 94));
        }

        if (state.StartsWith("error", StringComparison.Ordinal))
        {
            return new SolidColorBrush(ColorHelper.FromArgb(255, 196, 43, 28));
        }

        if (state.StartsWith("collect", StringComparison.Ordinal))
        {
            return new SolidColorBrush(ColorHelper.FromArgb(255, 232, 138, 26));
        }

        return new SolidColorBrush(ColorHelper.FromArgb(255, 96, 205, 255));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class HistoryRowBorderBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var state = value as string ?? string.Empty;
        var restore = (parameter as string)?.Equals("restore", StringComparison.OrdinalIgnoreCase) == true;
        if (state.Contains(":1", StringComparison.Ordinal) || state == "selected")
        {
            return new SolidColorBrush(ThemePalette.Accent(restore ? (byte)200 : (byte)120, restore ? (byte)160 : (byte)110));
        }

        return new SolidColorBrush(ThemePalette.Overlay(restore ? (byte)22 : (byte)36, restore ? (byte)16 : (byte)28));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class HistoryRowBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var state = value as string ?? string.Empty;
        var restore = (parameter as string)?.Equals("restore", StringComparison.OrdinalIgnoreCase) == true;
        if (state.Contains(":1", StringComparison.Ordinal) || state == "selected")
        {
            return new SolidColorBrush(ThemePalette.Accent(restore ? (byte)48 : (byte)28, restore ? (byte)36 : (byte)22));
        }

        return new SolidColorBrush(ThemePalette.Overlay(restore ? (byte)8 : (byte)14, restore ? (byte)6 : (byte)10));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class HistoryToneBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var tone = value as string ?? "neutral";
        var isBackground = (parameter as string)?.Equals("bg", StringComparison.OrdinalIgnoreCase) == true;

        if (tone == "protect")
        {
            if (ThemePalette.IsLight())
            {
                return isBackground
                    ? new SolidColorBrush(ColorHelper.FromArgb(255, 196, 235, 209))
                    : new SolidColorBrush(ColorHelper.FromArgb(255, 11, 106, 20));
            }

            return isBackground
                ? new SolidColorBrush(ColorHelper.FromArgb(40, 22, 163, 94))
                : new SolidColorBrush(ColorHelper.FromArgb(255, 108, 203, 95));
        }

        if (tone == "collect")
        {
            if (ThemePalette.IsLight())
            {
                return isBackground
                    ? new SolidColorBrush(ColorHelper.FromArgb(255, 255, 228, 196))
                    : new SolidColorBrush(ColorHelper.FromArgb(255, 156, 80, 12));
            }

            return isBackground
                ? new SolidColorBrush(ColorHelper.FromArgb(40, 232, 138, 26))
                : new SolidColorBrush(ColorHelper.FromArgb(255, 232, 168, 56));
        }

        if (ThemePalette.IsLight())
        {
            return isBackground
                ? new SolidColorBrush(ColorHelper.FromArgb(18, 0, 0, 0))
                : new SolidColorBrush(ColorHelper.FromArgb(210, 26, 26, 26));
        }

        return isBackground
            ? new SolidColorBrush(ColorHelper.FromArgb(22, 255, 255, 255))
            : new SolidColorBrush(ColorHelper.FromArgb(210, 232, 232, 232));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class IncludedOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? 1d : 0.48d;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class ServiceStateToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is DesiredServiceState state
            ? state switch
            {
                DesiredServiceState.RunningAutomatic => LocalizationService.Current.Get("service.runningAutomatic"),
                DesiredServiceState.StoppedManual => LocalizationService.Current.Get("service.stoppedManual"),
                DesiredServiceState.StoppedDisabled => LocalizationService.Current.Get("service.stoppedDisabled"),
                _ => state.ToString()
            }
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
