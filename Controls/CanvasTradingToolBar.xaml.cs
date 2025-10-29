using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScreenCaptureApp.Services;
using ScreenCaptureApp.Models;
using static ScreenCaptureApp.Services.ToastNotifyService;

namespace ScreenCaptureApp.Controls
{
    public partial class CanvasTradingToolBar : UserControl
    {
        public event Action OnSecButtonClicked;

        private Mt4SocketService _mt4SocketService;
        private ToastNotifyService _toastNotifyService;
        private string _currentSymbol;

        public CanvasTradingToolBar()
        {
            InitializeComponent();
            
            // Получаем сервисы из DI контейнера
            try
            {
                _mt4SocketService = ServiceContainer.Instance.GetService<Mt4SocketService>();
                _toastNotifyService = ServiceContainer.Instance.GetService<ToastNotifyService>();
                
                Logger.LogInfo("CanvasTradingToolBar: Services initialized");
            }
            catch (Exception ex)
            {
                Logger.LogError($"CanvasTradingToolBar: Error initializing services: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Устанавливает текущий символ для фильтрации открытых сделок
        /// </summary>
        public void SetSymbol(string symbol)
        {
            _currentSymbol = symbol;
            Logger.LogInfo($"CanvasTradingToolBar: Symbol set to '{symbol}'");
            
            // Подписываемся на события MT4 при установке символа
            SubscribeToMt4SocketEvents();
            
            // Скрываем тулбар при смене символа
            this.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Подписывается на события MT4 Socket Service
        /// </summary>
        private void SubscribeToMt4SocketEvents()
        {
            try
            {
                if (_mt4SocketService != null)
                {
                    // Отписываемся от предыдущих событий если были подписаны
                    _mt4SocketService.OrdersSummaryReceived -= Mt4SocketService_OrdersSummaryReceived;
                    
                    // Подписываемся на новые события
                    _mt4SocketService.OrdersSummaryReceived += Mt4SocketService_OrdersSummaryReceived;
                    Logger.LogInfo($"CanvasTradingToolBar: Subscribed to MT4 socket events for symbol '{_currentSymbol}'");
                }
                else
                {
                    Logger.LogWarning("CanvasTradingToolBar: MT4SocketService is null - cannot subscribe to events");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"CanvasTradingToolBar: Error subscribing to MT4 socket events: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Обработчик события orders_summary от Mt4SocketService
        /// </summary>
        private void Mt4SocketService_OrdersSummaryReceived(object sender, Mt4SocketService.OrdersSummary summary)
        {
            try
            {
                Logger.LogInfo($"CanvasTradingToolBar: Received orders_summary event. Current symbol: '{_currentSymbol}', Summary symbols count: {summary?.Symbols?.Count ?? 0}");
                
                // Проверяем, есть ли текущий символ
                if (string.IsNullOrEmpty(_currentSymbol) || summary?.Symbols == null)
                {
                    Logger.LogInfo($"CanvasTradingToolBar: Hiding toolbar - no current symbol or empty summary");
                    Dispatcher.Invoke(() => this.Visibility = Visibility.Collapsed);
                    return;
                }

                // Ищем символ с учетом возможных суффиксов
                // Сравниваем первые 6 символов текущего символа с первыми 6 символами каждого символа из summary
                var matchingSymbol = summary.Symbols.Find(s => 
                {
                    if (string.IsNullOrEmpty(s.Symbol))
                        return false;

                    // Берем первые 6 символов для сравнения
                    string currentSymbolBase = _currentSymbol.Length >= 6 ? _currentSymbol.Substring(0, 6) : _currentSymbol;
                    string summarySymbolBase = s.Symbol.Length >= 6 ? s.Symbol.Substring(0, 6) : s.Symbol;

                    bool matches = string.Equals(currentSymbolBase, summarySymbolBase, StringComparison.OrdinalIgnoreCase);
                    if (matches)
                    {
                        Logger.LogInfo($"CanvasTradingToolBar: Found matching symbol: '{currentSymbolBase}' matches '{summarySymbolBase}'");
                    }
                    return matches;
                });

                // Обновляем UI в UI потоке
                Dispatcher.Invoke(() =>
                {
                    if (matchingSymbol != null)
                    {
                        // Есть открытые сделки для текущего символа - показываем тулбар
                        UpdateToolbarData(matchingSymbol);
                        this.Visibility = Visibility.Visible;
                        Logger.LogInfo($"CanvasTradingToolBar: Showing toolbar for symbol '{_currentSymbol}' with data: Percent={matchingSymbol.Percent:F1}%, Lots={matchingSymbol.Lots:F2}, Points={matchingSymbol.ProfitPoints:F1}");
                    }
                    else
                    {
                        // Нет открытых сделок для текущего символа - скрываем тулбар
                        this.Visibility = Visibility.Collapsed;
                        Logger.LogInfo($"CanvasTradingToolBar: No matching symbol found for '{_currentSymbol}' - hiding toolbar");
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"CanvasTradingToolBar: Error processing orders summary: {ex.Message}", ex);
                Dispatcher.Invoke(() => this.Visibility = Visibility.Collapsed);
            }
        }

        /// <summary>
        /// Обновляет данные в тулбаре
        /// </summary>
        private void UpdateToolbarData(Mt4SocketService.OrdersSummary.SymbolInfo symbolInfo)
        {
            try
            {
                // Обновляем UI элементы в UI потоке
                Dispatcher.Invoke(() =>
                {
                    // Обновляем процент с цветовой индикацией
                    PercentText.Text = $"{symbolInfo.Percent:F1}%";
                    PercentText.Foreground = symbolInfo.Percent >= 0 ? Brushes.LimeGreen : Brushes.Red;
                    
                    // Обновляем пункты (всегда желтым цветом)
                    PointsText.Text = $"{symbolInfo.ProfitPoints:F1}pts";
                    PointsText.Foreground = Brushes.Yellow;
                });
                
                Logger.LogDebug($"CanvasTradingToolBar: Updated toolbar data - Percent: {symbolInfo.Percent:F1}%, Lots: {symbolInfo.Lots:F2}, Points: {symbolInfo.ProfitPoints:F1}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"CanvasTradingToolBar: Error updating toolbar data: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Обработчик клика по кнопке BE
        /// </summary>
        private void BeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Logger.LogInfo("CanvasTradingToolBar: BE button clicked");
                // TODO: Implement BE functionality
                // Пока что просто логируем клик
            }
            catch (Exception ex)
            {
                Logger.LogError($"CanvasTradingToolBar: Error handling BE button click: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Обработчик клика по кнопке Sec
        /// </summary>
        private void SecButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Logger.LogInfo("CanvasTradingToolBar: Sec button clicked");
                
                // Показываем уведомление
                _toastNotifyService?.ShowToast("Sec button clicked!", ToastType.Info);
                
                // Вызываем событие
                OnSecButtonClicked?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.LogError($"CanvasTradingToolBar: Error handling Sec button click: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Скрывает тулбар
        /// </summary>
        public void Hide()
        {
            this.Visibility = Visibility.Collapsed;
            Logger.LogDebug("CanvasTradingToolBar: Hidden");
        }

        /// <summary>
        /// Показывает тулбар
        /// </summary>
        public void Show()
        {
            this.Visibility = Visibility.Visible;
            Logger.LogDebug("CanvasTradingToolBar: Shown");
        }

        /// <summary>
        /// Освобождает ресурсы
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (_mt4SocketService != null)
                {
                    _mt4SocketService.OrdersSummaryReceived -= Mt4SocketService_OrdersSummaryReceived;
                    Logger.LogInfo("CanvasTradingToolBar: Unsubscribed from MT4 socket events");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"CanvasTradingToolBar: Error disposing: {ex.Message}", ex);
            }
        }
    }
}
