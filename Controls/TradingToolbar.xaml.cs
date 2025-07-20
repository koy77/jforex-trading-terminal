using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScreenCaptureApp.Models;

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
            }
        }

        private void DurationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag.ToString(), out int val))
            {
                SelectedDuration = val;
                HighlightSelectedDurationButton(val);
                DurationChanged?.Invoke(val);
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