using System;
using System.IO;
using System.Threading;

namespace ScreenCaptureApp.Services
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static readonly string _logFilePath = "app.log";
        private static readonly string _socketLogFilePath = "socket.log";

        public static event Action LogUpdated;

        public static void Log(string message, string level = "INFO")
        {
            try
            {
                lock (_lock)
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string logEntry = $"[{timestamp}] [{level}] {message}";
                    
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }
                LogUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                // Если не можем записать в лог, выводим в консоль
                Console.WriteLine($"Logger error: {ex.Message}");
            }
        }

        public static void LogInfo(string message)
        {
            Log(message, "INFO");
        }

        public static void LogError(string message, Exception ex = null)
        {
            string errorMessage = message;
            if (ex != null)
            {
                errorMessage += $"\nException: {ex.Message}\nStackTrace: {ex.StackTrace}";
            }
            Log(errorMessage, "ERROR");
        }

        public static void LogDebug(string message)
        {
            Log(message, "DEBUG");
        }

        public static void LogWarning(string message)
        {
            Log(message, "WARNING");
        }

        public static string GetAllLogs()
        {
            try
            {
                if (File.Exists(_logFilePath))
                {
                    return File.ReadAllText(_logFilePath);
                }
                else
                {
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                return $"Logger error: {ex.Message}";
            }
        }

        public static void ClearLog()
        {
            lock (_lock)
            {
                File.WriteAllText(_logFilePath, string.Empty);
            }
            LogUpdated?.Invoke();
        }

        public static void LogSocket(string message)
        {
            try
            {
                lock (_lock)
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string logEntry = $"[{timestamp}] [SOCKET] {message}";
                    File.AppendAllText(_socketLogFilePath, logEntry + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Logger SOCKET error: {ex.Message}");
            }
        }

        public static string GetAllSocketLogs()
        {
            try
            {
                if (File.Exists(_socketLogFilePath))
                {
                    return File.ReadAllText(_socketLogFilePath);
                }
                else
                {
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                return $"Logger SOCKET error: {ex.Message}";
            }
        }

        public static void ClearSocketLog()
        {
            lock (_lock)
            {
                File.WriteAllText(_socketLogFilePath, string.Empty);
            }
        }
    }
} 