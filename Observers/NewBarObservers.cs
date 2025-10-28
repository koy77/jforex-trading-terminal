using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using ScreenCaptureApp.Models;
using ScreenCaptureApp.Services;
using ScreenCaptureApp.Helpers;

namespace ScreenCaptureApp.Observers
{
    /// <summary>
    /// Наблюдатель для сдвига торговых штрихов в базе данных при получении нового бара
    /// </summary>
    public class TradingStrokesNewBarObserver : INewBarObserver
    {
        public string ObserverId => "TradingStrokesNewBarObserver";

        private readonly DatabaseService _databaseService;

        public TradingStrokesNewBarObserver()
        {
            _databaseService = ServiceContainer.Instance.GetService<DatabaseService>();
        }

        /// <summary>
        /// Обрабатывает событие нового бара - сдвигает торговые штрихи в базе данных
        /// </summary>
        public void OnNewBarReceived(NewBarEvent newBarEvent)
        {
            try
            {
                if (newBarEvent == null)
                {
                    Logger.LogWarning("Cannot shift trading strokes - NewBarEvent is null");
                    return;
                }

                // Извлекаем данные из NewBarEvent
                string symbol = newBarEvent.Symbol;
                string feedType = newBarEvent.FeedType;
                int? tickBarSize = newBarEvent.TickBarSize;

                if (string.IsNullOrEmpty(symbol))
                {
                    Logger.LogWarning("Cannot shift trading strokes - symbol is null or empty");
                    return;
                }

                if (_databaseService == null)
                {
                    Logger.LogWarning("Cannot shift trading strokes - DatabaseService is not available");
                    return;
                }

                // Получаем только отслеживаемые CaptureData с Source="trading_canvas"
                var tradingCaptures = _databaseService.GetTrackingCaptures();

                Logger.LogInfo($"Found {tradingCaptures.Count} trading canvas captures to check for shifting");

                int shiftedCount = 0;

                foreach (var capture in tradingCaptures)
                {
                    try
                    {
                        // Проверяем символ
                        if (string.IsNullOrEmpty(capture.Symbol) || !capture.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // Проверяем период для тиковых баров
                        bool shouldShift = false;
                        
                        if (newBarEvent.IsTickBar)
                        {
                            // Для тиковых баров проверяем, что период начинается с "T" и содержит размер тика
                            if (!string.IsNullOrEmpty(capture.Period) && 
                                capture.Period.StartsWith("T", StringComparison.OrdinalIgnoreCase) &&
                                tickBarSize.HasValue)
                            {
                                // Извлекаем число из периода (например, "T89" -> 89)
                                string periodNumber = capture.Period.Substring(1);
                                if (int.TryParse(periodNumber, out int periodTickSize) && periodTickSize == tickBarSize.Value)
                                {
                                    shouldShift = true;
                                    Logger.LogDebug($"TICK_BAR match: Symbol={symbol}, Period={capture.Period}, TickSize={tickBarSize}");
                                }
                            }
                        }
                        else
                        {
                            // Для обычных периодов просто проверяем символ
                            shouldShift = true;
                            Logger.LogDebug($"Regular period match: Symbol={symbol}, FeedType={feedType}");
                        }

                        if (shouldShift)
                        {
                            // Сдвигаем координаты влево
                            int newX = capture.X - (int)CanvasConstants.NEW_BAR_SHIFT_AMOUNT;
                            
                            // Обновляем координаты в базе данных
                            capture.X = newX;
                            _databaseService.UpdateCapture(capture);
                            
                            shiftedCount++;
                            Logger.LogDebug($"Shifted trading stroke: ID={capture.ID}, OldX={capture.X + (int)CanvasConstants.NEW_BAR_SHIFT_AMOUNT}, NewX={newX}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error processing capture ID={capture.ID}: {ex.Message}", ex);
                    }
                }

                Logger.LogInfo($"Shifted {shiftedCount} trading strokes for symbol '{symbol}' due to new bar");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error shifting trading strokes in database: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Наблюдатель для сдвига штрихов в CanvasWindow при получении нового бара
    /// </summary>
    public class CanvasNewBarObserver : INewBarObserver
    {
        public string ObserverId => "CanvasNewBarObserver";

        private readonly Dispatcher _dispatcher;
        private readonly Action<NewBarEvent> _shiftStrokesAction;
        private readonly Func<string> _getActiveSymbol;
        private readonly Func<IntPtr> _getTargetWindowHandle;

        /// <summary>
        /// Конструктор для CanvasNewBarObserver
        /// </summary>
        /// <param name="dispatcher">Dispatcher для выполнения операций в UI потоке</param>
        /// <param name="shiftStrokesAction">Действие для сдвига штрихов</param>
        /// <param name="getActiveSymbol">Функция получения активного символа</param>
        /// <param name="getTargetWindowHandle">Функция получения handle целевого окна</param>
        public CanvasNewBarObserver(
            Dispatcher dispatcher,
            Action<NewBarEvent> shiftStrokesAction,
            Func<string> getActiveSymbol,
            Func<IntPtr> getTargetWindowHandle)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _shiftStrokesAction = shiftStrokesAction ?? throw new ArgumentNullException(nameof(shiftStrokesAction));
            _getActiveSymbol = getActiveSymbol ?? throw new ArgumentNullException(nameof(getActiveSymbol));
            _getTargetWindowHandle = getTargetWindowHandle ?? throw new ArgumentNullException(nameof(getTargetWindowHandle));
        }

        /// <summary>
        /// Обрабатывает событие нового бара - сдвигает штрихи в CanvasWindow
        /// </summary>
        public void OnNewBarReceived(NewBarEvent newBarEvent)
        {
            try
            {
                if (newBarEvent != null)
                {
                    // Проверяем, совпадает ли символ с активным символом CanvasWindow
                    string activeSymbol = _getActiveSymbol();
                    if (!string.IsNullOrEmpty(activeSymbol) && newBarEvent.MatchesSymbol(activeSymbol))
                    {
                        // Дополнительная проверка заголовка окна для тиковых баров
                        bool shouldShift = true;
                        
                        if (newBarEvent.IsTickBar)
                        {
                            // Для тиковых баров проверяем, что в заголовке окна есть буква T
                            IntPtr targetWindowHandle = _getTargetWindowHandle();
                            string windowTitle = MainHelper.GetWindowTitle(targetWindowHandle);
                            if (string.IsNullOrEmpty(windowTitle) || !windowTitle.Contains("T"))
                            {
                                Logger.LogDebug($"NewBar is TICK_BAR but window title '{windowTitle}' does not contain 'T' - ignoring");
                                shouldShift = false;
                            }
                            else
                            {
                                Logger.LogInfo($"Window title '{windowTitle}' contains 'T' - proceeding with TICK_BAR shift");
                            }
                        }

                        if (shouldShift)
                        {
                            Logger.LogInfo($"NewBar received for matching symbol '{newBarEvent.Symbol}' - shifting strokes left by {CanvasConstants.NEW_BAR_SHIFT_AMOUNT} pixels");
                            
                            // Выполняем сдвиг штрихов в UI потоке
                            _dispatcher.Invoke(() => {
                                _shiftStrokesAction(newBarEvent);
                            });
                            
                            Logger.LogInfo($"Strokes shifted left for symbol '{newBarEvent.Symbol}' due to new bar");
                        }
                    }
                    else
                    {
                        Logger.LogDebug($"NewBar symbol '{newBarEvent.Symbol}' does not match active symbol '{activeSymbol}' - ignoring");
                    }
                }
                else
                {
                    Logger.LogWarning("Received NewBar event with null data");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error handling NewBar event: {ex.Message}", ex);
            }
        }
    }
}
