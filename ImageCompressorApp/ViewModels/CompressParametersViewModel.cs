using CommunityToolkit.Mvvm.ComponentModel;

namespace ImageCompressorApp.ViewModels;

public partial class CompressParametersViewodel : ObservableObject
{
    [ObservableProperty]
    public int _watermarkWidth = 220;

    [ObservableProperty]
    public int _watermarkHeight = 50;

    [ObservableProperty]
    public int _selectedQuality = 75;

    [ObservableProperty]
    public int _resizeWidth = 800;

    [ObservableProperty]
    public int _resizeHeight = 800;

    [ObservableProperty]
    public int _minimumSizeToCompressInKb = 400;

    [ObservableProperty]
    public bool isDeleteFilesAfterCompress = false;

    [ObservableProperty]
    public bool isDeletePreviusResizedImages = false;
}
