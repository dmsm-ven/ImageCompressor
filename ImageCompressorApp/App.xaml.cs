using ImageCompressorApp.Services;
using ImageCompressorApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
namespace ImageCompressorApp;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public IHost AppHost { get; }
    public App()
    {
        AppHost = Host
            .CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IImageProcessor, ImageMultiCompressor>();
                services.AddSingleton<MainWindowViewModel>();
            })
            .Build();
    }
    protected override async void OnStartup(StartupEventArgs e)
    {
        var vm = AppHost.Services.GetService<MainWindowViewModel>();
        App.Current.MainWindow = new MainWindow();
        App.Current.MainWindow.DataContext = vm;
        string workingDir = e.Args.Length > 0 ? e.Args[0] : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        vm!.WorkingFolder = workingDir;
        string resolutionOptions = e.Args.Length > 1 ? e.Args[1] : string.Empty;
        var resMatch = Regex.Match(resolutionOptions, @"(?<width>\d+)[x|х|*](?<height>\d+)");
        if (resMatch.Success)
        {
            vm.CompressParameters.ResizeWidth = int.Parse(resMatch.Groups["width"].Value);
            vm.CompressParameters.ResizeHeight = int.Parse(resMatch.Groups["height"].Value);
            vm.CompressParameters.IsDeleteFilesAfterCompress = true;
            await vm.ConvertImagesCommand.ExecuteAsync(null);
            await Task.Delay(TimeSpan.FromSeconds(0.5));
            await vm.ResizeImagesCommand.ExecuteAsync(null);
            Application.Current.Shutdown();
        }
        App.Current.MainWindow.ShowDialog();
    }
}
