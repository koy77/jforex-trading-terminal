using System;
using System.Collections.Generic;
using ScreenCaptureApp.Services;
using ScreenCaptureApp.Models;
using System.Linq;

namespace ScreenCaptureApp.Examples
{
    /// <summary>
    /// Примеры использования обновленного DatabaseService
    /// </summary>
    public class DatabaseServiceUsageExample
    {
        private readonly DatabaseService _databaseService;

        public DatabaseServiceUsageExample()
        {
            _databaseService = new DatabaseService();
        }

        /// <summary>
        /// Пример инициализации символов из массива jforex_symbols
        /// </summary>
        public void InitializeSymbolsExample()
        {
            // Массив символов из jforex_symbols
            string[] jforexSymbols = { "XAUUSD", "GBPJPY", "EURUSD", "USDJPY", "GBPUSD" };

            // Сохраняем символы в базу данных с дефолтным риском 1%
            _databaseService.SaveSymbolsFromArray(jforexSymbols, 1.0);

            Console.WriteLine($"Initialized {jforexSymbols.Length} symbols in database");
        }

        /// <summary>
        /// Пример работы с настройками риска для символов
        /// </summary>
        public void RiskManagementExample()
        {
            // Устанавливаем разные уровни риска для символов
            _databaseService.UpdateSymbolRisk("XAUUSD", 1.0);   // 1% риск
            _databaseService.UpdateSymbolRisk("GBPJPY", 5.0);   // 5% риск
            _databaseService.UpdateSymbolRisk("EURUSD", 10.0);  // 10% риск
            _databaseService.UpdateSymbolRisk("USDJPY", 30.0);  // 30% риск

            // Получаем символ с его настройками риска
            var symbol = _databaseService.GetSymbolByName("GBPJPY");
            if (symbol != null)
            {
                Console.WriteLine($"Symbol: {symbol.Symbol}, Risk: {symbol.RiskPercent}%");
            }
        }

        /// <summary>
        /// Пример активации/деактивации символов
        /// </summary>
        public void SymbolActivationExample()
        {
            // Деактивируем символ
            _databaseService.SetSymbolActive("USDJPY", false);

            // Получаем только активные символы
            var activeSymbols = _databaseService.GetActiveSymbols();
            Console.WriteLine($"Active symbols: {activeSymbols.Count}");

            foreach (var symbol in activeSymbols)
            {
                Console.WriteLine($"- {symbol.Symbol} (Risk: {symbol.RiskPercent}%)");
            }
        }

        /// <summary>
        /// Пример получения статистики базы данных
        /// </summary>
        public void DatabaseStatsExample()
        {
            var stats = _databaseService.GetDatabaseStats();
            Console.WriteLine($"Database contains {stats.capturesCount} captures and {stats.symbolsCount} symbols");

            // Получаем все символы с их настройками
            var allSymbols = _databaseService.GetAllSymbols();
            Console.WriteLine("\nAll symbols in database:");
            foreach (var symbol in allSymbols)
            {
                string status = symbol.IsActive ? "Active" : "Inactive";
                Console.WriteLine($"- {symbol.Symbol}: {symbol.RiskPercent}% risk ({status})");
            }
        }

        /// <summary>
        /// Пример очистки данных
        /// </summary>
        public void DataCleanupExample()
        {
            Console.WriteLine("Clearing only captures (symbols will be preserved)...");
            _databaseService.ClearAllCaptures();

            var stats = _databaseService.GetDatabaseStats();
            Console.WriteLine($"After clearing captures: {stats.capturesCount} captures, {stats.symbolsCount} symbols");

            // Для полной очистки (включая символы) используйте:
            // _databaseService.ClearAllData();
        }

        /// <summary>
        /// Пример создания нового символа с настройками
        /// </summary>
        public void CreateNewSymbolExample()
        {
            var newSymbol = new SymbolData
            {
                Symbol = "AUDUSD",
                RiskPercent = 15.0,
                IsActive = true
            };

            _databaseService.SaveSymbol(newSymbol);
            Console.WriteLine($"Created new symbol: {newSymbol.Symbol} with {newSymbol.RiskPercent}% risk");
        }

        /// <summary>
        /// Запуск всех примеров
        /// </summary>
        public void RunAllExamples()
        {
            Console.WriteLine("=== DatabaseService Usage Examples ===\n");

            InitializeSymbolsExample();
            Console.WriteLine();

            RiskManagementExample();
            Console.WriteLine();

            SymbolActivationExample();
            Console.WriteLine();

            DatabaseStatsExample();
            Console.WriteLine();

            CreateNewSymbolExample();
            Console.WriteLine();

            DataCleanupExample();
            Console.WriteLine();

            Console.WriteLine("=== Examples completed ===");
        }

        public static void RunExample()
        {
            var databaseService = new DatabaseService();
            
            // Получаем все захваты
            var allCaptures = databaseService.GetAllCaptures();
            Console.WriteLine($"Всего захватов: {allCaptures.Count}");
            
            // Получаем только неактивированные захваты
            var unfiredCaptures = databaseService.GetAllUnfiredCaptures();
            Console.WriteLine($"Неактивированных захватов: {unfiredCaptures.Count}");
            
            // Получаем неактивированные и не пропущенные захваты
            var unfiredAndUnskippedCaptures = databaseService.GetAllUnfiredAndUnskippedCaptures();
            Console.WriteLine($"Неактивированных и не пропущенных захватов: {unfiredAndUnskippedCaptures.Count}");
            
            // Пример создания нового захвата с новыми полями
            var newCapture = new CaptureData
            {
                X = 100,
                Y = 100,
                Width = 200,
                Height = 150,
                Handle = 123456,
                Monitor = 0,
                Timestamp = DateTime.Now.ToString("O"),
                ScreenshotPath = "test_screenshot.png",
                Symbol = "XAUUSD",
                Risk = 1.0,
                IsFired = false,
                IsSkipped = false
            };
            
            // Сохраняем новый захват
            databaseService.SaveCapture(newCapture);
            Console.WriteLine($"Новый захват сохранен с ID: {newCapture.ID}");
            
            // Получаем обновленную статистику
            var stats = databaseService.GetDatabaseStats();
            Console.WriteLine($"Статистика БД: {stats.capturesCount} захватов, {stats.symbolsCount} символов");
            
            // Пример обновления IsFired для захвата
            if (unfiredCaptures.Count > 0)
            {
                var captureToUpdate = unfiredCaptures[0];
                databaseService.UpdateCaptureIsFired(captureToUpdate, true);
                Console.WriteLine($"Захват обновлен: IsFired = true для ID: {captureToUpdate.ID}");
                
                // Проверяем, что захват больше не в списке неактивированных
                var updatedUnfiredCaptures = databaseService.GetAllUnfiredCaptures();
                Console.WriteLine($"Неактивированных захватов после обновления: {updatedUnfiredCaptures.Count}");
            }
            
            // Пример работы с полем IsSkipped
            if (unfiredAndUnskippedCaptures.Count > 0)
            {
                var captureToSkip = unfiredAndUnskippedCaptures[0];
                databaseService.UpdateCaptureIsSkipped(captureToSkip, true);
                Console.WriteLine($"Захват пропущен: IsSkipped = true для ID: {captureToSkip.ID}");
                
                // Проверяем, что захват больше не в списке неактивированных и не пропущенных
                var updatedUnfiredAndUnskippedCaptures = databaseService.GetAllUnfiredAndUnskippedCaptures();
                Console.WriteLine($"Неактивированных и не пропущенных захватов после пропуска: {updatedUnfiredAndUnskippedCaptures.Count}");
            }
            
            // Пример отображения информации о захватах с новыми полями
            Console.WriteLine("\nИнформация о захватах:");
            foreach (var capture in allCaptures.Take(3)) // Показываем первые 3 захвата
            {
                Console.WriteLine($"ID: {capture.ID}");
                Console.WriteLine($"  Symbol: {capture.Symbol}");
                Console.WriteLine($"  Handle: {capture.Handle}");
                Console.WriteLine($"  IsFired: {capture.IsFired}");
                Console.WriteLine($"  IsSkipped: {capture.IsSkipped}");
                Console.WriteLine();
            }
        }
    }
} 