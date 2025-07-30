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

        /// <summary>
        /// Логирует сообщение в отдельный файл по тегу
        /// </summary>
        /// <param name="tag">Тег (название файла)</param>
        /// <param name="message">Сообщение для логирования</param>
        /// <param name="logLevel">Уровень логирования (INFO, ERROR, DEBUG, WARNING)</param>
        public static void LogTag(string tag, string message, string logLevel = "INFO")
        {
            try
            {
                lock (_lock)
                {
                    string logFileName = $"{tag}.log";
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string logEntry = $"[{timestamp}] [{logLevel}] {message}";
                    
                    File.AppendAllText(logFileName, logEntry + Environment.NewLine);
                }
                LogUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                // Если не можем записать в лог, выводим в консоль
                Console.WriteLine($"Logger Tag error for {tag}: {ex.Message}");
            }
        }

        /// <summary>
        /// Логирует информационное сообщение по тегу
        /// </summary>
        /// <param name="tag">Тег (название файла)</param>
        /// <param name="message">Сообщение для логирования</param>
        public static void LogTagInfo(string tag, string message)
        {
            LogTag(tag, message, "INFO");
        }

        /// <summary>
        /// Логирует сообщение об ошибке по тегу
        /// </summary>
        /// <param name="tag">Тег (название файла)</param>
        /// <param name="message">Сообщение для логирования</param>
        /// <param name="ex">Исключение (опционально)</param>
        public static void LogTagError(string tag, string message, Exception ex = null)
        {
            string errorMessage = message;
            if (ex != null)
            {
                errorMessage += $"\nException: {ex.Message}\nStackTrace: {ex.StackTrace}";
            }
            LogTag(tag, errorMessage, "ERROR");
        }

        /// <summary>
        /// Логирует отладочное сообщение по тегу
        /// </summary>
        /// <param name="tag">Тег (название файла)</param>
        /// <param name="message">Сообщение для логирования</param>
        public static void LogTagDebug(string tag, string message)
        {
            LogTag(tag, message, "DEBUG");
        }

        /// <summary>
        /// Логирует предупреждение по тегу
        /// </summary>
        /// <param name="tag">Тег (название файла)</param>
        /// <param name="message">Сообщение для логирования</param>
        public static void LogTagWarning(string tag, string message)
        {
            LogTag(tag, message, "WARNING");
        }

        /// <summary>
        /// Получает все логи по тегу
        /// </summary>
        /// <param name="tag">Тег (название файла)</param>
        /// <returns>Содержимое файла лога</returns>
        public static string GetTagLogs(string tag)
        {
            try
            {
                string logFileName = $"{tag}.log";
                if (File.Exists(logFileName))
                {
                    return File.ReadAllText(logFileName);
                }
                else
                {
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                return $"Logger Tag error for {tag}: {ex.Message}";
            }
        }

        /// <summary>
        /// Очищает лог по тегу
        /// </summary>
        /// <param name="tag">Тег (название файла)</param>
        public static void ClearTagLog(string tag)
        {
            lock (_lock)
            {
                string logFileName = $"{tag}.log";
                File.WriteAllText(logFileName, string.Empty);
            }
            LogUpdated?.Invoke();
        }
    }
} 