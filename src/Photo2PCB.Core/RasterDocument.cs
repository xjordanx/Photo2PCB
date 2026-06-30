namespace Photo2PCB.Core;

/// <summary>
/// A horizontal "pen down" run within one scan line, drawn as a round-capped stroke of
/// <see cref="WidthMm"/> (the local aperture). Coordinates are in millimetres and refer
/// to the centre of the aperture at the start/end of the run.
/// </summary>
public readonly record struct PenRun(double XStartMm, double XEndMm, double WidthMm);

/// <summary>One horizontal scan line at a fixed Y (mm), containing zero or more pen runs.</summary>
public sealed class ScanLine
{
    public double YMm { get; init; }
    public IReadOnlyList<PenRun> Runs { get; init; } = Array.Empty<PenRun>();
}

/// <summary>
/// The geometric, device-independent result of converting an image: the physical
/// canvas size, the aperture range used, and every scan line / pen run, all in
/// millimetres. Consumed by <see cref="SvgWriter"/> and <see cref="RasterRenderer"/>.
/// </summary>
public sealed class RasterDocument
{
    public required double CanvasWidthMm { get; init; }
    public required double CanvasHeightMm { get; init; }

    /// <summary>Line width used for the lightest strokes.</summary>
    public required double MinApertureMm { get; init; }

    /// <summary>Line width used for the darkest strokes.</summary>
    public required double MaxApertureMm { get; init; }

    /// <summary>Number of horizontal lines (= <see cref="Rows"/>) used to approximate the image.</summary>
    public required int LineCount { get; init; }

    /// <summary>Vertical centre-to-centre distance between lines, in mm (Height / LineCount).</summary>
    public required double LinePitchMm { get; init; }

    /// <summary>Edge-to-edge spacing between adjacent lines where strokes are at their lightest (Pitch − MinAperture).</summary>
    public double LightGapMm => LinePitchMm - MinApertureMm;

    /// <summary>Edge-to-edge spacing between adjacent lines where strokes are at their heaviest (Pitch − MaxAperture). Negative ⇒ overlap.</summary>
    public double DarkGapMm => LinePitchMm - MaxApertureMm;

    /// <summary>True when the heaviest (dark) strokes overlap their neighbours.</summary>
    public bool DarkLinesOverlap => DarkGapMm < 0;

    /// <summary>Halftone cell grid width (flash columns).</summary>
    public required int Columns { get; init; }

    /// <summary>Halftone cell grid height = number of horizontal lines.</summary>
    public required int Rows { get; init; }

    /// <summary>Original source image dimensions, in pixels.</summary>
    public int SourceWidthPx { get; init; }
    public int SourceHeightPx { get; init; }

    public required IReadOnlyList<ScanLine> ScanLines { get; init; }

    /// <summary>Total number of pen runs across all scan lines.</summary>
    public int RunCount => ScanLines.Sum(s => s.Runs.Count);
}
