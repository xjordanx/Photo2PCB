namespace Photo2PCB.Core;

/// <summary>
/// User-controllable parameters that drive the photo-to-line-art conversion.
/// All values use the same ranges that the UI sliders expose.
/// </summary>
public sealed class ConversionSettings
{
    /// <summary>Smallest selectable aperture (line width): 0.127 mm (5 mil).</summary>
    public const double MinApertureLimitMm = 0.127;

    /// <summary>Largest selectable aperture (line width): 1.27 mm (50 mil).</summary>
    public const double MaxApertureLimitMm = 1.27;

    /// <summary>Minimum overall image height in millimetres.</summary>
    public const double MinHeightMm = 10.0;

    /// <summary>Maximum overall image height in millimetres.</summary>
    public const double MaxHeightMm = 500.0;

    /// <summary>Fewest horizontal lines used to approximate the image.</summary>
    public const int MinLineCount = 10;

    /// <summary>Absolute ceiling on line count (independent of the height/aperture cap).</summary>
    public const int MaxLineCount = 2000;

    /// <summary>
    /// How far line count may push past the "lines exactly fill the height" point
    /// (LineCount × MaxAperture = Height). 1.0 = lines just touch at their widest; 2.0 =
    /// up to ~2× coverage (heavy overlap) before the cap.
    /// </summary>
    public const double OverlapFactor = 2.0;

    /// <summary>Contrast ratio, 0.0 (flat) .. 1.0 (max). Default 0.5 (neutral).</summary>
    public double Contrast { get; set; } = 0.5;

    /// <summary>Brightness, 0.0 (dark) .. 1.0 (white). Default 0.5 (neutral).</summary>
    public double Brightness { get; set; } = 0.5;

    /// <summary>
    /// Background cutoff, 0.0 .. 1.0. Samples whose ink coverage (darkness) is below this
    /// are dropped to clean white (no stroke). 0 draws even the faintest tone; raising it
    /// removes light background so only the darker subject is traced. Default 0.03.
    /// </summary>
    public double BackgroundCutoff { get; set; } = 0.03;

    /// <summary>Line width used for the lightest strokes, in mm. Default 0.127 mm (5 mil).</summary>
    public double MinApertureMm { get; set; } = 0.127;

    /// <summary>Line width used for the darkest strokes, in mm (heavier traces for dark areas). Default 0.508 mm (20 mil).</summary>
    public double MaxApertureMm { get; set; } = 0.508;

    /// <summary>Overall output image height in millimetres. Width follows the source aspect ratio. Default 100 mm.</summary>
    public double HeightMm { get; set; } = 100.0;

    /// <summary>Number of horizontal lines used to approximate the image. Default 150.</summary>
    public int LineCount { get; set; } = 150;

    /// <summary>
    /// Largest line count allowed for a given height and (maximum) aperture:
    /// <c>floor(OverlapFactor × Height / MaxAperture)</c>, clamped to the absolute range.
    /// Uses the max aperture because that is what fills the vertical pitch in dark areas.
    /// </summary>
    public static int MaxLinesFor(double heightMm, double maxApertureMm)
    {
        double h = Math.Clamp(heightMm, MinHeightMm, MaxHeightMm);
        double a = Math.Clamp(maxApertureMm, MinApertureLimitMm, MaxApertureLimitMm);
        int dynamicMax = (int)Math.Floor(OverlapFactor * h / a);
        return Math.Clamp(dynamicMax, MinLineCount, MaxLineCount);
    }

    /// <summary>Clamp every field to its legal range (Min ≤ Max aperture, height/aperture-dependent line cap).</summary>
    public ConversionSettings Normalized()
    {
        double minAp = Math.Clamp(MinApertureMm, MinApertureLimitMm, MaxApertureLimitMm);
        double maxAp = Math.Clamp(MaxApertureMm, MinApertureLimitMm, MaxApertureLimitMm);
        if (maxAp < minAp) maxAp = minAp;   // max must not fall below min

        double height = Math.Clamp(HeightMm, MinHeightMm, MaxHeightMm);
        int maxLines = MaxLinesFor(height, maxAp);
        return new ConversionSettings
        {
            Contrast = Math.Clamp(Contrast, 0.0, 1.0),
            Brightness = Math.Clamp(Brightness, 0.0, 1.0),
            BackgroundCutoff = Math.Clamp(BackgroundCutoff, 0.0, 1.0),
            MinApertureMm = minAp,
            MaxApertureMm = maxAp,
            HeightMm = height,
            LineCount = Math.Clamp(LineCount, MinLineCount, maxLines),
        };
    }

    public ConversionSettings Clone() => new()
    {
        Contrast = Contrast,
        Brightness = Brightness,
        BackgroundCutoff = BackgroundCutoff,
        MinApertureMm = MinApertureMm,
        MaxApertureMm = MaxApertureMm,
        HeightMm = HeightMm,
        LineCount = LineCount,
    };
}
