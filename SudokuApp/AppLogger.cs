using System.IO;
using System.Text;

namespace SudokuApp;

public static class AppLogger
{
    private static readonly object SyncRoot = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SudokuApp",
        "logs");
    private static readonly string LogFilePath = Path.Combine(LogDirectory, "app.log");

    public static string CurrentLogFilePath => LogFilePath;

    public static void Info(string message)
    {
        Write("INFO", message, null);
    }

    public static void Error(string message, Exception? ex = null)
    {
        Write("ERROR", message, ex);
    }

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            lock (SyncRoot)
            {
                Directory.CreateDirectory(LogDirectory);
                var builder = new StringBuilder();
                builder.Append('[')
                    .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                    .Append("] [")
                    .Append(level)
                    .Append("] ")
                    .AppendLine(message);

                if (ex is not null)
                {
                    builder.AppendLine(ex.ToString());
                }

                File.AppendAllText(LogFilePath, builder.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // 日志写入不应影响主流程
        }
    }
}