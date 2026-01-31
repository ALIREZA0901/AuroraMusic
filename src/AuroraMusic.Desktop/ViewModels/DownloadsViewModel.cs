using System.Collections.ObjectModel;
using AuroraMusic.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuroraMusic.ViewModels;

public partial class DownloadsViewModel : ObservableObject
{
    private readonly DownloadService _svc;
    [ObservableProperty] private ObservableCollection<DownloadItem> items = new();
    public IRelayCommand<DownloadItem> CancelCommand { get; }

    public DownloadsViewModel(DownloadService svc)
    {
        _svc = svc;
        Items = new ObservableCollection<DownloadItem>(_svc.Items);
        CancelCommand = new RelayCommand<DownloadItem>(item =>
        {
            if (item is null) return;
            _svc.Cancel(item.Id);
        });
        _svc.ItemsChanged += () =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Items = new ObservableCollection<DownloadItem>(_svc.Items);
            });
        };
    }
}
