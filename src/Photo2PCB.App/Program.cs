using Avalonia;
using Photo2PCB.App.ViewModels;
using Photo2PCB.Core;

namespace Photo2PCB.App;

internal static class Program
{
    /// <summary>Initial options handed to the GUI when it launches interactively.</summary>
    public static CliOptions? StartupOptions { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        CliOptions options;
        try
        {
            options = CliOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine(CliOptions.Usage);
            return 2;
        }

        if (options.ShowHelp)
        {
            Console.WriteLine(CliOptions.Usage);
            return 0;
        }

        if (options.NonInteractive)
            return RunHeadless(options);

        // Interactive: launch the Avalonia GUI, seeded with any CLI-provided settings.
        StartupOptions = options;
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static int RunHeadless(CliOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.InputPath))
        {
            Console.Error.WriteLine("Error: --no-gui requires an input file (-i).");
            return 2;
        }
        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            Console.Error.WriteLine("Error: --no-gui requires an output file (-o).");
            return 2;
        }
        if (!File.Exists(options.InputPath))
        {
            Console.Error.WriteLine($"Error: input file not found: {options.InputPath}");
            return 1;
        }

        try
        {
            RasterDocument doc = RasterConverter.Convert(options.InputPath, options.Settings);
            SvgWriter.WriteToFile(doc, options.OutputPath);
            string darkGapText = doc.DarkLinesOverlap
                ? $"dark overlap {-doc.DarkGapMm:0.###} mm"
                : $"dark gap {doc.DarkGapMm:0.###} mm";
            Console.WriteLine(
                $"Wrote {options.OutputPath}: {doc.CanvasWidthMm:0.##} x {doc.CanvasHeightMm:0.##} mm " +
                $"({doc.CanvasWidthMm / 25.4:0.00} x {doc.CanvasHeightMm / 25.4:0.00} in), " +
                $"{doc.LineCount} lines, pitch {doc.LinePitchMm:0.###} mm, {darkGapText}, " +
                $"aperture {doc.MinApertureMm:0.###}–{doc.MaxApertureMm:0.###} mm, {doc.RunCount} runs.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    // Avalonia configuration, referenced by the visual designer and Main.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
