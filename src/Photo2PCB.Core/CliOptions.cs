using System.Globalization;

namespace Photo2PCB.Core;

/// <summary>
/// Parsed command-line options. Kept in Core (rather than the App) so the parser is
/// unit-testable and reusable. Switches:
///   -i, --input  &lt;file&gt;        input PNG/JPG
///   -o, --output &lt;file&gt;        output SVG
///       --contrast   &lt;0..100&gt;  contrast ratio (percent)
///       --brightness &lt;0..100&gt;  brightness (percent)
///       --cutoff     &lt;0..100&gt;  background cutoff percent (drop strokes lighter than this)
///       --min-aperture &lt;mm&gt;    line width for light strokes, mm (0.127..1.27)
///       --max-aperture &lt;mm&gt;    line width for dark strokes, mm (≥ min)
///       --height     &lt;mm&gt;      overall image height in mm (10..500)
///       --lines      &lt;n&gt;       number of horizontal lines (capped by height/max-aperture)
///   -n, --no-gui              non-interactive: convert and exit, no dialog
///   -h, --help                show usage
/// </summary>
public sealed class CliOptions
{
    public string? InputPath { get; private set; }
    public string? OutputPath { get; private set; }
    public bool NonInteractive { get; private set; }
    public bool ShowHelp { get; private set; }
    public ConversionSettings Settings { get; } = new();

    public static string Usage =>
        """
        Photo2PCB - convert a photo into a horizontally-rasterized black-on-white SVG for PCB etching.

        Usage:
          Photo2PCB [options]

        Options:
          -i, --input <file>       Input image (.png or .jpg)
          -o, --output <file>      Output SVG file
              --contrast <0-100>   Contrast ratio percent (default 50)
              --brightness <0-100> Brightness percent, 0=dark 100=white (default 50)
              --cutoff <0-100>     Background cutoff percent; drop strokes lighter than this (default 3)
              --min-aperture <mm>  Line width for light strokes, mm, 0.127-1.27 (default 0.127)
              --max-aperture <mm>  Line width for dark strokes, mm, >= min (default 0.508)
              --height <mm>        Overall image height in mm, 10-500 (default 100)
              --lines <n>          Number of horizontal lines (default 150; capped by height/max-aperture)
          -n, --no-gui             Non-interactive: generate output and exit (requires -i and -o)
          -h, --help               Show this help

        With no arguments the GUI opens with an empty preview, ready for File > Open.
        """;

    /// <summary>Parse argv. Throws <see cref="ArgumentException"/> on malformed input.</summary>
    public static CliOptions Parse(string[] args)
    {
        var o = new CliOptions();
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            switch (a)
            {
                case "-h" or "--help":
                    o.ShowHelp = true;
                    break;
                case "-n" or "--no-gui":
                    o.NonInteractive = true;
                    break;
                case "-i" or "--input":
                    o.InputPath = Next(args, ref i, a);
                    break;
                case "-o" or "--output":
                    o.OutputPath = Next(args, ref i, a);
                    break;
                case "--contrast":
                    o.Settings.Contrast = Percent(Next(args, ref i, a), a);
                    break;
                case "--brightness":
                    o.Settings.Brightness = Percent(Next(args, ref i, a), a);
                    break;
                case "--cutoff":
                    o.Settings.BackgroundCutoff = Percent(Next(args, ref i, a), a);
                    break;
                case "--min-aperture":
                    o.Settings.MinApertureMm = Number(Next(args, ref i, a), a);
                    break;
                case "--max-aperture":
                    o.Settings.MaxApertureMm = Number(Next(args, ref i, a), a);
                    break;
                case "--height":
                    o.Settings.HeightMm = Number(Next(args, ref i, a), a);
                    break;
                case "--lines":
                    o.Settings.LineCount = (int)Number(Next(args, ref i, a), a);
                    break;
                default:
                    // Bare first token is treated as the input file for convenience.
                    if (!a.StartsWith('-') && o.InputPath is null)
                        o.InputPath = a;
                    else
                        throw new ArgumentException($"Unknown option: {a}");
                    break;
            }
        }
        return o;
    }

    private static string Next(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length)
            throw new ArgumentException($"Option {flag} requires a value.");
        return args[++i];
    }

    private static double Percent(string s, string flag)
    {
        double v = Number(s, flag);
        return Math.Clamp(v / 100.0, 0.0, 1.0);
    }

    private static double Number(string s, string flag)
    {
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            throw new ArgumentException($"Option {flag} expects a number, got '{s}'.");
        return v;
    }
}
