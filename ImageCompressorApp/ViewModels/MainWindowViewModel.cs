using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageCompressorApp.Models;
using ImageCompressorApp.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
namespace ImageCompressorApp.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public static readonly int DEFAULT_THREADS_LIMIT = 8;
    //private readonly ImageMultiCompressor compressor;
    private readonly ImageProcessorManager imageManager;
    [ObservableProperty]
    public ObservableCollection<string> log = new();
    [ObservableProperty]
    public string title = "Обработчик изображений";
    [ObservableProperty]
    public bool inProgress = false;
    [ObservableProperty]
    public CompressParametersViewodel compressParameters = new();
    [ObservableProperty]
    public ProgressStatus progressStatus = new(0, 0);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWorkingDirectoryExists))]
    [NotifyCanExecuteChangedFor(nameof(ConvertImagesCommand), nameof(ResizeImagesCommand), nameof(CompressImagesCommand))]
    public string workingFolder = "";
    public bool IsWorkingDirectoryExists => Directory.Exists(WorkingFolder);
    public bool CanCopyErrorsTextCommand() => Log.Any();
    public MainWindowViewModel(ImageProcessorManager imageManager)
    {
        this.imageManager = imageManager;
        imageManager.ThreadsLimit = DEFAULT_THREADS_LIMIT;
        Log.CollectionChanged += (o, e) => OnPropertyChanged(nameof(CanCopyErrorsTextCommand));
        imageManager.OnError += (error) => App.Current.Dispatcher.Invoke(() => Log.Add(error));
        imageManager.OnLimitExceededResolver += (folder, filesCount) =>
        {
            var res = MessageBox.Show($"В папке {folder} находится {filesCount} файлов которые будут обработаны. \r\nВы действительно хотите выполнить команду ?",
               "Внимание",
               MessageBoxButton.YesNoCancel,
               MessageBoxImage.Warning);
            return res == MessageBoxResult.Yes;
        };
    }

    [RelayCommand]
    private void SelectDownloadFolder()
    {
        WorkingFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }
    [RelayCommand]
    private void SelectImagesFolder()
    {
        var ofd = new Microsoft.Win32.OpenFolderDialog();
        if (ofd.ShowDialog() == true)
        {
            var oldDirectory = WorkingFolder;
            WorkingFolder = ofd.FolderName;
        }
    }
    [RelayCommand(CanExecute = nameof(IsWorkingDirectoryExists))]
    private async Task ConvertImages()
    {
        Log.Clear();
        try
        {
            InProgress = true;
            await imageManager.ConvertImagesToJpg(WorkingFolder,
                CompressParameters.IsDeleteFilesAfterCompress,
                CreateIndicatorCallback(OperationType.ConvertToJpg));
            Title = $"Конвертация в JPG выполнена";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message + "\r\n\r\n" + ex.StackTrace, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            InProgress = false;
        }
    }
    [RelayCommand(CanExecute = nameof(IsWorkingDirectoryExists))]
    private async Task ResizeImages()
    {
        Log.Clear();
        InProgress = true;
        try
        {
            await imageManager.ResizeImages(WorkingFolder,
                new ImageSize(CompressParameters.ResizeWidth, CompressParameters.ResizeHeight),
                CompressParameters.SelectedResizeMode,
                CreateIndicatorCallback(OperationType.Resize));
            Title = $"Обработчик изображений | изменение размеров выполнено";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message + "\r\n\r\n" + ex.StackTrace, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            InProgress = false;
        }
    }
    [RelayCommand(CanExecute = nameof(IsWorkingDirectoryExists))]
    private async Task CompressImages()
    {
        InProgress = true;
        try
        {
            imageManager.MinimumImageSizeToResizeInKb = CompressParameters.MinimumSizeToCompressInKb;
            await imageManager.CompressImages(WorkingFolder,
                CompressParameters.SelectedQuality,
                CreateIndicatorCallback(OperationType.Compress));
            Title = $"Сжатие изображений выполнено";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message + "\r\n\r\n" + ex.StackTrace, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            InProgress = false;
        }
    }
    [RelayCommand(CanExecute = nameof(CanCopyErrorsTextCommand))]
    private void CopyErrorsText() => Clipboard.SetText(string.Join(Environment.NewLine, Log));
    private IProgress<ProgressStatus> CreateIndicatorCallback(OperationType operation)
    {
        string operationMessage = operation switch
        {
            OperationType.ConvertToJpg => "по преобразованию в JPG",
            OperationType.Resize => "по изменения размера изображений",
            OperationType.Compress => "по сжатию изображений",
            _ => throw new NotSupportedException()
        };
        return new Progress<ProgressStatus>((v) =>
        {
            ProgressStatus = v;
            Title = $"Выполнение операции {operationMessage}: {v.Current} / {v.Total} ({v.Percent:P0})";
        });
    }
}
