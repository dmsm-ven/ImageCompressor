using ImageCompressorApp.Interfaces;
using ImageCompressorApp.Models;
using System.IO;

namespace ImageCompressorApp.Services;


public class ImageProcessorManager
{
    public event Action<string>? OnError;
    public event Func<string, int, bool>? OnLimitExceededResolver;
    public int MinimumFilesCountToResolveRequire { get; set; } = 1000;
    public bool IsDeletePreviuosResizedImages { get; set; } = false;
    public long MinimumImageSizeToResizeInKb { get; set; } = 0;
    public int ThreadsLimit { get; set; } = 1;

    private readonly IImageProcessor processor;
    private readonly List<string> lastResiedImages = new();
    private SemaphoreSlim semaphore;

    public ImageProcessorManager(IImageProcessor processor)
    {
        this.processor = processor;
    }
    public async Task ResizeImages(string folder, ImageSize size, ResizeModeOptions mode = ResizeModeOptions.Stretch, IProgress<ProgressStatus>? indicator = null)
    {
        if (!IsFilesCountCheckSuccess(folder)) { return; }
        InitializeSemaphore();

        var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
        int total = files.Count();
        int currentOperationProgress = 0;

        if (total == 0) { return; }

        var tasks = files
            .Select(image => ResizeSingleImageAsync(size, mode, image).ContinueWith((t) =>
            {
                int current = Interlocked.Increment(ref currentOperationProgress);
                indicator?.Report(new(current, total));
            }));

        await Task.WhenAll(tasks.ToArray());

        await DeleteLastResizedImagesIfNeed();
    }
    public async Task CompressImages(string folder, long quality = 100, IProgress<ProgressStatus>? indicator = null)
    {
        InitializeSemaphore();

        var files = Directory.GetFiles(folder, "*.jpg", SearchOption.AllDirectories);
        int total = files.Count();
        int current = 0;

        if (total == 0) { return; }

        var tasks = files
            .Select(image => CompressSingleImageAsync(quality, image).ContinueWith((t) =>
            {
                int curr = Interlocked.Increment(ref current);
                indicator?.Report(new(curr, total));
            }));

        await Task.WhenAll(tasks.ToArray());
    }
    public async Task ConvertImagesToJpg(string folder, bool deleteOriginal = false, IProgress<ProgressStatus>? indicator = null)
    {
        InitializeSemaphore();

        var allowedExtensions = new[] { ".webp", ".png", ".avif", ".jpeg" };
        var files = Directory.GetFiles(folder)
            .Where(f => allowedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToArray();

        int total = files.Count();
        int current = 0;

        if (total == 0) { return; }

        var tasks = files
            .Select(image => ConvertSingleImageToJpg(deleteOriginal, image).ContinueWith((t) =>
            {
                int curr = Interlocked.Increment(ref current);
                indicator?.Report(new(curr, total));
            }));

        await Task.WhenAll(tasks.ToArray());
    }
    private async Task ResizeSingleImageAsync(ImageSize size, ResizeModeOptions mode, string image)
    {
        await semaphore.WaitAsync();

        try
        {
            await processor.ResizeImageAsync(image, size, mode);
            lastResiedImages.Add(image);
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Error resizing image '{image}': {ex.Message}");
        }

        semaphore.Release();
    }
    private async Task CompressSingleImageAsync(long quality, string image)
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
    }
    private async Task ConvertSingleImageToJpg(bool deleteOriginal, string image)
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
    private void InitializeSemaphore()
    {
        semaphore?.Dispose();
        semaphore = new SemaphoreSlim(ThreadsLimit, ThreadsLimit);
    }
}
