namespace ImageCompressorApp.Models;
public class CompressParametersData
{
    public int SelectedQuality { get; init; } = 75;
    public int ResizeWidth { get; init; } = 800;
    public int ResizeHeight { get; init; } = 800;
    public int MinimumSizeToCompressInKb { get; init; } = 400;
    public bool IsDeleteFilesAfterCompress { get; init; } = false;
    public bool IsDeletePreviusResizedImages { get; init; } = false;
}
