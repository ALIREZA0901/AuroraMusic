using System.Collections.ObjectModel;
using AuroraMusic.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuroraMusic.ViewModels;

public partial class DownloadsViewModel : ObservableObject
{
    private readonly DownloadService _svc;
    [ObservableProperty] private ObservableCollection<DownloadItem> items = new();

    public DownloadsViewModel(DownloadService svc)
    {
        _svc = svc;
        Items = new ObservableCollection<DownloadItem>(_svc.Items);
        _svc.ItemsChanged += () =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Items = new ObservableCollection<DownloadItem>(_svc.Items);
            });
        };
    }
}
