using ImageCompressorApp.Interfaces;
using ImageCompressorApp.Models;
using System.Collections.Concurrent;
using System.IO;

namespace ImageCompressorApp.Services;


public class ImageProcessorManager
{
    public event Action<string>? OnError;
    public event Func<string, int, bool>? OnLimitExceededResolver;
    public TimeSpan MULTITHREAD_REPORT_DELAY { get; } = TimeSpan.FromMilliseconds(25);
    public int MinimumFilesCountToResolveRequire { get; set; } = 1000;
    public bool IsDeletePreviuosResizedImages { get; set; } = false;
    public long MinimumImageSizeToResizeInKb { get; set; } = 0;
    public int ThreadsLimit { get; set; } = 1;

    private int currentOperationProgress = 0;
    private int currentOperationTotal = 0;
    private IProgress<ProgressStatus>? currentOperationIndicator = null;

    private readonly IImageProcessor processor;
    private readonly ConcurrentBag<string> lastResiedImages = new();
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
        currentOperationTotal = files.Count();
        currentOperationProgress = 0;
        currentOperationIndicator = indicator;

        if (currentOperationTotal == 0) { return; }

        var tasks = files.Select(image => ResizeSingleImageAsync(image, size, mode));

        await Task.WhenAll(tasks.ToArray());

        await DeleteLastResizedImagesIfNeed();
    }
    public async Task CompressImages(string folder, long quality = 100, IProgress<ProgressStatus>? indicator = null)
    {
        InitializeSemaphore();

        var files = Directory.GetFiles(folder, "*.jpg", SearchOption.AllDirectories);
        currentOperationTotal = files.Count();
        currentOperationProgress = 0;
        currentOperationIndicator = indicator;

        if (currentOperationTotal == 0) { return; }

        var tasks = files.Select(image => CompressSingleImageAsync(image, quality));

        await Task.WhenAll(tasks.ToArray());
    }
    public async Task ConvertImagesToJpg(string folder, bool deleteOriginal = false, IProgress<ProgressStatus>? indicator = null)
    {
        InitializeSemaphore();

        var allowedExtensions = new[] { ".webp", ".png", ".avif", ".jpeg" };
        var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
            .Where(f => allowedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToArray();

        currentOperationTotal = files.Count();
        currentOperationProgress = 0;
        currentOperationIndicator = indicator;

        if (currentOperationTotal == 0) { return; }

        var tasks = files.Select(image => ConvertSingleImageToJpg(image, deleteOriginal));

        await Task.WhenAll(tasks.ToArray());
    }
    private async Task ResizeSingleImageAsync(string image, ImageSize size, ResizeModeOptions mode)
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

        await IncrementIndicator();

        semaphore.Release();
    }
    private async Task CompressSingleImageAsync(string image, long quality)
    {
        await semaphore.WaitAsync();

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

        await IncrementIndicator();

        semaphore.Release();
    }
    private async Task ConvertSingleImageToJpg(string image, bool deleteOriginal)
    {
        await semaphore.WaitAsync();

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

        await IncrementIndicator();

        semaphore.Release();
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

        lastResiedImages.Clear();
    }
    private void InitializeSemaphore()
    {
        semaphore?.Dispose();
        semaphore = new SemaphoreSlim(ThreadsLimit, ThreadsLimit);
    }
    private async Task IncrementIndicator()
    {
        await Task.Delay(MULTITHREAD_REPORT_DELAY);
        int current = Interlocked.Increment(ref currentOperationProgress);
        currentOperationIndicator?.Report(new(current, currentOperationTotal));
    }
}
