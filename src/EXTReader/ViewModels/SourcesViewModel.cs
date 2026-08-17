using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EXTReader.Models;
using EXTReader.Services;
using Microsoft.Win32;

namespace EXTReader.ViewModels;

public partial class SourcesViewModel : ObservableObject
{
    private readonly DriveDiscoveryService _driveDiscovery = new();
    private readonly ImageFileService _imageFileService = new();

    [ObservableProperty]
    private bool _isElevated;

    [ObservableProperty]
    private string _statusMessage = "Click Refresh to scan for EXT sources.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseCommand))]
    private bool _isScanning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BrowseCommand))]
    private ExtSource? _selectedSource;

    public ObservableCollection<ExtSource> Sources { get; } = new();

    public event Action<ExtSource>? BrowseRequested;

    public SourcesViewModel()
    {
        IsElevated = AdminRightsService.IsElevated;
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task RefreshAsync()
    {
        IsScanning = true;
        StatusMessage = "Scanning physical drives…";
        Sources.Clear();

        try
        {
            var drives = await Task.Run(() => _driveDiscovery.DiscoverDrives());
            int extCount = 0;

            foreach (var drive in drives)
            {
                foreach (var partition in drive.Partitions)
                {
                    if (!partition.IsExt) continue;

                    Sources.Add(new ExtSource
                    {
                        DisplayName = $"{drive.Model} — Partition {partition.Index}",
                        Type = SourceType.PhysicalDisk,
                        BackingPath = drive.DevicePath,
                        Offset = partition.StartOffset,
                        Size = partition.Size,
                        FileSystem = partition.FileSystem,
                    });
                    extCount++;
                }
            }

            StatusMessage = drives.Count == 0
                ? IsElevated
                    ? "No physical drives found."
                    : "No drives accessible. Try restarting as Administrator."
                : $"Found {extCount} EXT partition(s) on {drives.Count} drive(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task OpenImageAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files|*.img;*.dd;*.vhd;*.raw;*.iso|All files|*.*",
            Title = "Select an EXT disk or filesystem image",
        };

        if (dialog.ShowDialog() != true) return;

        IsScanning = true;
        StatusMessage = $"Opening {Path.GetFileName(dialog.FileName)}…";

        try
        {
            var sources = await Task.Run(() => _imageFileService.OpenImage(dialog.FileName));

            foreach (var source in sources)
                Sources.Add(source);

            StatusMessage = sources.Count > 0
                ? $"Added {sources.Count} EXT source(s) from image."
                : "No EXT filesystems found in image.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void RestartElevated()
    {
        AdminRightsService.RestartElevated();
    }

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private void Browse()
    {
        if (SelectedSource != null)
            BrowseRequested?.Invoke(SelectedSource);
    }

    private bool CanBrowse() => SelectedSource != null;
    private bool CanScan() => !IsScanning;
}
