using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRCoder;

using System.IO;
namespace AuroraMusic.ViewModels;

public partial class DonateViewModel : ObservableObject
{
    public string Wallet { get; } = "TPHoKQ5D5YdosEWLwesj8bQ1aYWwd4z95s";
    [ObservableProperty] private ImageSource? qrImage;

    public IRelayCommand CopyCommand { get; }

    public DonateViewModel()
    {
        CopyCommand = new RelayCommand(() => Clipboard.SetText(Wallet));
        QrImage = GenerateQr(Wallet);
    }

    private static ImageSource GenerateQr(string text)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var qr = new PngByteQRCode(data);
        var bytes = qr.GetGraphic(20);

        var img = new BitmapImage();
        img.BeginInit();
        img.StreamSource = new MemoryStream(bytes);
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.EndInit();
        img.Freeze();
        return img;
    }
}
