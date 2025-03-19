using ImageCompressorApp.Services;
using ImageCompressorApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
                services.AddSingleton<ISettingsStorage>(new JsonSettingsStorage("settings.json"));
                services.AddSingleton<MainWindowViewModel>();
            })
            .Build();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        var vm = AppHost.Services.GetService<MainWindowViewModel>();
        var window = new MainWindow();
        window.DataContext = vm;
        window.ShowDialog();
    }
}

