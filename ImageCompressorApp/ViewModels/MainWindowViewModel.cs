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
    //private readonly ImageMultiCompressor compressor;
    private readonly ISettingsStorage settings;
    private readonly IImageProcessor imageProcessor;

    [ObservableProperty]
    public ObservableCollection<string> log = new();

    [ObservableProperty]
    public string title = "Обработчик изображений";

    [ObservableProperty]
    public bool inProgress = false;

    [ObservableProperty]
    public CompressParametersViewodel compressParameters = new();

    [ObservableProperty]
    public CompressProgressStatus progressStatus = new(0, 0);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WorkingDirectoryExists))]
    [NotifyCanExecuteChangedFor(nameof(CompressImagesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResizeImagesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConvertImagesCommand))]
    public string workingFolder = "";

    public MainWindowViewModel(ISettingsStorage settings, IImageProcessor imageProcessor)
    {
        Log.CollectionChanged += (o, e) => OnPropertyChanged(nameof(CanCopyErrorsTextCommand));
        this.settings = settings;
        this.imageProcessor = imageProcessor;
        imageProcessor.OnError += (error) => App.Current.Dispatcher.Invoke(() => Log.Add(error));
    }

    [RelayCommand]
    public async Task Loaded()
    {
        WorkingFolder = (await settings.LoadSettings<UserSettingsEntry>()).WorkingFolder;
    }

    public bool WorkingDirectoryExists => Directory.Exists(WorkingFolder);

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
            WorkingFolder = ofd.FolderName;
        }
    }

    [RelayCommand(CanExecute = nameof(WorkingDirectoryExists))]
    private void EraseWatermakrs()
    {
        MessageBox.Show("Not implemented");
    }

    [RelayCommand(CanExecute = nameof(WorkingDirectoryExists))]
    private async Task ConvertImages()
    {
        Log.Clear();

        try
        {
            InProgress = true;

            await imageProcessor.SaveAllAsJpg(WorkingFolder, CompressParameters.IsDeleteFilesAfterCompress, CreateIndicatorCallback());

            MessageBox.Show($"Конвертация в JPG выполнена", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
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

    [RelayCommand(CanExecute = nameof(WorkingDirectoryExists))]
    private async Task ResizeImages()
    {
        Log.Clear();

        InProgress = true;
        try
        {
            int? deleted = null;

            if (CompressParameters.IsDeletePreviusResizedImages)
            {
                deleted = await imageProcessor.DeletePreviusResizedImages();
            }

            if (CompressParameters.ResizeWidth > 32 && CompressParameters.ResizeHeight > 32)
            {
                await imageProcessor.ResizeImages(WorkingFolder,
                    new ImageSize(CompressParameters.ResizeWidth, CompressParameters.ResizeHeight),
                    CreateIndicatorCallback());

                Title = $"Обработчик изображений | изменение размеров выполнено";
                if (deleted.HasValue && deleted.Value > 0)
                {
                    Title += $" | удалено {deleted.Value} изображений";
                }
            }
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

    [RelayCommand(CanExecute = nameof(WorkingDirectoryExists))]
    private async Task CompressImages()
    {
        InProgress = true;
        try
        {
            await imageProcessor.CompressImages(WorkingFolder,
                CompressParameters.SelectedQuality,
                CompressParameters.MinimumSizeToCompressInKb,
                CreateIndicatorCallback());

            MessageBox.Show($"Сжатие изображений выполнено", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
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
    private void CopyErrorsText()
    {
        Clipboard.SetText(string.Join(Environment.NewLine, Log));
    }

    private bool CanCopyErrorsTextCommand() => Log.Any();

    public IProgress<CompressProgressStatus> CreateIndicatorCallback()
    {
        return new Progress<CompressProgressStatus>((v) =>
        {
            ProgressStatus = v;
            Title = $"Выполнение операции: {v.Current} / {v.Total} ({v.Percent:P0})";
        });
    }
}