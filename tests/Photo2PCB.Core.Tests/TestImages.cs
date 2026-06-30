namespace Photo2PCB.Core.Tests;

/// <summary>
/// Locates the repository's "Test Files" directory at runtime by walking up from the
/// test assembly location. The sample photos are personal and are NOT committed to the
/// repository, so when the folder (or any image in it) is absent the image-driven tests
/// skip gracefully rather than fail. Drop PNG/JPG files into a "Test Files" folder at
/// the repo root to exercise them locally.
/// </summary>
public static class TestImages
{
    public static string? Directory { get; } = Locate();

    public static IEnumerable<string> All =>
        Directory is null
            ? Enumerable.Empty<string>()
            : System.IO.Directory.EnumerateFiles(Directory)
                .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f);

    /// <summary>True when at least one sample image is available to test against.</summary>
    public static bool Available => All.Any();

    /// <summary>
    /// xUnit MemberData source: each test image as a single-element row. When no images
    /// are present a single sentinel <c>null</c> row is emitted so the theory still runs
    /// once and can skip (an empty data set would otherwise be reported as a failure).
    /// </summary>
    public static IEnumerable<object?[]> AllAsMemberData =>
        Available ? All.Select(p => new object?[] { p }) : new[] { new object?[] { null } };

    private static string? Locate()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "Test Files");
            if (System.IO.Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
