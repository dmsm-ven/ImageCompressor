using ImageCompressorApp.Interfaces;
using ImageCompressorApp.Models;
using System.IO;

namespace ImageCompressorApp.Services;


public class ImageProcessorManager
{
    private static readonly object lockObject = new();
    public event Action<string>? OnError;
    public event Func<string, int, bool>? OnLimitExceededResolver;
    public int MinimumFilesCountToResolveRequire { get; set; } = 1000;
    public bool IsDeletePreviuosResizedImages { get; set; } = false;
    public long MinimumImageSizeToResizeInKb { get; set; } = 0;
    public int ThreadsLimit { get; set; } = 1;

    private readonly IImageProcessor processor;
    private readonly List<string> lastResiedImages = new();

    public ImageProcessorManager(IImageProcessor processor)
    {
        this.processor = processor;
    }
    public async Task ResizeImages(string folder, ImageSize size, ResizeModeOptions mode = ResizeModeOptions.Stretch, IProgress<ProgressStatus>? indicator = null)
    {
        if (!IsFilesCountCheckSuccess(folder)) { return; }
        var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
        int total = files.Count();
        int current = 0;

        foreach (var image in files)
        {
            try
            {
                await processor.ResizeImageAsync(image, size, mode);
                lastResiedImages.Add(image);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Error resizing image '{image}': {ex.Message}");
            }

            indicator?.Report(new(++current, total));
        }

        await DeleteLastResizedImagesIfNeed();
    }
    public async Task CompressImages(string folder, long quality = 100, IProgress<ProgressStatus>? indicator = null)
    {
        var files = Directory.GetFiles(folder, "*.jpg", SearchOption.AllDirectories);
        int total = files.Count();
        int current = 0;

        foreach (var image in files)
        {
            FileInfo fi = new(image);
            int fileSizeInKb = (int)(fi.Length / 1024);

            try
            {
                if (fileSizeInKb >= MinimumImageSizeToResizeInKb)
                {
                    await processor.CompressImageAsync(image, quality);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Error compress image '{image}': {ex.Message}");
            }
            indicator?.Report(new(++current, total));
        }
    }
    public async Task ConvertImagesToJpg(string folder, bool deleteOriginal = false, IProgress<ProgressStatus>? indicator = null)
    {
        var allowedExtensions = new[] { ".webp", ".png", ".avif", ".jpeg" };
        var files = Directory.GetFiles(folder)
            .Where(f => allowedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToArray();

        int total = files.Count();
        int current = 0;

        foreach (var image in files)
        {
            if (Path.GetExtension(image).ToLower() == ".jpeg")
            {
                File.Move(image, Path.ChangeExtension(image, ".jpg"));
            }
            else
            {
                try
                {
                    string convertedImage = await processor.ConvertToJpgAsync(image);
                }
                catch (Exception ex)
                {
                    OnError?.Invoke($"Error compress image '{image}': {ex.Message}");
                }
            }

            if (deleteOriginal && File.Exists(image))
            {
                File.Delete(image);
            }

            indicator?.Report(new(++current, total));
        }
    }
    private bool IsFilesCountCheckSuccess(string folder)
    {
        int filesCount = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories).Count();

        if (filesCount > MinimumFilesCountToResolveRequire)
        {
            var res = OnLimitExceededResolver?.Invoke(folder, filesCount) ?? false;
            return res;
        }
        return true;
    }
    private async Task DeleteLastResizedImagesIfNeed()
    {
        if (!IsDeletePreviuosResizedImages || lastResiedImages.Count == 0) { return; }

        foreach (var img in lastResiedImages)
        {
            try
            {
                if (File.Exists(img))
                {
                    File.Delete(img);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Error deleting previous resized image '{img}': {ex.Message}");
            }
        }
    }
}
