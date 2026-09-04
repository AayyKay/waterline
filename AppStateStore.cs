using System.Text.Json;
using System.IO;

namespace Waterline;

public sealed class AppStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;

    public AppStateStore()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Waterline");
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "state.json");
    }

    public WaterlineState Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new WaterlineState();
            return JsonSerializer.Deserialize<WaterlineState>(File.ReadAllText(_filePath), JsonOptions) ?? new WaterlineState();
        }
        catch
        {
            return new WaterlineState();
        }
    }

    public void Save(WaterlineState state)
    {
        var temporaryFile = _filePath + ".tmp";
        File.WriteAllText(temporaryFile, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(temporaryFile, _filePath, true);
    }
}
