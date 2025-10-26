using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using ScreenCaptureApp.Models;
using ScreenCaptureApp.Services;

namespace ScreenCaptureApp.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath;
        private readonly JsonSerializerOptions _jsonOptions;        private CaptureDbRoot _cachedRoot;
        private bool _isInitialized = false;
        private readonly object _lockObject = new object();

        public DatabaseService()
        {
            _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "db.json");
            _dbPath = Path.GetFullPath(_dbPath);
            _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        }

        /// <summary>
        /// Инициализирует сервис - загружает базу данных в память
        /// </summary>
        public void Initialize()
        {
            lock (_lockObject)
            {
                if (_isInitialized)
                {
                    Logger.LogWarning("DatabaseService: Already initialized");
                    return;
                }

                _cachedRoot = LoadOrCreateRoot();
                _isInitialized = true;
                Logger.LogInfo($"DatabaseService: Initialized with {_cachedRoot.Captures.Count} captures and {_cachedRoot.Symbols.Count} symbols");
            }
        }

        /// <summary>
        /// Сохраняет базу данных на диск и закрывает сервис
        /// </summary>
        public void Shutdown()
        {
            lock (_lockObject)
            {
                if (!_isInitialized)
                {
                    Logger.LogWarning("DatabaseService: Not initialized, nothing to save");
                    return;
                }

                SaveRoot(_cachedRoot);
                _isInitialized = false;
                Logger.LogInfo("DatabaseService: Shutdown completed, database saved to disk");
            }
        }

        #region Capture Methods

        /// <summary>
        /// Сохраняет данные о захвате
        /// </summary>
        public void SaveCapture(CaptureData entry)
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                _cachedRoot.Captures.Add(entry);
                SaveRoot(_cachedRoot);
            }
        }

        /// <summary>
        /// Обновляет существующую запись о захвате
        /// </summary>
        public void UpdateCapture(CaptureData updatedEntry)
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                
                // Находим последнюю запись и обновляем её
                if (_cachedRoot.Captures.Count > 0)
                {
                    var lastCapture = _cachedRoot.Captures[_cachedRoot.Captures.Count - 1];
                    lastCapture.ScreenshotPath = updatedEntry.ScreenshotPath;
                }
                
                SaveRoot(_cachedRoot);
            }
        }

        /// <summary>
        /// Получает все сохраненные захваты
        /// </summary>
        public List<CaptureData> GetAllCaptures()
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                return _cachedRoot.Captures;
            }
        }

        /// <summary>
        /// Получает все неактивированные захваты (IsFired = false)
        /// </summary>
        public List<CaptureData> GetAllUnfiredCaptures()
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                return _cachedRoot.Captures.Where(c => !c.IsFired).ToList();
            }
        }

        /// <summary>
        /// Обновляет поле IsFired для конкретного захвата
        /// </summary>
        public void UpdateCaptureIsFired(CaptureData capture, bool isFired = true)
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                var existingCapture = _cachedRoot.Captures.FirstOrDefault(c => c.ID == capture.ID);
                
                if (existingCapture != null)
                {
                    existingCapture.IsFired = isFired;
                    SaveRoot(_cachedRoot);
                    Logger.LogInfo($"CaptureTrackingService: Updated IsFired={isFired} for capture ID={capture.ID}, Handle={capture.Handle}");
                }
                else
                {
                    Logger.LogWarning($"CaptureTrackingService: Capture not found for update IsFired. ID={capture.ID}, Handle={capture.Handle}");
                }
            }
        }

        /// <summary>
        /// Обновляет поле IsSkipped для конкретного захвата
        /// </summary>
        public void UpdateCaptureIsSkipped(CaptureData capture, bool isSkipped = true)
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                var existingCapture = _cachedRoot.Captures.FirstOrDefault(c => c.ID == capture.ID);
                
                if (existingCapture != null)
                {
                    existingCapture.IsSkipped = isSkipped;
                    SaveRoot(_cachedRoot);
                    Logger.LogInfo($"CaptureTrackingService: Updated IsSkipped={isSkipped} for capture ID={capture.ID}, Handle={capture.Handle}");
                }
                else
                {
                    Logger.LogWarning($"CaptureTrackingService: Capture not found for update IsSkipped. ID={capture.ID}, Handle={capture.Handle}");
                }
            }
        }

        /// <summary>
        /// Получает все неактивированные и не пропущенные захваты (IsFired = false и IsSkipped = false)
        /// </summary>
        public List<CaptureData> GetAllUnfiredAndUnskippedCaptures()
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                return _cachedRoot.Captures.Where(c => !c.IsFired && !c.IsSkipped).ToList();
            }
        }

        /// <summary>
        /// Получает все активированные захваты (IsFired = true)
        /// </summary>
        public List<CaptureData> GetAllFiredCaptures()
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                return _cachedRoot.Captures.Where(c => c.IsFired).ToList();
            }
        }

        /// <summary>
        /// Получает все пропущенные захваты (IsSkipped = true)
        /// </summary>
        public List<CaptureData> GetAllSkippedCaptures()
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                return _cachedRoot.Captures.Where(c => c.IsSkipped).ToList();
            }
        }

        /// <summary>
        /// Получает все захваты для трекинга (IsFired = false и IsSkipped = false)
        /// </summary>
        public List<CaptureData> GetTrackingCaptures()
        {
            return GetAllUnfiredAndUnskippedCaptures();
        }

        /// <summary>
        /// Получает все захваты с Source = "trading_canvas" для указанного handle
        /// </summary>
        public List<CaptureData> GetTradingCanvasCapturesByHandle(long handle)
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                return _cachedRoot.Captures.Where(c => c.Source == "trading_canvas" && c.Handle == handle).ToList();
            }
        }

        /// <summary>
        /// Помечает все захваты с Source = "trading_canvas" для указанного handle как пропущенные
        /// </summary>
        public void MarkTradingCanvasCapturesAsSkipped(long handle)
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                var capturesToUpdate = _cachedRoot.Captures.Where(c => c.Source == "trading_canvas" && c.Handle == handle && !c.IsSkipped && !c.IsFired).ToList();
                
                foreach (var capture in capturesToUpdate)
                {
                    capture.IsSkipped = true;
                }
                
                if (capturesToUpdate.Count > 0)
                {
                    SaveRoot(_cachedRoot);
                    Logger.LogInfo($"DatabaseService: Marked {capturesToUpdate.Count} trading_canvas captures as skipped for handle {handle}");
                }
            }
        }

        /// <summary>
        /// Получает последний захват
        /// </summary>
        public CaptureData GetLastCapture()
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                return _cachedRoot.Captures.Count > 0 ? _cachedRoot.Captures[_cachedRoot.Captures.Count - 1] : null;
            }
        }

        /// <summary>
        /// Получает захваты за определенный период
        /// </summary>
        public List<CaptureData> GetCapturesByDateRange(DateTime startDate, DateTime endDate)
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                var result = new List<CaptureData>();

                foreach (var capture in _cachedRoot.Captures)
                {
                    if (DateTime.TryParse(capture.Timestamp, out DateTime captureDate))
                    {
                        if (captureDate >= startDate && captureDate <= endDate)
                        {
                            result.Add(capture);
                        }
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Обновляет поле Mt4Order для захвата по ID
        /// </summary>
        public void UpdateCaptureMt4Order(string id, string mt4OrderJson)
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                
                // Find capture by ID field
                var capture = _cachedRoot.Captures.FirstOrDefault(c => c.ID == id);
                
                if (capture != null)
                {
                    capture.Mt4Order = mt4OrderJson;
                    SaveRoot(_cachedRoot);
                    Logger.LogInfo($"DatabaseService: Updated Mt4Order for capture ID={capture.ID}, Order data: {mt4OrderJson}");
                }
                else
                {
                    Logger.LogWarning($"DatabaseService: No capture found with ID={id} to update with Mt4Order");
                }
            }
        }

        /// <summary>
        /// Помечает все захваты для трекинга (не сработавшие и не пропущенные) как пропущенные
        /// </summary>
        public void SkipAllTrackingCaptures()
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                var capturesToUpdate = _cachedRoot.Captures.Where(c => !c.IsSkipped && !c.IsFired).ToList();
                
                foreach (var capture in capturesToUpdate)
                {
                    capture.IsSkipped = true;
                }
                
                if (capturesToUpdate.Count > 0)
                {
                    SaveRoot(_cachedRoot);
                    Logger.LogInfo($"DatabaseService: Marked {capturesToUpdate.Count} tracking captures as skipped");
                }
                else
                {
                    Logger.LogInfo("DatabaseService: No tracking captures found to skip");
                }
            }
        }

        #endregion

        #region Symbol Methods

        /// <summary>
        /// Сохраняет символ с настройками риска
        /// </summary>
        public void SaveSymbol(SymbolData symbol)
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                
                // Проверяем, существует ли уже такой символ
                var existingSymbol = _cachedRoot.Symbols.FirstOrDefault(s => s.Symbol == symbol.Symbol);
                if (existingSymbol != null)
                {
                    // Обновляем существующий символ
                    existingSymbol.RiskPercent = symbol.RiskPercent;
                    existingSymbol.IsActive = symbol.IsActive;
                    existingSymbol.LastUpdated = DateTime.Now;
                }
                else
                {
                    // Добавляем новый символ
                    symbol.CreatedAt = DateTime.Now;
                    symbol.LastUpdated = DateTime.Now;
                    _cachedRoot.Symbols.Add(symbol);
                }
                
                SaveRoot(_cachedRoot);
            }
        }

        /// <summary>
        /// Сохраняет массив символов из jforex_symbols
        /// </summary>
        public void SaveSymbolsFromArray(string[] symbols, double defaultRiskPercent = 1.0)
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                
                foreach (var symbolName in symbols)
                {
                    var existingSymbol = _cachedRoot.Symbols.FirstOrDefault(s => s.Symbol == symbolName);
                    if (existingSymbol == null)
                    {
                        // Добавляем новый символ с дефолтным риском
                        var newSymbol = new SymbolData
                        {
                            Symbol = symbolName,
                            RiskPercent = defaultRiskPercent,
                            IsActive = true,
                            CreatedAt = DateTime.Now,
                            LastUpdated = DateTime.Now
                        };
                        _cachedRoot.Symbols.Add(newSymbol);
                    }
                }
                
                SaveRoot(_cachedRoot);
            }
        }

        /// <summary>
        /// Инициализирует символы в базе данных из WindowManagementService
        /// </summary>
        public void InitializeSymbolsFromWindowManagementService(WindowManagementService windowManagementService, double defaultRiskPercent = 1.0)
        {
            try
            {
                var symbols = windowManagementService?.Symbols;
                if (symbols != null && symbols.Count > 0)
                {
                    // Получаем массив ключей символов
                    string[] symbolKeys = symbols.Keys.ToArray();
                    
                    // Сохраняем символы в базу данных с дефолтным риском
                    SaveSymbolsFromArray(symbolKeys, defaultRiskPercent);
                    
                    Logger.LogInfo($"Initialized {symbolKeys.Length} symbols in database");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize symbols in database", ex);
            }
        }

        /// <summary>
        /// Получает все символы
        /// </summary>
        public List<SymbolData> GetAllSymbols()
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                return _cachedRoot.Symbols;
            }
        }

        /// <summary>
        /// Получает активные символы
        /// </summary>
        public List<SymbolData> GetActiveSymbols()
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                return _cachedRoot.Symbols.Where(s => s.IsActive).ToList();
            }
        }

        /// <summary>
        /// Получает символ по имени
        /// </summary>
        public SymbolData GetSymbolByName(string symbolName)
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                return _cachedRoot.Symbols.FirstOrDefault(s => s.Symbol == symbolName);
            }
        }

        /// <summary>
        /// Обновляет риск для символа
        /// </summary>
        public void UpdateSymbolRisk(string symbolName, double riskPercent)
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                var symbol = _cachedRoot.Symbols.FirstOrDefault(s => s.Symbol == symbolName);
                
                if (symbol != null)
                {
                    symbol.RiskPercent = riskPercent;
                    symbol.LastUpdated = DateTime.Now;
                    SaveRoot(_cachedRoot);
                }
            }
        }

        /// <summary>
        /// Активирует/деактивирует символ
        /// </summary>
        public void SetSymbolActive(string symbolName, bool isActive)
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                var symbol = _cachedRoot.Symbols.FirstOrDefault(s => s.Symbol == symbolName);
                
                if (symbol != null)
                {
                    symbol.IsActive = isActive;
                    symbol.LastUpdated = DateTime.Now;
                    SaveRoot(_cachedRoot);
                }
            }
        }

        /// <summary>
        /// Удаляет символ
        /// </summary>
        public void RemoveSymbol(string symbolName)
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                var symbol = _cachedRoot.Symbols.FirstOrDefault(s => s.Symbol == symbolName);
                
                if (symbol != null)
                {
                    _cachedRoot.Symbols.Remove(symbol);
                    SaveRoot(_cachedRoot);
                }
            }
        }

        #endregion

        #region Database Management

        /// <summary>
        /// Очищает все сохраненные захваты, но сохраняет символы
        /// </summary>
        public void ClearAllCaptures()
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                _cachedRoot.Captures.Clear();
                SaveRoot(_cachedRoot);
            }
        }

        /// <summary>
        /// Очищает все данные (захваты и символы)
        /// </summary>
        public void ClearAllData()
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                _cachedRoot = new CaptureDbRoot();
                SaveRoot(_cachedRoot);
            }
        }

        /// <summary>
        /// Получает статистику базы данных
        /// </summary>
        public (int capturesCount, int symbolsCount) GetDatabaseStats()
        {
            lock (_lockObject)
            {
                EnsureInitialized();
                return (_cachedRoot.Captures.Count, _cachedRoot.Symbols.Count);
            }
        }

        #endregion

        /// <summary>
        /// Загружает существующий корень или создает новый
        /// </summary>
        private CaptureDbRoot LoadOrCreateRoot()
        {
            if (File.Exists(_dbPath))
            {
                try
                {
                    string json = File.ReadAllText(_dbPath);
                    var root = JsonSerializer.Deserialize<CaptureDbRoot>(json, _jsonOptions);
                    
                    // Обеспечиваем инициализацию коллекций если они null
                    if (root == null)
                    {
                        root = new CaptureDbRoot();
                    }
                    
                    if (root.Captures == null)
                        root.Captures = new List<CaptureData>();
                    
                    if (root.Symbols == null)
                        root.Symbols = new List<SymbolData>();
                    
                    return root;
                }
                catch 
                { 
                    return new CaptureDbRoot(); 
                }
            }
            return new CaptureDbRoot();
        }

        /// <summary>
        /// Проверяет, что сервис инициализирован, и инициализирует его если нужно
        /// </summary>
        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                Logger.LogWarning("DatabaseService: Auto-initializing service");
                Initialize();
            }
        }

        /// <summary>
        /// Сохраняет корень в файл
        /// </summary>
        private void SaveRoot(CaptureDbRoot root)
        {
            File.WriteAllText(_dbPath, JsonSerializer.Serialize(root, _jsonOptions));
        }
    }
} 