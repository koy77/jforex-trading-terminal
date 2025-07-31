using System;
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

        public TradingToolbar()
        {
            InitializeComponent();
            BrokerComboBox.SelectedIndex = 0;
            HighlightSelectedRiskButton(SelectedRisk);
            HighlightSelectedDurationButton(SelectedDuration);
        }

        public void SetModeLabel(string text, bool visible)
        {
            ModeLabel.Text = text;
            ModeLabel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public void SetSymbol(string symbol)
        {
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

        private void BrokerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BrokerComboBox.SelectedItem is ComboBoxItem item)
            {
                BrokerType newType = BrokerType.Forex;
                switch (item.Tag?.ToString())
                {
                    case "Forex": newType = BrokerType.Forex; break;
                    case "PocketOption": newType = BrokerType.PocketOption; break;
                    case "Binarium": newType = BrokerType.Binarium; break;
                    case "Quotex": newType = BrokerType.Quotex; break;
                }
                SelectedBroker = newType;
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
            for (int i = 0; i < BrokerComboBox.Items.Count; i++)
            {
                if (BrokerComboBox.Items[i] is ComboBoxItem item && item.Tag?.ToString() == broker.ToString())
                {
                    BrokerComboBox.SelectedIndex = i;
                    break;
                }
            }
        }
    }
} 