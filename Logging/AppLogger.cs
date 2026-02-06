using System;
using System.IO;

namespace HomeoMahanagarLabelCleanV2.Logging
{
    public static class AppLogger
    {
        // Put logs under an explicit "log" folder next to the application executable
        // Single centralized log file for easier inspection by the user.
        private static readonly string PrimaryLogDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
        private static readonly string PrimaryLogFile = Path.Combine(PrimaryLogDir, "app.log");

        // Ensure logging directories and files exist early
        public static void Initialize()
        {
            try { Directory.CreateDirectory(PrimaryLogDir); } catch { }

            try
            {
                if (!File.Exists(PrimaryLogFile))
                    File.WriteAllText(PrimaryLogFile, $"Log created: {DateTime.Now:O}\n");
            }
            catch { }
        }

        public static void Log(Exception ex)
        {
            try
            {
                string text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {ex.GetType()}: {ex.Message}\n{ex.StackTrace}\n";
                if (ex.InnerException != null)
                    text += $"Inner: {ex.InnerException.GetType()}: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}\n";

                // append to single primary log file
                try { File.AppendAllText(PrimaryLogFile, text + "\n"); } catch { }
            }
            catch
            {
                // swallow logging errors to avoid recursive failures
            }
        }

        public static void Log(string message)
        {
            try
            {
                string text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {message}\n";
                try { File.AppendAllText(PrimaryLogFile, text + "\n"); } catch { }
            }
            catch { }
        }

        public static string[] GetLogPaths() => new[] { PrimaryLogFile };
    }
}
