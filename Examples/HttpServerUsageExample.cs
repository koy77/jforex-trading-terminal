using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ScreenCaptureApp.Models;

namespace ScreenCaptureApp.Examples
{
    /// <summary>
    /// Пример использования HTTP сервиса для тестирования
    /// </summary>
    public static class HttpServerUsageExample
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string BaseUrl = "http://localhost:7000";

        /// <summary>
        /// Отправляет данные ценового уровня на сервер
        /// </summary>
        public static async Task SendPriceLevelAsync(string symbol, decimal price, string additionalData = null)
        {
            try
            {
                var priceLevelData = new PriceLevelData
                {
                    Symbol = symbol,
                    Price = price,
                    Timestamp = DateTime.Now,
                    AdditionalData = additionalData
                };

                var json = JsonConvert.SerializeObject(priceLevelData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{BaseUrl}/pricelevel", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Response: {response.StatusCode} - {responseContent}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending price level: {ex.Message}");
            }
        }

        /// <summary>
        /// Проверяет статус сервера
        /// </summary>
        public static async Task CheckServerStatusAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}/status");
                var content = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Server Status: {response.StatusCode} - {content}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking server status: {ex.Message}");
            }
        }

        /// <summary>
        /// Проверяет здоровье сервера
        /// </summary>
        public static async Task CheckServerHealthAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}/health");
                var content = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Server Health: {response.StatusCode} - {content}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking server health: {ex.Message}");
            }
        }

        /// <summary>
        /// Пример использования всех методов
        /// </summary>
        public static async Task RunExampleAsync()
        {
            Console.WriteLine("=== HTTP Server Usage Example ===");

            // Проверяем статус сервера
            await CheckServerStatusAsync();

            // Проверяем здоровье сервера
            await CheckServerHealthAsync();

            // Отправляем несколько ценовых уровней
            await SendPriceLevelAsync("EURUSD", 1.0850m, "Support level");
            await SendPriceLevelAsync("GBPUSD", 1.2650m, "Resistance level");
            await SendPriceLevelAsync("USDJPY", 150.25m, "Key level");

            Console.WriteLine("=== Example completed ===");
        }
    }
} 