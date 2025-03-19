using System.IO;
using System.Text.Json;

namespace ImageCompressorApp.Services;

public interface ISettingsStorage
{
    Task<T> LoadSettings<T>();
    Task SaveSettings<T>(T settings);
}

public class JsonSettingsStorage : ISettingsStorage
{
    private readonly string fileName;
    private readonly JsonSerializerOptions options = new() { WriteIndented = true };

    public JsonSettingsStorage(string fileName)
    {
        this.fileName = fileName;
        if (!File.Exists(fileName))
        {
            using var file = File.Create(fileName);
        }
    }

    public async Task<T> LoadSettings<T>()
    {
        string jsonText = await File.ReadAllTextAsync(fileName);
        var data = JsonSerializer.Deserialize<T>(jsonText, options);
        return data;
    }

    public async Task SaveSettings<T>(T settings)
    {
        string jsonText = JsonSerializer.Serialize(settings, options);
        await File.WriteAllTextAsync(fileName, jsonText);
    }
}
