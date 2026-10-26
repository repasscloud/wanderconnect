namespace Swoovr.Client;

/// <summary>
/// One stroke or fill of a badge: SVG path data, the part it plays ("line", "disc", "tick" or "bird") and an optional
/// translate-then-scale applied before the view box.
/// </summary>
public sealed record BadgeStroke(string Part, string Data, double TranslateX = 0, double TranslateY = 0, double Scale = 1);

/// <summary>A badge's shape: its view box (min x, min y, width, height) and strokes, drawn in order.</summary>
public sealed record BadgeShape(double MinX, double MinY, double Width, double Height, IReadOnlyList<BadgeStroke> Strokes);

/// <summary>
/// The badge artwork shared by the web (Badge.razor) and the app (D141: same shapes, colours and meanings everywhere,
/// UI-STYLE.md Badges). Colours stay in the generated tokens (--badge-* on the web, Badge* in Colors.xaml); this holds
/// only the shapes, as path data both SVG and MAUI's path markup read. Photo: portrait frame; ID: card; both carry a
/// filled check disc. Founding, staff and bot reuse the Swoovr bird.
/// </summary>
public static class BadgeArtwork
{
    public static readonly IReadOnlyList<string> Kinds = ["photo", "id", "founding", "staff", "bot"];

    private static readonly BadgeShape Bird = new(140, 30, 420, 356,
    [
        new("bird", "M139 0C153.755264 7.892353 168.569412 15.313709 182.947464 22.516602C252.37085 57.295238 311.627411 86.980667 305 140C304.202057 150.107193 301.06955 159.719162 296.279846 168.835938C297.106506 168.971268 298.010498 169.491089 298.900879 170.167953C327.374664 144.083389 345.228699 148.062378 367 154C373 156 378 156 384 156C355 164 350 185 339 207C310 270 226 282 97 324C153.7 289.85 197.8 257.1 233.4 227.7C236.604 225.054 238.45 222.35 239.85 218.65C260.85 163.15 218.0 80.86 139 0ZM77 137C120 159 205 163 226 192C259.219879 239.585236 132.02446 263.793365 32.588951 282.71817C21.274101 284.871643 10.3187 286.956696 0 289C79 260 166 226 180 207C188.996353 193.205597 164.180161 181.929153 134.46019 168.424454C114.626785 159.41217 92.609497 149.407547 77 137Z",
            145, 35.03125, 1.067708),
    ]);

    public static BadgeShape Shape(string kind) => kind switch
    {
        "photo" => new(0, 0, 24, 24,
        [
            new("line", "M5.5 2.5H14.5A3 3 0 0 1 17.5 5.5V14.5A3 3 0 0 1 14.5 17.5H5.5A3 3 0 0 1 2.5 14.5V5.5A3 3 0 0 1 5.5 2.5Z"),
            new("line", "M7.4 8.4A2.6 2.6 0 1 0 12.6 8.4A2.6 2.6 0 1 0 7.4 8.4Z"),
            new("line", "M5.4 16.2C6.3 13.9 8 12.7 10 12.7C12 12.7 13.4 13.6 14.3 15.3"),
            new("disc", "M13 18A5 5 0 1 0 23 18A5 5 0 1 0 13 18Z"),
            new("tick", "M15.6 18.1L17.2 19.7L20.3 16.4"),
        ]),
        "id" => new(0, 0, 24, 24,
        [
            new("line", "M4 4.5H17A2.5 2.5 0 0 1 19.5 7V16A2.5 2.5 0 0 1 17 18.5H4A2.5 2.5 0 0 1 1.5 16V7A2.5 2.5 0 0 1 4 4.5Z"),
            new("line", "M5.1 10A1.9 1.9 0 1 0 8.9 10A1.9 1.9 0 1 0 5.1 10Z"),
            new("line", "M4.4 15.4C4.9 13.9 5.8 13.2 7 13.2C8.2 13.2 9.1 13.9 9.6 15.4M12.5 9.5H17M12.5 12.5H15"),
            new("disc", "M13.7 18A4.8 4.8 0 1 0 23.3 18A4.8 4.8 0 1 0 13.7 18Z"),
            new("tick", "M16.2 18.1L17.7 19.6L20.7 16.5"),
        ]),
        "founding" or "staff" or "bot" => Bird,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown badge"),
    };

    /// <summary>The colour token's suffix for a kind: --badge-{token} on the web, Badge{Token} in Colors.xaml.</summary>
    public static string ColourToken(string kind) => kind switch
    {
        "photo" => "photoVerified", "id" => "idVerified", "founding" => "founding", "staff" => "staff", "bot" => "bot",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown badge"),
    };

    /// <summary>The words for a kind, the same on the web and in the app.</summary>
    public static string Label(string kind) => kind switch
    {
        "photo" => "Photo verified", "id" => "ID verified", "founding" => "Founding member", "staff" => "Swoovr staff", "bot" => "Swoovr guide (bot)",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown badge"),
    };

    /// <summary>
    /// The path data mapped by x' = k·x + dx, y' = k·y + dy, for clients that draw untransformed paths (the app: MAUI clips
    /// a transformed path to its untransformed bounds). Reads only the absolute commands the artwork uses (M L H V C A Z).
    /// </summary>
    public static string Transform(string data, double k, double dx, double dy)
    {
        var tokens = System.Text.RegularExpressions.Regex.Matches(data, @"[A-Za-z]|-?(?:\d+\.?\d*|\.\d+)(?:[eE]-?\d+)?").Select(m => m.Value).ToList();
        var output = new List<string>();
        var command = ' ';
        string N(double value) => Math.Round(value, 4).ToString(System.Globalization.CultureInfo.InvariantCulture);
        double Next(ref int i) => double.Parse(tokens[i++], System.Globalization.CultureInfo.InvariantCulture);
        for (var i = 0; i < tokens.Count;)
        {
            if (char.IsLetter(tokens[i][0])) { command = tokens[i++][0]; output.Add(command.ToString()); if (command == 'Z') continue; }
            switch (command)
            {
                case 'M' or 'L': output.Add(N(k * Next(ref i) + dx)); output.Add(N(k * Next(ref i) + dy)); break;
                case 'H': output.Add(N(k * Next(ref i) + dx)); break;
                case 'V': output.Add(N(k * Next(ref i) + dy)); break;
                case 'C': for (var p = 0; p < 3; p++) { output.Add(N(k * Next(ref i) + dx)); output.Add(N(k * Next(ref i) + dy)); } break;
                case 'A':
                    output.Add(N(k * Next(ref i))); output.Add(N(k * Next(ref i))); output.Add(N(Next(ref i)));
                    output.Add(N(Next(ref i))); output.Add(N(Next(ref i)));
                    output.Add(N(k * Next(ref i) + dx)); output.Add(N(k * Next(ref i) + dy)); break;
                default: throw new FormatException("Badge path data must use absolute M, L, H, V, C, A and Z only.");
            }
        }
        return string.Join(' ', output);
    }

    /// <summary>A stroke's data placed in a square of the given size: its own transform, then the view box, centred.</summary>
    public static string Placed(BadgeShape shape, BadgeStroke stroke, double size)
    {
        var scale = size / Math.Max(shape.Width, shape.Height);
        var (offsetX, offsetY) = ((size - shape.Width * scale) / 2, (size - shape.Height * scale) / 2);
        return Transform(stroke.Data, scale * stroke.Scale, scale * (stroke.TranslateX - shape.MinX) + offsetX, scale * (stroke.TranslateY - shape.MinY) + offsetY);
    }

    /// <summary>The SVG view box attribute for a shape.</summary>
    public static string ViewBox(BadgeShape shape) => FormattableString.Invariant($"{shape.MinX} {shape.MinY} {shape.Width} {shape.Height}");

    /// <summary>The SVG transform attribute for a stroke, or null.</summary>
    public static string? SvgTransform(BadgeStroke stroke) => stroke is { TranslateX: 0, TranslateY: 0, Scale: 1 } ? null
        : FormattableString.Invariant($"translate({stroke.TranslateX} {stroke.TranslateY}) scale({stroke.Scale})");
}
