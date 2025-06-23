using ImageCompressorApp.Models;
using ImageProcessor;
using ImageProcessor.Imaging;
using ImageProcessor.Imaging.Formats;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;
using Encoder = System.Drawing.Imaging.Encoder;

namespace ImageCompressorApp.Services;

public interface IImageProcessor
{
    event Action<string> OnError;
    event Func<string, int, bool> OnLimitWarning;

    void CompressImage(string imageFilePath, long qualityLevel);
    Task CompressImages(string workingFolder, long qualityLevel, int minimumSizeInKb, IProgress<CompressProgressStatus>? indicator = null);
    Task<int> DeletePreviusResizedImages();
    Task ResizeImages(string workingFolder, ImageSize newSize, ResizeMode resizeMode, IProgress<CompressProgressStatus>? indicator = null, int threads = 1);
    Task SaveAllAsJpg(string workingFolder, bool removeOriginalFiles, IProgress<CompressProgressStatus>? indicator = null);
}

public class ImageMultiCompressor : IImageProcessor
{
    public const int WARNING_FILES_MIN_COUNT = 1000;

    private static readonly object lockObject = new();

    private readonly List<string> lastResiedImages = new();

    public event Action<string> OnError;
    public event Func<string, int, bool> OnLimitWarning;

    #region Compress
    public async Task CompressImages(string workingFolder, long qualityLevel, int minimumSizeInKb, IProgress<CompressProgressStatus> indicator)
    {
        string[] valid_extensions = new string[2] { ".jpg", ".jpeg" };

        var images = Directory.GetFiles(workingFolder)
            .Select(file => new FileInfo(file))
            .Where(file => valid_extensions.Contains(file.Extension))
            .Where(file => file.Length >= minimumSizeInKb * 1000)
            .Select(fi => fi.FullName)
            .ToArray();

        int total = images.Count();
        int current = 0;

        if (!IsFilesCountCheckSuccess(workingFolder, total))
        {
            return;
        }

        foreach (var image in images)
        {
            try
            {
                await Task.Run(() => CompressImage(image, qualityLevel)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex.Message + $" ({image})");
            }
            indicator?.Report(new CompressProgressStatus(++current, total));
        }
    }

    public void CompressImage(string imageFilePath, long qualityLevel)
    {

        string tempImage = Path.GetTempFileName();

        using (var image = (Bitmap)Bitmap.FromFile(imageFilePath))
        using (Graphics imageGraphics = Graphics.FromImage(image))
        {
            ImageCodecInfo formatEncoder = GetEncoder(Path.GetExtension(imageFilePath));
            Encoder myEncoder = Encoder.Quality;
            EncoderParameters myEncoderParameters = new(1);
            EncoderParameter myEncoderParameter = new(myEncoder, qualityLevel);
            myEncoderParameters.Param[0] = myEncoderParameter;

            image.Save(tempImage, formatEncoder, myEncoderParameters);
        }

        File.Delete(imageFilePath);
        File.Move(tempImage, imageFilePath);

    }

    #endregion

    #region ConvertToJpg
    public async Task SaveAllAsJpg(string workingFolder, bool removeOriginalFiles, IProgress<CompressProgressStatus> indicator)
    {
        var images = Directory
            .GetFiles(workingFolder, "*.*", SearchOption.AllDirectories)
            .Where(img => Path.GetExtension(img).ToLower() != ".jpg" || Regex.IsMatch(img, @"\.JPG$"))
            .ToArray();

        int total = images.Length;
        int current = 0;

        if (!IsFilesCountCheckSuccess(workingFolder, total))
        {
            return;
        }

        foreach (var file in images)
        {
            string ext = Path.GetExtension(file).ToLower();

            try
            {
                // Если jpeg то просто переименовываем
                if (ext.Equals(".jpeg") || file.EndsWith(".JPG"))
                {
                    string newFile = Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + ".jpg");
                    File.Move(file, newFile);
                }
                else if (new string[] { ".png", ".gif" }.Contains(ext))
                {
                    await Task.Run(() => ConvertToJpg(file, removeOriginalFiles)).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex.Message + $" ({file})");
            }
            indicator?.Report(new CompressProgressStatus(++current, total));
        }
    }

    private void ConvertToJpg(string sourceFile, bool removeOriginalFiles)
    {
        string newPath = Path.Combine(Path.GetDirectoryName(sourceFile), Path.GetFileNameWithoutExtension(sourceFile)) + ".jpg";

        bool isSuccessSave = false;
        using (var fs = new FileStream(sourceFile, FileMode.Open, FileAccess.Read))
        using (ImageFactory imageFactory = new(preserveExifData: true))
        {
            // Load, resize, set the format and quality and save an image.
            using (var newFile = new FileStream(newPath, FileMode.Create))
            {
                var format = new JpegFormat { Quality = 100 };
                imageFactory.Load(fs).BackgroundColor(Color.White).Format(format).Save(newFile);
                isSuccessSave = true;
            }
        }

        if (removeOriginalFiles && isSuccessSave)
        {
            File.Delete(sourceFile);
        }
    }
    #endregion

    #region Resize
    public async Task ResizeImages(string workingFolder,
        ImageSize newSize,
        ResizeMode resizeMode,
        IProgress<CompressProgressStatus> indicator = null, int threads = 2)
    {
        if (threads <= 0 || threads > (Environment.ProcessorCount * 3))
        {
            throw new ArgumentOutOfRangeException(nameof(threads));
        }

        var images = Directory.GetFiles(workingFolder, "*.*", SearchOption.AllDirectories).ToArray();
        int total = images.Count();
        int current = 0;

        if (!IsFilesCountCheckSuccess(workingFolder, total))
        {
            return;
        }

        using (SemaphoreSlim semaphore = new(threads))
        {
            var tasks = images
                .Select(img => Task.Run(() =>
                {
                    semaphore.Wait();
                    ResizeImage(img, new(newSize.Width, newSize.Height), resizeMode);
                    semaphore.Release();
                })
                .ContinueWith(t =>
                {
                    lock (lockObject)
                    {
                        current++;
                        indicator?.Report(new(current, total));
                    }
                }));

            await Task.WhenAll(tasks);
        }

    }

    public async Task ResizeImages(string workingFolder, ImageSize newSize, ResizeMode resizeMode, IProgress<CompressProgressStatus> indicator)
    {
        var images = Directory.GetFiles(workingFolder, "*.*", SearchOption.AllDirectories).ToArray();

        int total = images.Count();
        int current = 0;

        if (!IsFilesCountCheckSuccess(workingFolder, total))
        {
            return;
        }

        foreach (var image in images)
        {
            try
            {
                await Task.Run(() => ResizeImage(image, newSize, resizeMode)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex.Message + $" ({image})");
            }
            indicator?.Report(new(++current, total));
        }
    }

    private void ResizeImage(string image, ImageSize newSize, ResizeMode resizeMode)
    {
        ResizeLayer resizeLayer = new(new System.Drawing.Size(newSize.Width, newSize.Height), resizeMode);

        var tempFile = Path.GetTempFileName();

        using (var fs = new FileStream(image, FileMode.Open, FileAccess.Read))
        {
            using (ImageFactory imageFactory = new(false))
            {
                // Load, resize, set the format and quality and save an image.
                using (var newFile = new FileStream(tempFile, FileMode.Create))
                {
                    imageFactory
                    .Load(fs)
                    .Resize(resizeLayer)
                    .BackgroundColor(Color.White)
                    .Save(newFile);

                    lastResiedImages.Add(image);
                }
            }
        }

        File.Delete(image);
        File.Move(tempFile, image);
    }
    #endregion

    private bool IsFilesCountCheckSuccess(string folder, int filesCount)
    {
        if (filesCount > WARNING_FILES_MIN_COUNT)
        {
            var res = OnLimitWarning?.Invoke(folder, filesCount) ?? false;

            return res;
        }
        return true;
    }

    private ImageCodecInfo GetEncoder(string fileExt)
    {
        ImageFormat format = null;
        switch (fileExt.ToLower())
        {
            case ".jpeg":
            case ".jpg": format = ImageFormat.Jpeg; break;
            case ".png": format = ImageFormat.Png; break;
            default: throw new NotSupportedException();
        }

        ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
        foreach (ImageCodecInfo codec in codecs)
        {
            if (codec.FormatID == format.Guid)
            {
                return codec;
            }
        }
        return null;
    }

    public async Task<int> DeletePreviusResizedImages()
    {
        int totalDeleted = 0;

        if (lastResiedImages.Count == 0)
        {
            return 0;
        }

        foreach (var file in lastResiedImages)
        {
            if (File.Exists(file))
            {
                await Task.Run(() => File.Delete(file));
            }
        }

        lastResiedImages.Clear();

        await Task.Delay(TimeSpan.FromMilliseconds(400));

        return totalDeleted;
    }
}