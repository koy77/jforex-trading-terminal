using System;
using System.IO;
using System.Threading;

namespace ScreenCaptureApp.Services
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static readonly string _logFilePath = "app.log";

        static Logger()
        {
            // Очищаем файл лога при инициализации
            try
            {
                if (File.Exists(_logFilePath))
                {
                    File.WriteAllText(_logFilePath, string.Empty);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to clear log file: {ex.Message}");
            }
        }

        public static event Action LogUpdated;

        public static void Log(string message, string level = "INFO", string tag = null)
        {
            try
            {
                lock (_lock)
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string logEntry;
                    
                    if (!string.IsNullOrEmpty(tag))
                    {
                        logEntry = $"[{timestamp}] [{level}][{tag}] {message}";
                    }
                    else
                    {
                        logEntry = $"[{timestamp}] [{level}] {message}";
                    }
                    
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

        public static void LogError(string message, Exception ex = null, string tag = null)
        {
            string errorMessage = message;
            if (ex != null)
            {
                errorMessage += $"\nException: {ex.Message}\nStackTrace: {ex.StackTrace}";
            }
            Log(errorMessage, "ERROR", tag);
        }

        public static void LogDebug(string message, string tag = null)
        {
            Log(message, "DEBUG", tag);
        }

        public static void LogWarning(string message, string tag = null)
        {
            Log(message, "WARNING", tag);
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
            Log(message, "SOCKET");
        }

        /// <summary>
        /// Логирует сообщение с тегом в единый файл лога
        /// </summary>
        /// <param name="tag">Тег для категоризации</param>
        /// <param name="message">Сообщение для логирования</param>
        /// <param name="logLevel">Уровень логирования (INFO, ERROR, DEBUG, WARNING)</param>
        public static void LogTag(string tag, string message, string logLevel = "INFO")
        {
            Log(message, logLevel, tag);
        }

        /// <summary>
        /// Логирует информационное сообщение по тегу
        /// </summary>
        /// <param name="tag">Тег для категоризации</param>
        /// <param name="message">Сообщение для логирования</param>
        public static void LogTagInfo(string tag, string message)
        {
            Log(message, "INFO", tag);
        }

        /// <summary>
        /// Логирует сообщение об ошибке по тегу
        /// </summary>
        /// <param name="tag">Тег для категоризации</param>
        /// <param name="message">Сообщение для логирования</param>
        /// <param name="ex">Исключение (опционально)</param>
        public static void LogTagError(string tag, string message, Exception ex = null)
        {
            LogError(message, ex, tag);
        }

        /// <summary>
        /// Логирует отладочное сообщение по тегу
        /// </summary>
        /// <param name="tag">Тег для категоризации</param>
        /// <param name="message">Сообщение для логирования</param>
        public static void LogTagDebug(string tag, string message)
        {
            Log(message, "DEBUG", tag);
        }

        /// <summary>
        /// Логирует предупреждение по тегу
        /// </summary>
        /// <param name="tag">Тег для категоризации</param>
        /// <param name="message">Сообщение для логирования</param>
        public static void LogTagWarning(string tag, string message)
        {
            Log(message, "WARNING", tag);
        }

    }
} 