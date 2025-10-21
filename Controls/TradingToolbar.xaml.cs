using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScreenCaptureApp.Models;
using ScreenCaptureApp.Services;
using static ScreenCaptureApp.Services.ToastNotifyService;

namespace ScreenCaptureApp.Controls
{
    public partial class TradingToolbar : UserControl
    {
        public event Action<double> RiskChanged;
        public event Action<BrokerType> BrokerChanged;
        public event Action<int> DurationChanged;

        public double SelectedRisk { get; private set; } = 1;
        public int SelectedDuration { get; private set; } = 2;
        public BrokerType SelectedBroker { get; private set; } = BrokerType.Forex;

        private Mt4SocketService _mt4SocketService;
        private ToolbarSettingsManager _toolbarSettingsManager;
        private ToastNotifyService _toastNotifyService;
        private string _currentSymbol;
        public string CurrentSymbol => _currentSymbol;
        private long _currentHandleID;
        private static int _instanceCounter = 0;
        private readonly int _instanceId;



        public TradingToolbar()
        {
            InitializeComponent();
            _instanceId = ++_instanceCounter;
            Logger.LogTagInfo("TradingToolbar", $"TradingToolbar instance {_instanceId} created");
            
            // Получаем ToolbarSettingsManager из DI контейнера
            try
            {
                _toolbarSettingsManager = ServiceContainer.Instance.GetService<ToolbarSettingsManager>();
                if (_toolbarSettingsManager != null)
                {
                    Logger.LogTagInfo("TradingToolbar", "ToolbarSettingsManager initialized successfully");
                }
                else
                {
                    Logger.LogTagWarning("TradingToolbar", "ToolbarSettingsManager is null - settings will not be saved/loaded");
                }
            }
            catch (Exception ex)
            {
                Logger.LogTagError("TradingToolbar", "Failed to get ToolbarSettingsManager from DI container", ex);
            }

            // Получаем ToastNotifyService из DI контейнера
            try
            {
                _toastNotifyService = ServiceContainer.Instance.GetService<ToastNotifyService>();
                if (_toastNotifyService != null)
                {
                    Logger.LogTagInfo("TradingToolbar", "ToastNotifyService initialized successfully");
                }
                else
                {
                    Logger.LogTagWarning("TradingToolbar", "ToastNotifyService is null - toast notifications will not be shown");
                }
            }
            catch (Exception ex)
            {
                Logger.LogTagError("TradingToolbar", "Failed to get ToastNotifyService from DI container", ex);
            }
            
            HighlightSelectedBroker(SelectedBroker);
            HighlightSelectedRiskButton(SelectedRisk);
            
            // Subscribe to MT4 socket service events
            SubscribeToMt4SocketEvents();
            
                    // Отписываемся от HTTP сервера - логика перенесена в SimpleTradingOverlay
        // SubscribeToHttpServerEvents();
            
            // Wire up order summary button events
            OrderCloseButton.Click += (s, e) => OnOrderCloseClicked();
            OrderBEButton.Click += (s, e) => OnOrderBEClicked();
            OrderTP1Button.Click += (s, e) => OnOrderTP1Clicked();
            OrderTP2Button.Click += (s, e) => OnOrderTP2Clicked();
            OrderTP3Button.Click += (s, e) => OnOrderTP3Clicked();
            OrderINFButton.Click += (s, e) => OnOrderINFClicked();
            
            // Ensure default duration is properly highlighted when panel becomes visible
            DurationPanel.IsVisibleChanged += (s, e) => 
            {
                if (DurationPanel.Visibility == Visibility.Visible)
                {
                    HighlightSelectedDurationButton(SelectedDuration);
                }
            };
            
            // Initial highlight for duration (in case panel is visible)
            HighlightSelectedDurationButton(SelectedDuration);
        }

        private void SubscribeToMt4SocketEvents()
        {
            try
            {
                _mt4SocketService = ServiceContainer.Instance.GetService<Mt4SocketService>();
                if (_mt4SocketService != null)
                {
                    _mt4SocketService.OrdersSummaryReceived += Mt4SocketService_OrdersSummaryReceived;
                }
            }
            catch (Exception ex)
            {
                Logger.LogTagError("TradingToolbar", $"Error subscribing to MT4 socket events: {ex.Message}", ex);
            }
        }



        private void Mt4SocketService_OrdersSummaryReceived(object sender, Mt4SocketService.OrdersSummary summary)
        {
            try
            {
                // Check if we have a current symbol and if it matches any symbol in the summary
                if (string.IsNullOrEmpty(_currentSymbol) || summary?.Symbols == null)
                {
                    HideOrderSummary();
                    return;
                }

                // Ищем символ с учетом возможных суффиксов
                // Сравниваем первые 6 символов текущего символа с первыми 6 символами каждого символа из summary
                var matchingSymbol = summary.Symbols.Find(s => 
                {
                    if (string.IsNullOrEmpty(s.Symbol))
                        return false;

                    // Берем первые 6 символов для сравнения
                    string currentSymbolPrefix = _currentSymbol.Length >= 6 ? _currentSymbol.Substring(0, 6) : _currentSymbol;
                    string summarySymbolPrefix = s.Symbol.Length >= 6 ? s.Symbol.Substring(0, 6) : s.Symbol;

                    return string.Equals(currentSymbolPrefix, summarySymbolPrefix, StringComparison.OrdinalIgnoreCase);
                });

                if (matchingSymbol != null)
                {
                    Logger.LogTagInfo("TradingToolbar", $"Found matching symbol: {_currentSymbol} matches {matchingSymbol.Symbol}");
                    ShowOrderSummary(matchingSymbol);
                }
                else
                {
                    Logger.LogTagInfo("TradingToolbar", $"No matching symbol found for {_currentSymbol}");
                    HideOrderSummary();
                }
            }
            catch (Exception ex)
            {
                Logger.LogTagError("TradingToolbar", $"Error handling orders summary event: {ex.Message}", ex);
                HideOrderSummary();
            }
        }

        private void ShowOrderSummary(Mt4SocketService.OrdersSummary.SymbolInfo symbolInfo)
        {
            if (Dispatcher.CheckAccess())
            {
                UpdateOrderSummaryUI(symbolInfo);
            }
            else
            {
                Dispatcher.Invoke(() => UpdateOrderSummaryUI(symbolInfo));
            }
        }

        private void UpdateOrderSummaryUI(Mt4SocketService.OrdersSummary.SymbolInfo symbolInfo)
        {
            OrderLotsText.Text = $"%: {symbolInfo.Percent:F2}";
            OrderPercentText.Text = $"Lots: {symbolInfo.Lots:F2}";
            OrderPointsText.Text = $"Pts: {symbolInfo.ProfitPoints:F0}";
            
            OrderSummaryPanel.Visibility = Visibility.Visible;
            Logger.LogTagInfo("TradingToolbar", $"OrderSummary panel shown for symbol: {symbolInfo.Symbol}, Lots: {symbolInfo.Lots}, Percent: {symbolInfo.Percent}, Points: {symbolInfo.ProfitPoints}");
        }

        private void HideOrderSummary()
        {
            if (Dispatcher.CheckAccess())
            {
                OrderSummaryPanel.Visibility = Visibility.Collapsed;
                Logger.LogTagInfo("TradingToolbar", "OrderSummary panel hidden");
            }
            else
            {
                Dispatcher.Invoke(() => 
                {
                    OrderSummaryPanel.Visibility = Visibility.Collapsed;
                    Logger.LogTagInfo("TradingToolbar", "OrderSummary panel hidden");
                });
            }
        }











        private void OnOrderCloseClicked()
        {
            if (!string.IsNullOrEmpty(_currentSymbol))
            {
                // Используем текущий символ
                string symbolToClose = _currentSymbol;
                CloseSymbolOrder(symbolToClose);
            }
        }

        private void OnOrderBEClicked()
        {
            if (!string.IsNullOrEmpty(_currentSymbol))
            {
                Logger.LogTagInfo("TradingToolbar", $"BE clicked for {_currentSymbol}");
                // TODO: Implement BE functionality
            }
        }

        private void OnOrderTP1Clicked()
        {
            if (!string.IsNullOrEmpty(_currentSymbol))
            {
                Logger.LogTagInfo("TradingToolbar", $"TP1 clicked for {_currentSymbol}");
                // TODO: Implement TP1 functionality
            }
        }

        private void OnOrderTP2Clicked()
        {
            if (!string.IsNullOrEmpty(_currentSymbol))
            {
                Logger.LogTagInfo("TradingToolbar", $"TP2 clicked for {_currentSymbol}");
                // TODO: Implement TP2 functionality
            }
        }

        private void OnOrderTP3Clicked()
        {
            if (!string.IsNullOrEmpty(_currentSymbol))
            {
                Logger.LogTagInfo("TradingToolbar", $"TP3 clicked for {_currentSymbol}");
                // TODO: Implement TP3 functionality
            }
        }

        private void OnOrderINFClicked()
        {
            if (!string.IsNullOrEmpty(_currentSymbol))
            {
                Logger.LogTagInfo("TradingToolbar", $"INF clicked for {_currentSymbol}");
                // TODO: Implement INF functionality
            }
        }

        private void CloseSymbolOrder(string symbol)
        {
            try
            {
                if (_mt4SocketService != null && !string.IsNullOrEmpty(symbol))
                {
                    // Send close_positions command for the symbol
                    var cmd = $"{{\"cmd\":\"close_positions\",\"symbol\":\"{symbol}\"}}\r\n";
                    _ = _mt4SocketService.WriteAsync(cmd);
                    Logger.LogTagInfo("TradingToolbar", $"Sent close_positions command for {symbol}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogTagError("TradingToolbar", $"Error closing symbol order: {ex.Message}", ex);
            }
        }

        public void SetModeLabel(string text, bool visible)
        {
            // Этот метод больше не используется, так как убрали ModeLabel
        }

        public void SetSymbol(string symbol)
        {
            _currentSymbol = symbol;
            
            if (!string.IsNullOrEmpty(symbol))
            {
                SymbolLabel.Text = symbol;
                SymbolLabel.Visibility = Visibility.Visible;
                
                // Отправляем команду открытия символа только для binary-брокеров
                Logger.LogTagInfo("TradingToolbar", $"SetSymbol called for {symbol}, current broker: {SelectedBroker}");
                if (SelectedBroker != BrokerType.Forex)
                {
                    Logger.LogTagInfo("TradingToolbar", $"Sending open symbol command for broker {SelectedBroker}");
                    SendOpenSymbolCommand(symbol);
                }
                else
                {
                    Logger.LogTagInfo("TradingToolbar", $"Skipping open symbol command - Forex broker selected");
                }
            }
            else
            {
                SymbolLabel.Text = "UNKNOWN";
                SymbolLabel.Visibility = Visibility.Visible;
            }
            
            // Hide order summary when symbol changes
            HideOrderSummary();
            
            // Hide prices when symbol changes
            HidePrices();
        }

        /// <summary>
        /// Устанавливает Entry Level и показывает его в UI
        /// </summary>
        /// <param name="entryPrice">Цена входа</param>
        /// <param name="symbol">Торговый символ</param>
        public void SetEntryLevel(double entryPrice, string symbol)
        {
            try
            {
                Logger.LogTagInfo("TradingToolbar", $"SetEntryLevel called: {entryPrice} for {symbol}");
                                
                // Обновляем UI
                ShowPrices((double)entryPrice, 0, "Waiting for Stop Loss");
                
                Logger.LogTagInfo("TradingToolbar", $"Entry Level set successfully: {entryPrice} for {symbol}");
            }
            catch (Exception ex)
            {
                Logger.LogTagError("TradingToolbar", "Error setting Entry Level", ex);
            }
        }

        public void SetHandleID(long handleID)
        {
            _currentHandleID = handleID;
            HandleIDLabel.Text = $"HandleID: {handleID}";
            // Загружаем настройки из ToolbarSettingsManager
            if (_toolbarSettingsManager != null)
            {
                var settings = _toolbarSettingsManager.GetSettings(_currentHandleID);
                if (settings != null)
                {
                    SelectedRisk = settings.Risk;
                    SelectedDuration = settings.Duration;
                    SelectedBroker = settings.Broker;
                    HighlightSelectedRiskButton(SelectedRisk);
                    HighlightSelectedDurationButton(SelectedDuration);
                    HighlightSelectedBroker(SelectedBroker);
                    Logger.LogTagInfo("TradingToolbar", $"Loaded settings for HandleID {handleID}: Risk={settings.Risk}, Duration={settings.Duration}, Broker={settings.Broker}");
                }
                else
                {
                    // Создаем настройки по умолчанию
                    _toolbarSettingsManager.UpdateSettings(_currentHandleID, SelectedRisk, SelectedDuration, SelectedBroker);
                    Logger.LogTagInfo("TradingToolbar", $"Created default settings for HandleID {handleID}: Risk={SelectedRisk}, Duration={SelectedDuration}, Broker={SelectedBroker}");
                }
            }
        }

        /// <summary>
        /// Устанавливает брокера извне (например, при переключении окон)
        /// </summary>
        public void SetBroker(BrokerType broker)
        {
            Logger.LogTagInfo("TradingToolbar", $"SetBroker called - changing from {SelectedBroker} to {broker}");
            SelectedBroker = broker;
            HighlightSelectedBroker(broker);
            
            // Сохраняем настройки в ToolbarSettingsManager
            if (_toolbarSettingsManager != null && _currentHandleID != 0)
            {
                _toolbarSettingsManager.UpdateSettings(_currentHandleID, broker: broker);
                Logger.LogTagInfo("TradingToolbar", $"Saved broker setting {broker} for HandleID {_currentHandleID}");
            }
            
            Logger.LogTagInfo("TradingToolbar", $"SelectedBroker updated to {SelectedBroker}");
        }

        public void ShowDurationPanel(bool show)
        {
            DurationPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            
            // Ensure proper highlighting when showing the panel
            if (show)
            {
                HighlightSelectedDurationButton(SelectedDuration);
            }
        }

        private void RiskButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null && double.TryParse(btn.Tag.ToString(), out double val))
            {
                Logger.LogTagInfo("TradingToolbar", $"Risk button clicked - value: {val}, current broker: {SelectedBroker}");
                SelectedRisk = val;
                HighlightSelectedRiskButton(val);
                RiskChanged?.Invoke(val);
                
                // Сохраняем настройки в ToolbarSettingsManager
                if (_toolbarSettingsManager != null && _currentHandleID != 0)
                {
                    _toolbarSettingsManager.UpdateSettings(_currentHandleID, risk: val);
                    Logger.LogTagInfo("TradingToolbar", $"Saved risk setting {val} for HandleID {_currentHandleID}");
                }
                
                // Отправляем команду в binary-сокет только для binary-брокеров
                if (SelectedBroker != BrokerType.Forex)
                {
                    Logger.LogTagInfo("TradingToolbar", $"Sending risk command for broker {SelectedBroker}");
                    SendRiskCommandToBinarySocket(val);
                }
                else
                {
                    Logger.LogTagInfo("TradingToolbar", $"Skipping risk command - Forex broker selected");
                }
            }
        }

        private void DurationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag.ToString(), out int val))
            {
                Logger.LogTagInfo("TradingToolbar", $"Duration button clicked - value: {val}, current broker: {SelectedBroker}");
                SelectedDuration = val;
                HighlightSelectedDurationButton(val);
                DurationChanged?.Invoke(val);
                
                // Сохраняем настройки в ToolbarSettingsManager
                if (_toolbarSettingsManager != null && _currentHandleID != 0)
                {
                    _toolbarSettingsManager.UpdateSettings(_currentHandleID, duration: val);
                    Logger.LogTagInfo("TradingToolbar", $"Saved duration setting {val} for HandleID {_currentHandleID}");
                }
                
                // Отправляем команду в binary-сокет только для binary-брокеров
                if (SelectedBroker != BrokerType.Forex)
                {
                    Logger.LogTagInfo("TradingToolbar", $"Sending duration command for broker {SelectedBroker}");
                    SendDurationCommandToBinarySocket(val);
                }
                else
                {
                    Logger.LogTagInfo("TradingToolbar", $"Skipping duration command - Forex broker selected");
                }
            }
        }

        private void BrokerButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                BrokerType oldType = SelectedBroker;
                BrokerType newType = BrokerType.Forex;
                switch (btn.Tag.ToString())
                {
                    case "Forex": newType = BrokerType.Forex; break;
                    case "PocketOption": newType = BrokerType.PocketOption; break;
                    case "Binarium": newType = BrokerType.Binarium; break;
                    case "Quotex": newType = BrokerType.Quotex; break;
                }
                
                Logger.LogTagInfo("TradingToolbar", $"Broker button clicked - changing from {oldType} to {newType}");
                SelectedBroker = newType;
                HighlightSelectedBroker(newType);
                BrokerChanged?.Invoke(newType);
                
                // Сохраняем настройки в ToolbarSettingsManager
                if (_toolbarSettingsManager != null && _currentHandleID != 0)
                {
                    _toolbarSettingsManager.UpdateSettings(_currentHandleID, broker: newType);
                    Logger.LogTagInfo("TradingToolbar", $"Saved broker setting {newType} for HandleID {_currentHandleID}");
                }
                
                Logger.LogTagInfo("TradingToolbar", $"SelectedBroker updated to {SelectedBroker}");
            }
        }

        /// <summary>
        /// Отправляет команду установки риска в binary-сокет для binary-брокеров
        /// </summary>
        private async void SendRiskCommandToBinarySocket(double risk)
        {
            try
            {
                // Temporarily disabled
                // var binarySocketService = ServiceContainer.Instance.GetService<BinaryOptionsSocketService>();
                // if (binarySocketService != null)
                // {
                //     string brokerName = SelectedBroker.ToString();
                //     bool success = await binarySocketService.SetRisk(brokerName, risk);
                //     
                //     if (!success)
                //     {
                //         Logger.LogTagWarning("TradingToolbar", $"Failed to set risk {risk} for broker {brokerName}");
                //     }
                // }
                // else
                // {
                //     Logger.LogTagWarning("TradingToolbar", "Binary socket service not available for risk command");
                // }
            }
            catch (Exception ex)
            {
                Logger.LogTagError("TradingToolbar", $"Error setting risk via binary socket service: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Отправляет команду установки duration в binary-сокет для binary-брокеров
        /// </summary>
        private async void SendDurationCommandToBinarySocket(int duration)
        {
            try
            {
                // Temporarily disabled
                // var binarySocketService = ServiceContainer.Instance.GetService<BinaryOptionsSocketService>();
                // if (binarySocketService != null)
                // {
                //     string brokerName = SelectedBroker.ToString();
                //     bool success = await binarySocketService.SetDuration(brokerName, duration);
                //     
                //     if (!success)
                //     {
                //         Logger.LogTagWarning("TradingToolbar", $"Failed to set duration {duration} for broker {brokerName}");
                //     }
                // }
                // else
                // {
                //     Logger.LogTagWarning("TradingToolbar", "Binary socket service not available for duration command");
                // }
            }
            catch (Exception ex)
            {
                Logger.LogTagError("TradingToolbar", $"Error setting duration via binary socket service: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Отправляет команду открытия символа в binary-сокет для binary-брокеров
        /// </summary>
        private async void SendOpenSymbolCommand(string symbolName)
        {
            Logger.LogTagInfo("TradingToolbar", $"SendOpenSymbolCommand: Starting to send command for symbol {symbolName}, broker {SelectedBroker}");
            try
            {
                // Temporarily disabled
                // var binarySocketService = ServiceContainer.Instance.GetService<BinaryOptionsSocketService>();
                // if (binarySocketService != null)
                // {
                //     string brokerName = SelectedBroker.ToString();
                //     Logger.LogTagInfo("TradingToolbar", $"SendOpenSymbolCommand: Calling binarySocketService.OpenSymbol({brokerName}, {symbolName})");
                //     bool success = await binarySocketService.OpenSymbol(brokerName, symbolName);
                //     
                //     if (!success)
                //     {
                //         Logger.LogTagWarning("TradingToolbar", $"Failed to open symbol {symbolName} for broker {brokerName}");
                //     }
                //     else
                //     {
                //         Logger.LogTagInfo("TradingToolbar", $"Successfully sent open symbol command for {symbolName} to {brokerName}");
                //     }
                // }
                // else
                // {
                //     Logger.LogTagWarning("TradingToolbar", "Binary socket service not available for open symbol command");
                // }
            }
            catch (Exception ex)
            {
                Logger.LogTagError("TradingToolbar", $"Error opening symbol via binary socket service: {ex.Message}", ex);
            }
        }

        public void HighlightSelectedRiskButton(double risk)
        {
            foreach (var child in RiskPanelTop.Children)
            {
                if (child is Button btn && double.TryParse(btn.Tag?.ToString(), out double val))
                {
                    btn.Background = (val == risk) ? Brushes.Orange : Brushes.White;
                }
            }
        }

        public void HighlightSelectedDurationButton(int duration)
        {
            foreach (var child in DurationPanel.Children)
            {
                if (child is Button btn && int.TryParse(btn.Tag?.ToString(), out int val))
                {
                    btn.Background = (val == duration) ? Brushes.Orange : Brushes.LightGray;
                }
            }
        }

        public void HighlightSelectedBroker(BrokerType broker)
        {
            // Сбрасываем все кнопки
            FXButton.Background = Brushes.White;
            POButton.Background = Brushes.White;
            BINButton.Background = Brushes.White;
            QOButton.Background = Brushes.White;

            // Выделяем выбранную кнопку
            switch (broker)
            {
                case BrokerType.Forex:
                    FXButton.Background = Brushes.Orange;
                    break;
                case BrokerType.PocketOption:
                    POButton.Background = Brushes.Orange;
                    break;
                case BrokerType.Binarium:
                    BINButton.Background = Brushes.Orange;
                    break;
                case BrokerType.Quotex:
                    QOButton.Background = Brushes.Orange;
                    break;
            }
        }

        /// <summary>
        /// Отображает цены входа и стоп-лосса на панели инструментов
        /// </summary>
        /// <param name="entryPrice">Цена входа</param>
        /// <param name="stopLossPrice">Цена стоп-лосса (0 если еще не распознана)</param>
        /// <param name="tradeType">Тип сделки (BUY/SELL)</param>
        public void ShowPrices(double entryPrice, double stopLossPrice, string tradeType = "")
        {
            if (Dispatcher.CheckAccess())
            {
                UpdatePricesUI(entryPrice, stopLossPrice, tradeType);
            }
            else
            {
                Dispatcher.Invoke(() => UpdatePricesUI(entryPrice, stopLossPrice, tradeType));
            }
        }

        /// <summary>
        /// Скрывает панель с ценами
        /// </summary>
        public void HidePrices()
        {
            if (Dispatcher.CheckAccess())
            {
                PriceDisplayPanel.Visibility = Visibility.Collapsed;
                Logger.LogTagInfo("TradingToolbar", "Price display panel hidden");
            }
            else
            {
                Dispatcher.Invoke(() => 
                {
                    PriceDisplayPanel.Visibility = Visibility.Collapsed;
                    Logger.LogTagInfo("TradingToolbar", "Price display panel hidden");
                });
            }
        }

        /// <summary>
        /// Обновляет UI с ценами
        /// </summary>
        /// <param name="entryPrice">Цена входа</param>
        /// <param name="stopLossPrice">Цена стоп-лосса (0 если еще не распознана)</param>
        /// <param name="tradeType">Тип сделки (BUY/SELL)</param>
        private void UpdatePricesUI(double entryPrice, double stopLossPrice, string tradeType)
        {
            // Обновляем тип сделки
            if (!string.IsNullOrEmpty(tradeType))
            {
                TradeTypeLabel.Text = tradeType;
                TradeTypeLabel.Foreground = tradeType == "BUY" ? Brushes.LightGreen : Brushes.LightCoral;
                TradeTypeLabel.Visibility = Visibility.Visible;
            }
            else
            {
                TradeTypeLabel.Visibility = Visibility.Collapsed;
            }
            
            EntryPriceLabel.Text = $"Entry: {entryPrice:F5}";
            
            if (stopLossPrice > 0)
            {
                StopLossPriceLabel.Text = $"SL: {stopLossPrice:F5}";
                StopLossPriceLabel.Visibility = Visibility.Visible;
            }
            else
            {
                StopLossPriceLabel.Text = "SL: ---";
                StopLossPriceLabel.Visibility = Visibility.Visible;
            }
            
            PriceDisplayPanel.Visibility = Visibility.Visible;
            
            Logger.LogTagInfo("TradingToolbar", $"Price display updated: Entry={entryPrice:F5}, StopLoss={stopLossPrice:F5}, TradeType={tradeType}");
        }
    }
} 