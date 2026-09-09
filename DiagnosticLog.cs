using System.Text;
using System.IO;

namespace Waterline;

public sealed class DiagnosticLog
{
    private const int MaximumEntries = 200;
    private readonly string _path;
    private readonly object _gate = new();

    public DiagnosticLog(string stateFilePath)
    {
        _path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(stateFilePath)!, "diagnostics.log");
    }

    public string Path => _path;
    public bool Exists => File.Exists(_path);

    public void Record(string category, Exception exception)
    {
        var safeCategory = Sanitize(category);
        var safeMessage = Sanitize(exception.Message);
        var line = $"{DateTimeOffset.UtcNow:O}\t{safeCategory}\t{exception.GetType().Name}\t{safeMessage}";
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
                var lines = File.Exists(_path) ? File.ReadAllLines(_path).TakeLast(MaximumEntries - 1) : [];
                File.WriteAllLines(_path, lines.Append(line), Encoding.UTF8);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public bool Clear()
    {
        lock (_gate)
        {
            try
            {
                if (File.Exists(_path)) File.Delete(_path);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }

    private static string Sanitize(string value) => value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
}
