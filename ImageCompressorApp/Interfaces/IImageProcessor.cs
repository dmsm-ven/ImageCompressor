using ImageCompressorApp.Models;

namespace ImageCompressorApp.Interfaces;

public interface IImageProcessor
{
    Task CompressImageAsync(string filePath, long qualityLevel);
    Task ResizeImageAsync(string filePath, ImageSize size, ResizeModeOptions resizeMode);
    Task<string> ConvertToJpgAsync(string filePath);
}
