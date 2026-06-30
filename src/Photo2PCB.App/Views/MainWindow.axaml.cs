using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Photo2PCB.App.ViewModels;

namespace Photo2PCB.App.Views;

public partial class MainWindow : Window
{
    private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg" };

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => WireUp();
        WireUp();

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        string? path = e.Data.GetFiles()?
            .Select(f => f.TryGetLocalPath())
            .FirstOrDefault(p => p is not null &&
                SupportedExtensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase));

        if (path is not null)
            vm.LoadImage(path);
    }

    private void WireUp()
    {
        if (DataContext is not MainViewModel vm) return;
        vm.OpenFileHook = OpenImageAsync;
        vm.SaveFileHook = SaveSvgAsync;
        vm.CloseHook = Close;
    }

    private async Task<string?> OpenImageAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open image",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images (*.png, *.jpg)")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg" },
                },
            },
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    private async Task<string?> SaveSvgAsync(string suggestedName)
    {
        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save SVG",
            SuggestedFileName = suggestedName,
            DefaultExtension = "svg",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("SVG (*.svg)") { Patterns = new[] { "*.svg" } },
            },
        });

        return file?.TryGetLocalPath();
    }
}
