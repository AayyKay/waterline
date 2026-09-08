namespace Waterline.Infrastructure;

public sealed record LegacyDataLocation(string RootPath, string LevelDbPath, bool Exists);

public sealed class LegacyDataDiscovery
{
    private readonly string _roamingAppData;

    public LegacyDataDiscovery(string? roamingAppData = null)
    {
        _roamingAppData = roamingAppData ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    }

    public IReadOnlyList<LegacyDataLocation> FindElectronLocations()
    {
        var candidates = new[]
        {
            Path.Combine(_roamingAppData, "Waterline"),
            Path.Combine(_roamingAppData, "waterline")
        };

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(root =>
            {
                var levelDb = Path.Combine(root, "Local Storage", "leveldb");
                return new LegacyDataLocation(root, levelDb, Directory.Exists(levelDb));
            })
            .ToList();
    }
}
