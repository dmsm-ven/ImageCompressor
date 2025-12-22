using ImageCompressorApp.Interfaces;
using ImageCompressorApp.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System.IO;

namespace ImageCompressorApp.Services;

public class ImageSharpProcessor : IImageProcessor
{
    public async Task CompressImageAsync(string filePath, long qualityLevel)
    {
        ThrowExceptionIfNonJpg(filePath);

        try
        {
            using Image image = Image.Load(filePath);
            await image.SaveAsync(filePath, GetJpegEncoder(qualityLevel));
        }
        catch
        {
            throw;
        }
    }
    public async Task<string> ConvertToJpgAsync(string filePath)
    {
        string outputPath = Path.Combine(Path.GetDirectoryName(filePath) ?? "", Path.GetFileNameWithoutExtension(filePath)) + ".jpg";

        if (File.Exists(outputPath))
        {
            throw new Exception($"File {filePath} already exists");
        }

        try
        {
            using Image image = Image.Load(filePath);
            image.Mutate(x =>
            {
                x.BackgroundColor(Color.White);
            });
            await image.SaveAsync(outputPath, GetJpegEncoder());
            return outputPath;
        }
        catch
        {
            throw;
        }
    }
    public async Task ResizeImageAsync(string filePath, ImageSize size, ResizeModeOptions resizeMode)
    {
        ThrowExceptionIfNonJpg(filePath);

        try
        {
            using Image image = Image.Load(filePath);

            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(size.Width, size.Height),
                Mode = resizeMode switch
                {
                    ResizeModeOptions.Stretch => SixLabors.ImageSharp.Processing.ResizeMode.Stretch,
                    ResizeModeOptions.BoxPad => SixLabors.ImageSharp.Processing.ResizeMode.BoxPad,
                    _ => SixLabors.ImageSharp.Processing.ResizeMode.Max
                },
                Sampler = KnownResamplers.Lanczos3
            }));

            await image.SaveAsync(filePath, GetJpegEncoder());
        }
        catch
        {
            throw;
        }
    }
    private void ThrowExceptionIfNonJpg(string filePath)
    {
        if (Path.GetExtension(filePath) != ".jpg")
        {
            throw new InvalidDataException("Only JPG images are supported for compression.");
        }
    }
    private JpegEncoder GetJpegEncoder(long qualityLevel = 90) => new JpegEncoder { Quality = (int)qualityLevel };
}