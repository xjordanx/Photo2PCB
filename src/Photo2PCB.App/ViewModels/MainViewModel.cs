using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Photo2PCB.Core;

namespace Photo2PCB.App.ViewModels;

/// <summary>
/// Backing model for the main window: owns the conversion settings, drives the live
/// preview (debounced + off the UI thread), and exposes the File menu commands. File
/// dialogs and window-close are provided by the view through the delegate hooks so the
/// view-model stays UI-toolkit free and testable.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly DispatcherTimer _debounce;
    private CancellationTokenSource? _renderCts;

    // Hooks wired up by the view.
    public Func<Task<string?>>? OpenFileHook { get; set; }
    public Func<string, Task<string?>>? SaveFileHook { get; set; }
    public Action? CloseHook { get; set; }

    public int MinLineCount { get; } = ConversionSettings.MinLineCount;
    public double MinHeightMm { get; } = ConversionSettings.MinHeightMm;
    public double MaxHeightMm { get; } = ConversionSettings.MaxHeightMm;
    public double ApertureLimitMinMm { get; } = ConversionSettings.MinApertureLimitMm;
    public double ApertureLimitMaxMm { get; } = ConversionSettings.MaxApertureLimitMm;

    /// <summary>Dynamic upper bound on line count for the current height + max aperture.</summary>
    public int MaxLineCount => ConversionSettings.MaxLinesFor(HeightMm, MaxApertureMm);

    [ObservableProperty] private double _contrastPercent = 50;
    [ObservableProperty] private double _brightnessPercent = 50;
    [ObservableProperty] private double _backgroundCutoffPercent = 3;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MinApertureLabel))]
    [NotifyPropertyChangedFor(nameof(SpacingLabel))]
    private double _minApertureMm = 0.127;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaxApertureLabel))]
    [NotifyPropertyChangedFor(nameof(MaxLineCount))]
    [NotifyPropertyChangedFor(nameof(SpacingLabel))]
    private double _maxApertureMm = 0.508;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaxLineCount))]
    [NotifyPropertyChangedFor(nameof(SpacingLabel))]
    private double _heightMm = 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpacingLabel))]
    private int _lineCount = 150;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string? _inputPath;

    [ObservableProperty] private string? _outputPath;
    [ObservableProperty] private Bitmap? _previewImage;
    [ObservableProperty] private string _status = "Open an image with File › Open.";
    [ObservableProperty] private string? _physicalSize;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private bool _isDirty;

    public MainViewModel()
    {
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            _ = UpdatePreviewAsync();
        };
    }

    public bool HasImage => !string.IsNullOrEmpty(InputPath);

    public string WindowTitle => HasImage
        ? $"Photo2PCB — {Path.GetFileName(InputPath)}{(IsDirty ? " *" : "")}"
        : "Photo2PCB";

    public string MinApertureLabel => $"Min aperture (light): {MinApertureMm:0.###} mm  ({MinApertureMm / 0.0254:0} mil)";

    public string MaxApertureLabel => $"Max aperture (dark): {MaxApertureMm:0.###} mm  ({MaxApertureMm / 0.0254:0} mil)";

    /// <summary>Live pitch / gap readout, derived without needing a full render.</summary>
    public string SpacingLabel
    {
        get
        {
            double pitch = HeightMm / Math.Max(1, LineCount);
            double lightGap = pitch - MinApertureMm;
            double darkGap = pitch - MaxApertureMm;
            string dark = darkGap < 0 ? $"dark overlap {-darkGap:0.###} mm" : $"dark gap {darkGap:0.###} mm";
            return $"Pitch {pitch:0.###} mm · light gap {lightGap:0.###} mm · {dark}";
        }
    }

    /// <summary>Apply settings supplied on the command line (if any) before the window shows.</summary>
    public void ApplyStartupOptions(CliOptions? options)
    {
        if (options is null) return;
        ContrastPercent = options.Settings.Contrast * 100.0;
        BrightnessPercent = options.Settings.Brightness * 100.0;
        BackgroundCutoffPercent = options.Settings.BackgroundCutoff * 100.0;
        MinApertureMm = options.Settings.MinApertureMm;
        MaxApertureMm = options.Settings.MaxApertureMm;
        HeightMm = options.Settings.HeightMm;
        LineCount = options.Settings.LineCount;
        OutputPath = options.OutputPath;
        if (!string.IsNullOrWhiteSpace(options.InputPath) && File.Exists(options.InputPath))
            LoadImage(options.InputPath);
    }

    /// <summary>Load a new source image (from Open, drag-and-drop, or startup) and refresh the preview.</summary>
    public void LoadImage(string path)
    {
        if (!File.Exists(path))
        {
            Status = $"File not found: {path}";
            return;
        }
        InputPath = path;
        OutputPath = null;     // new image => first save is always Save As
        IsDirty = true;
        ScheduleRender();
    }

    private ConversionSettings CurrentSettings() => new()
    {
        Contrast = ContrastPercent / 100.0,
        Brightness = BrightnessPercent / 100.0,
        BackgroundCutoff = BackgroundCutoffPercent / 100.0,
        MinApertureMm = MinApertureMm,
        MaxApertureMm = MaxApertureMm,
        HeightMm = HeightMm,
        LineCount = LineCount,
    };

    // Any slider change marks the output stale and reschedules the debounced preview.
    partial void OnContrastPercentChanged(double value) => OnSettingChanged();
    partial void OnBrightnessPercentChanged(double value) => OnSettingChanged();
    partial void OnBackgroundCutoffPercentChanged(double value) => OnSettingChanged();
    partial void OnMinApertureMmChanged(double value)
    {
        if (MaxApertureMm < value) MaxApertureMm = value;   // keep max ≥ min
        OnSettingChanged();
    }
    partial void OnMaxApertureMmChanged(double value)
    {
        if (value < MinApertureMm) { MinApertureMm = value; }  // keep min ≤ max
        ClampLineCountToMax();
        OnSettingChanged();
    }
    partial void OnHeightMmChanged(double value) { ClampLineCountToMax(); OnSettingChanged(); }
    partial void OnLineCountChanged(int value) => OnSettingChanged();

    // Height/max-aperture shrink the allowed line count; pull the current value down if needed.
    private void ClampLineCountToMax()
    {
        if (LineCount > MaxLineCount) LineCount = MaxLineCount;
    }

    private void OnSettingChanged()
    {
        if (HasImage) IsDirty = true;
        ScheduleRender();
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        ContrastPercent = 50;
        BrightnessPercent = 50;
        BackgroundCutoffPercent = 3;
        MinApertureMm = 0.127;
        MaxApertureMm = 0.508;
        HeightMm = 100;
        LineCount = 150;
    }

    private void ScheduleRender()
    {
        if (!HasImage) return;
        _debounce.Stop();
        _debounce.Start();
    }

    private async Task UpdatePreviewAsync()
    {
        if (!HasImage) return;
        string input = InputPath!;
        ConversionSettings settings = CurrentSettings();

        _renderCts?.Cancel();
        var cts = new CancellationTokenSource();
        _renderCts = cts;

        IsBusy = true;
        Status = "Rendering preview…";
        try
        {
            (Bitmap bitmap, string info, string size) = await Task.Run(() =>
            {
                RasterDocument doc = RasterConverter.Convert(input, settings);
                byte[] png = RasterRenderer.RenderPng(doc, maxDimension: 1400);
                using var ms = new MemoryStream(png);
                var bmp = new Bitmap(ms);
                string spacing = doc.DarkLinesOverlap
                    ? $"dark overlap {-doc.DarkGapMm:0.###} mm"
                    : $"dark gap {doc.DarkGapMm:0.###} mm";
                string status =
                    $"{doc.LineCount} lines  |  {doc.RunCount} runs  |  " +
                    $"aperture {doc.MinApertureMm:0.###}–{doc.MaxApertureMm:0.###} mm  |  {spacing}";
                string sizeText =
                    $"{doc.CanvasWidthMm:0.0} × {doc.CanvasHeightMm:0.0} mm  " +
                    $"({doc.CanvasWidthMm / 25.4:0.00} × {doc.CanvasHeightMm / 25.4:0.00} in)";
                return (bmp, status, sizeText);
            }, cts.Token);

            if (cts.Token.IsCancellationRequested) return;
            PreviewImage = bitmap;
            Status = info;
            PhysicalSize = size;
        }
        catch (OperationCanceledException) { /* superseded by a newer render */ }
        catch (Exception ex)
        {
            Status = $"Preview failed: {ex.Message}";
        }
        finally
        {
            if (_renderCts == cts) IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (OpenFileHook is null) return;
        string? path = await OpenFileHook();
        if (string.IsNullOrEmpty(path)) return;
        LoadImage(path);
    }

    [RelayCommand]
    private Task SaveAsync() => SaveCore(forceSaveAs: false);

    [RelayCommand]
    private Task SaveAsAsync() => SaveCore(forceSaveAs: true);

    private async Task SaveCore(bool forceSaveAs)
    {
        if (!HasImage)
        {
            Status = "Open an image first.";
            return;
        }

        // First save of a new output is always Save As (never silently overwrites).
        string? target = OutputPath;
        if (forceSaveAs || string.IsNullOrEmpty(target))
        {
            if (SaveFileHook is null) return;
            string suggested = Path.GetFileNameWithoutExtension(InputPath!) + ".svg";
            target = await SaveFileHook(suggested);
            if (string.IsNullOrEmpty(target)) return;   // user cancelled
        }

        try
        {
            RasterDocument doc = RasterConverter.Convert(InputPath!, CurrentSettings());
            SvgWriter.WriteToFile(doc, target);
            OutputPath = target;
            IsDirty = false;
            Status = $"Saved {Path.GetFileName(target)} ({doc.RunCount} runs).";
        }
        catch (Exception ex)
        {
            Status = $"Save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Exit() => CloseHook?.Invoke();
}
