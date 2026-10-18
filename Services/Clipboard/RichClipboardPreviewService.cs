using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RadialLauncher.Services.Clipboard;

public enum RichClipboardKind
{
    PlainText,
    HexColor,
    Url,
    CodeSnippet
}

public sealed record RichClipboardMetadata(
    RichClipboardKind Kind,
    string FormattedTitle,
    string BadgeLabel,
    string AccentHex);

public interface IRichClipboardPreviewService
{
    RichClipboardMetadata Analyze(string rawText);
    ImageSource? CreateRichIcon(string rawText);
}

public sealed class RichClipboardPreviewService : IRichClipboardPreviewService
{
    private static readonly Regex HexColorRegex = new(
        @"^#(?:(?<rgb>[0-9a-fA-F]{3})|(?<rrggbb>[0-9a-fA-F]{6})|(?<aarrggbb>[0-9a-fA-F]{8}))$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex RgbFuncRegex = new(
        @"^rgb\(\s*(?<r>\d{1,3})\s*,\s*(?<g>\d{1,3})\s*,\s*(?<b>\d{1,3})\s*\)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] CodeKeywords =
    {
        "=>", "function ", "public ", "const ", "import ", "SELECT ", "git "
    };

    public RichClipboardMetadata Analyze(string rawText)
    {
        var trimmed = (rawText ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return new RichClipboardMetadata(
                RichClipboardKind.PlainText,
                "📋 (Boş)",
                "TXT",
                "#F59E0B");
        }

        if (TryParseColor(trimmed, out var displayHex, out var normalizedHex, out _))
        {
            var upperHex = displayHex.ToUpperInvariant();
            return new RichClipboardMetadata(
                RichClipboardKind.HexColor,
                $"🎨 Renk: {upperHex}",
                upperHex,
                normalizedHex);
        }

        if (TryParseHttpUrl(trimmed, out var host, out var shortPath))
        {
            return new RichClipboardMetadata(
                RichClipboardKind.Url,
                $"🔗 {host} — {shortPath}",
                "LINK",
                "#38BDF8");
        }

        var preview = CollapsePreview(trimmed, 42);
        if (IsCodeSnippet(trimmed))
        {
            return new RichClipboardMetadata(
                RichClipboardKind.CodeSnippet,
                $"💻 Kod: {preview}",
                "{ }",
                "#A855F7");
        }

        return new RichClipboardMetadata(
            RichClipboardKind.PlainText,
            $"📋 {preview}",
            "TXT",
            "#F59E0B");
    }

    public ImageSource? CreateRichIcon(string rawText)
    {
        var trimmed = (rawText ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (TryParseColor(trimmed, out var displayHex, out _, out var parsedColor))
        {
            var upperHex = displayHex.ToUpperInvariant();
            var label = upperHex.Length > 7 ? "#" + upperHex[^6..] : upperHex;
            var luminance = (0.299 * parsedColor.R + 0.587 * parsedColor.G + 0.114 * parsedColor.B) / 255.0;
            var fgColor = luminance > 0.58 ? Color.FromRgb(17, 24, 39) : Colors.White;
            return RenderSquircleBadge(parsedColor, fgColor, label, 10.0, "Consolas");
        }

        var metadata = Analyze(trimmed);
        return metadata.Kind switch
        {
            RichClipboardKind.Url => RenderSquircleBadge(
                Color.FromRgb(2, 132, 199),   // #0284C7
                Colors.White,
                "🌐",
                22.0,
                "Segoe UI Emoji"),
            RichClipboardKind.CodeSnippet => RenderSquircleBadge(
                Color.FromRgb(124, 58, 237),  // #7C3AED
                Colors.White,
                "{ }",
                18.0,
                "Consolas"),
            RichClipboardKind.PlainText => RenderSquircleBadge(
                Color.FromRgb(217, 119, 6),   // #D97706
                Colors.White,
                "📋",
                22.0,
                "Segoe UI Emoji"),
            _ => null
        };
    }

    private static ImageSource RenderSquircleBadge(
        Color fillColor,
        Color foregroundColor,
        string centerText,
        double fontSize,
        string fontFamilyName)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var fillBrush = new SolidColorBrush(fillColor);
            fillBrush.Freeze();
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(96, 255, 255, 255)), 1.5);
            borderPen.Freeze();

            dc.DrawRoundedRectangle(fillBrush, borderPen, new Rect(4, 4, 56, 56), 14, 14);

            var fgBrush = new SolidColorBrush(foregroundColor);
            fgBrush.Freeze();
            var typeface = new Typeface(
                new FontFamily(fontFamilyName),
                FontStyles.Normal,
                FontWeights.Bold,
                FontStretches.Normal);

            var formatted = new FormattedText(
                centerText,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                fgBrush,
                1.0);

            var origin = new Point(
                (64.0 - formatted.Width) / 2.0,
                (64.0 - formatted.Height) / 2.0);
            dc.DrawText(formatted, origin);
        }

        var rtb = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    private static bool TryParseColor(
        string input,
        out string displayHex,
        out string normalizedHex,
        out Color color)
    {
        displayHex = string.Empty;
        normalizedHex = string.Empty;
        color = Colors.Transparent;

        var hexMatch = HexColorRegex.Match(input);
        if (hexMatch.Success)
        {
            if (hexMatch.Groups["rgb"].Success)
            {
                var s = hexMatch.Groups["rgb"].Value;
                var expanded = $"#{s[0]}{s[0]}{s[1]}{s[1]}{s[2]}{s[2]}".ToUpperInvariant();
                displayHex = input.ToUpperInvariant();
                normalizedHex = expanded;
                color = (Color)ColorConverter.ConvertFromString(expanded);
                return true;
            }

            var upper = input.ToUpperInvariant();
            displayHex = upper;
            normalizedHex = upper;
            color = (Color)ColorConverter.ConvertFromString(upper);
            return true;
        }

        var rgbMatch = RgbFuncRegex.Match(input);
        if (rgbMatch.Success
            && int.TryParse(rgbMatch.Groups["r"].Value, out var r) && r is >= 0 and <= 255
            && int.TryParse(rgbMatch.Groups["g"].Value, out var g) && g is >= 0 and <= 255
            && int.TryParse(rgbMatch.Groups["b"].Value, out var b) && b is >= 0 and <= 255)
        {
            color = Color.FromRgb((byte)r, (byte)g, (byte)b);
            normalizedHex = $"#{r:X2}{g:X2}{b:X2}";
            displayHex = normalizedHex;
            return true;
        }

        return false;
    }

    private static bool TryParseHttpUrl(string input, out string host, out string shortPath)
    {
        host = string.Empty;
        shortPath = string.Empty;

        if (!input.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
        {
            return false;
        }

        host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? uri.Host[4..]
            : uri.Host;
        var pathAndQuery = uri.PathAndQuery;
        shortPath = string.IsNullOrEmpty(pathAndQuery) || pathAndQuery == "/"
            ? "/"
            : CollapsePreview(pathAndQuery, 28);
        return true;
    }

    private static bool IsCodeSnippet(string input)
    {
        if (input.Contains('{') && input.Contains('}'))
        {
            return true;
        }

        foreach (var kw in CodeKeywords)
        {
            if (input.Contains(kw, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string CollapsePreview(string text, int maxLength)
    {
        var singleLine = Regex.Replace(text, @"\s+", " ").Trim();
        return singleLine.Length <= maxLength
            ? singleLine
            : singleLine[..maxLength].TrimEnd() + "…";
    }
}
