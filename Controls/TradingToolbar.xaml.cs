using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScreenCaptureApp.Models;
using ScreenCaptureApp.Services;

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
        private string _currentSymbol;

        public TradingToolbar()
        {
            InitializeComponent();
            HighlightSelectedBroker(SelectedBroker);
            HighlightSelectedRiskButton(SelectedRisk);
            HighlightSelectedDurationButton(SelectedDuration);
            
            // Subscribe to MT4 socket service events
            SubscribeToMt4SocketEvents();
            
            // Wire up order summary button events
            OrderCloseButton.Click += (s, e) => OnOrderCloseClicked();
            OrderBEButton.Click += (s, e) => OnOrderBEClicked();
            OrderTP1Button.Click += (s, e) => OnOrderTP1Clicked();
            OrderTP2Button.Click += (s, e) => OnOrderTP2Clicked();
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
                Logger.LogError($"Error subscribing to MT4 socket events: {ex.Message}", ex);
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
                    Logger.LogInfo($"Found matching symbol: {_currentSymbol} matches {matchingSymbol.Symbol}");
                    ShowOrderSummary(matchingSymbol);
                }
                else
                {
                    Logger.LogInfo($"No matching symbol found for {_currentSymbol}");
                    HideOrderSummary();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error handling orders summary event: {ex.Message}", ex);
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
            OrderSymbolText.Text = symbolInfo.Symbol;
            OrderLotsText.Text = $"Lots: {symbolInfo.Lots:F2}";
            OrderPercentText.Text = $"%: {symbolInfo.Percent:F2}";
            OrderPointsText.Text = $"Pts: {symbolInfo.ProfitPoints:F0}";
            
            OrderSummaryPanel.Visibility = Visibility.Visible;
            Logger.LogInfo($"OrderSummary panel shown for symbol: {symbolInfo.Symbol}, Lots: {symbolInfo.Lots}, Percent: {symbolInfo.Percent}, Points: {symbolInfo.ProfitPoints}");
        }

        private void HideOrderSummary()
        {
            if (Dispatcher.CheckAccess())
            {
                OrderSummaryPanel.Visibility = Visibility.Collapsed;
                Logger.LogInfo("OrderSummary panel hidden");
            }
            else
            {
                Dispatcher.Invoke(() => 
                {
                    OrderSummaryPanel.Visibility = Visibility.Collapsed;
                    Logger.LogInfo("OrderSummary panel hidden");
                });
            }
        }

        private void OnOrderCloseClicked()
        {
            if (!string.IsNullOrEmpty(_currentSymbol))
            {
                // Используем символ из OrderSummary (с суффиксом), если он доступен
                string symbolToClose = OrderSymbolText.Text ?? _currentSymbol;
                CloseSymbolOrder(symbolToClose);
            }
        }

        private void OnOrderBEClicked()
        {
            if (!string.IsNullOrEmpty(_currentSymbol))
            {
                Logger.LogInfo($"BE clicked for {_currentSymbol}");
                // TODO: Implement BE functionality
            }
        }

        private void OnOrderTP1Clicked()
        {
            if (!string.IsNullOrEmpty(_currentSymbol))
            {
                Logger.LogInfo($"TP1 clicked for {_currentSymbol}");
                // TODO: Implement TP1 functionality
            }
        }

        private void OnOrderTP2Clicked()
        {
            if (!string.IsNullOrEmpty(_currentSymbol))
            {
                Logger.LogInfo($"TP2 clicked for {_currentSymbol}");
                // TODO: Implement TP2 functionality
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
                    Logger.LogInfo($"Sent close_positions command for {symbol}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error closing symbol order: {ex.Message}", ex);
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
                
                // Отправляем команду открытия символа для binary-брокеров
                SendOpenSymbolCommand(symbol);
            }
            else
            {
                SymbolLabel.Text = "UNKNOWN";
                SymbolLabel.Visibility = Visibility.Visible;
            }
            
            // Hide order summary when symbol changes
            HideOrderSummary();
        }

        public void SetHandleID(long handleID)
        {
            HandleIDLabel.Text = $"HandleID: {handleID}";
        }

        public void ShowDurationPanel(bool show)
        {
            DurationPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RiskButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null && double.TryParse(btn.Tag.ToString(), out double val))
            {
                SelectedRisk = val;
                HighlightSelectedRiskButton(val);
                RiskChanged?.Invoke(val);
                
                // Отправляем команду в binary-сокет для binary-брокеров
                SendRiskCommandToBinarySocket(val);
            }
        }

        private void DurationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag.ToString(), out int val))
            {
                SelectedDuration = val;
                HighlightSelectedDurationButton(val);
                DurationChanged?.Invoke(val);
                
                // Отправляем команду в binary-сокет для binary-брокеров
                SendDurationCommandToBinarySocket(val);
            }
        }

        private void BrokerButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                BrokerType newType = BrokerType.Forex;
                switch (btn.Tag.ToString())
                {
                    case "Forex": newType = BrokerType.Forex; break;
                    case "PocketOption": newType = BrokerType.PocketOption; break;
                    case "Binarium": newType = BrokerType.Binarium; break;
                    case "Quotex": newType = BrokerType.Quotex; break;
                }
                SelectedBroker = newType;
                HighlightSelectedBroker(newType);
                BrokerChanged?.Invoke(newType);
            }
        }

        /// <summary>
        /// Отправляет команду установки риска в binary-сокет для binary-брокеров
        /// </summary>
        private async void SendRiskCommandToBinarySocket(double risk)
        {
            // Проверяем, что выбран binary-брокер (не Forex)
            if (SelectedBroker == BrokerType.Forex)
            {
                return;
            }

            try
            {
                var jforexService = ServiceContainer.Instance.GetService<JForexWindowsManagerService>();
                if (jforexService != null)
                {
                    string brokerName = SelectedBroker.ToString();
                    bool success = await jforexService.SetRisk(brokerName, risk);
                    
                    if (!success)
                    {
                        Logger.LogWarning($"Failed to set risk {risk} for broker {brokerName}");
                    }
                }
                else
                {
                    Logger.LogWarning("JForex service not available for risk command");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error setting risk via JForex service: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Отправляет команду установки duration в binary-сокет для binary-брокеров
        /// </summary>
        private async void SendDurationCommandToBinarySocket(int duration)
        {
            // Проверяем, что выбран binary-брокер (не Forex)
            if (SelectedBroker == BrokerType.Forex)
            {
                return;
            }

            try
            {
                var jforexService = ServiceContainer.Instance.GetService<JForexWindowsManagerService>();
                if (jforexService != null)
                {
                    string brokerName = SelectedBroker.ToString();
                    bool success = await jforexService.SetDuration(brokerName, duration);
                    
                    if (!success)
                    {
                        Logger.LogWarning($"Failed to set duration {duration} for broker {brokerName}");
                    }
                }
                else
                {
                    Logger.LogWarning("JForex service not available for duration command");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error setting duration via JForex service: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Отправляет команду открытия символа в binary-сокет для binary-брокеров
        /// </summary>
        private async void SendOpenSymbolCommand(string symbolName)
        {
            // Проверяем, что выбран binary-брокер (не Forex)
            if (SelectedBroker == BrokerType.Forex)
            {
                return;
            }

            try
            {
                var jforexService = ServiceContainer.Instance.GetService<JForexWindowsManagerService>();
                if (jforexService != null)
                {
                    string brokerName = SelectedBroker.ToString();
                    bool success = await jforexService.OpenSymbol(brokerName, symbolName);
                    
                    if (!success)
                    {
                        Logger.LogWarning($"Failed to open symbol {symbolName} for broker {brokerName}");
                    }
                }
                else
                {
                    Logger.LogWarning("JForex service not available for open symbol command");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error opening symbol via JForex service: {ex.Message}", ex);
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
    }
} 