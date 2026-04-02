using System.Diagnostics;
using System.Text;

namespace TravelSystem.Mobile.Services;

public static class DiagnosticLogService
{
    private const string LogFileName = "gps_map_trace.log";
    private const string BackupLogFileName = "gps_map_trace.prev.log";
    private const long MaxLogBytes = 2 * 1024 * 1024; // 2 MB
    private static readonly SemaphoreSlim WriteLock = new(1, 1);
    private static readonly string LogFilePathValue = Path.Combine(FileSystem.AppDataDirectory, LogFileName);
    private static readonly string BackupLogFilePathValue = Path.Combine(FileSystem.AppDataDirectory, BackupLogFileName);

    private static int _pathLogged;

    public static string LogFilePath => LogFilePathValue;

    public static void Log(string category, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}][{category}] {message}";
        Debug.WriteLine(line);
        _ = AppendLineAsync(line + Environment.NewLine);

        if (Interlocked.Exchange(ref _pathLogged, 1) == 0)
        {
            var pathLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}][LOG_FILE] {LogFilePathValue}";
            Debug.WriteLine(pathLine);
            _ = AppendLineAsync(pathLine + Environment.NewLine);
        }
    }

    public static async Task<string> ReadTailAsync(int maxLines = 400)
    {
        try
        {
            if (!File.Exists(LogFilePathValue))
            {
                return string.Empty;
            }

            var lines = await File.ReadAllLinesAsync(LogFilePathValue);
            if (lines.Length <= maxLines)
            {
                return string.Join(Environment.NewLine, lines);
            }

            var tail = lines.Skip(Math.Max(0, lines.Length - maxLines));
            return string.Join(Environment.NewLine, tail);
        }
        catch
        {
            return string.Empty;
        }
    }

    public static async Task ClearAsync()
    {
        await WriteLock.WaitAsync();
        try
        {
            if (File.Exists(LogFilePathValue))
            {
                File.Delete(LogFilePathValue);
            }

            if (File.Exists(BackupLogFilePathValue))
            {
                File.Delete(BackupLogFilePathValue);
            }
        }
        catch
        {
        }
        finally
        {
            WriteLock.Release();
        }
    }

    private static async Task AppendLineAsync(string line)
    {
        await WriteLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFilePathValue)!);
            RotateIfNeeded();
            await File.AppendAllTextAsync(LogFilePathValue, line, Encoding.UTF8);
        }
        catch
        {
        }
        finally
        {
            WriteLock.Release();
        }
    }

    private static void RotateIfNeeded()
    {
        try
        {
            if (!File.Exists(LogFilePathValue))
            {
                return;
            }

            var info = new FileInfo(LogFilePathValue);
            if (info.Length < MaxLogBytes)
            {
                return;
            }

            if (File.Exists(BackupLogFilePathValue))
            {
                File.Delete(BackupLogFilePathValue);
            }

            File.Move(LogFilePathValue, BackupLogFilePathValue);
        }
        catch
        {
        }
    }
}
