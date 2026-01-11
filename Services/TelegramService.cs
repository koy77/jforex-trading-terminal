using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ScreenCaptureApp.Services
{
    public class TelegramService
    {
        private readonly string _botToken = "7705031494:AAHS06hFSe0aoqFjLhEidHgcQct7obmENVk";
        private readonly string _channelId = "-1003577152426";
        private const int MaxRetryAttempts = 3;
        private const int RetryDelayMinutes = 3;

        public TelegramService()
        {
        }

        private async Task SendPhotoAsync(string photoPath)
        {
            if (string.IsNullOrEmpty(_botToken) || _botToken == "YOUR_BOT_TOKEN")
            {
                Logger.LogError("TELEGRAM_BOT_TOKEN не настроен");
                return;
            }

            for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
            {
                try
                {
                    var bot = new TelegramBotClient(_botToken);
                    using (var photoStream = File.OpenRead(photoPath))
                    {
                        await bot.SendPhoto(
                            chatId: _channelId,
                            photo: InputFile.FromStream(photoStream, Path.GetFileName(photoPath))
                        );
                    }
                    Logger.LogInfo($"Скриншот отправлен в Telegram: {photoPath} (попытка {attempt})");
                    return; // Успешная отправка, выходим из метода
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Ошибка отправки в Telegram (попытка {attempt}/{MaxRetryAttempts}): {ex.Message}", ex);

                    if (attempt < MaxRetryAttempts)
                    {
                        Logger.LogInfo($"Повторная попытка через {RetryDelayMinutes} минут...");
                        await Task.Delay(TimeSpan.FromMinutes(RetryDelayMinutes));
                    }
                    else
                    {
                        Logger.LogError($"Все попытки отправки в Telegram исчерпаны. Файл: {photoPath}");
                    }
                }
            }

            // Удаляем временный файл после всех попыток
            try
            {
                if (File.Exists(photoPath))
                {
                    File.Delete(photoPath);
                    Logger.LogInfo($"Временный файл удален: {photoPath}");
                }
            }
            catch (Exception cleanupError)
            {
                Logger.LogError($"Ошибка при удалении временного файла: {cleanupError.Message}", cleanupError);
            }
        }

        public void SendPhoto(string photoPath)
        {
            Task.Run(async () => await SendPhotoAsync(photoPath));
        }
    }
}
