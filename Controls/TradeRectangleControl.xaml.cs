using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ScreenCaptureApp.Services;
using ScreenCaptureApp.Models;

namespace ScreenCaptureApp.Controls
{
    public partial class TradeRectangleControl : UserControl
    {
        public event EventHandler<RectangleTradeEventArgs> BuyClicked;
        public event EventHandler<RectangleTradeEventArgs> SellClicked;

        private string _symbol;
        private double _upperPrice;
        private double _lowerPrice;
        private double _selectedRisk = 1; // По умолчанию риск 1

        public TradeRectangleControl()
        {
            InitializeComponent();
            
            // Устанавливаем начальное состояние - кнопка с риском 1 активна
            HighlightSelectedRiskButton(_selectedRisk);
        }

        /// <summary>
        /// Получает выбранный риск
        /// </summary>
        public double SelectedRisk => _selectedRisk;

        /// <summary>
        /// Устанавливает данные для отображения
        /// </summary>
        public void SetRectangleData(string symbol, double upperPrice, double lowerPrice)
        {
            _symbol = symbol;
            _upperPrice = upperPrice;
            _lowerPrice = lowerPrice;

            // Используем Dispatcher для обновления UI из другого потока
            this.Dispatcher.Invoke(() =>
            {
                SymbolLabel.Text = symbol;
                UpperPriceLabel.Text = $"Upper: {upperPrice:F5}";
                LowerPriceLabel.Text = $"Lower: {lowerPrice:F5}";
            });
        }

        /// <summary>
        /// Показывает контрол
        /// </summary>
        public void Show()
        {
            this.Dispatcher.Invoke(() =>
            {
                this.Visibility = Visibility.Visible;
            });
        }

        /// <summary>
        /// Скрывает контрол
        /// </summary>
        public void Hide()
        {
            this.Dispatcher.Invoke(() =>
            {
                this.Visibility = Visibility.Collapsed;
            });
        }

        private void BuyButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.LogTagInfo("TradeRectangleControl", $"Buy button clicked for {_symbol} at upper price {_upperPrice:F5}");
            
            var args = new RectangleTradeEventArgs
            {
                Symbol = _symbol,
                EntryPrice = _upperPrice,
                StopLossPrice = _lowerPrice,
                TradeType = "buy"
            };

            BuyClicked?.Invoke(this, args);
        }

        private void SellButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.LogTagInfo("TradeRectangleControl", $"Sell button clicked for {_symbol} at lower price {_lowerPrice:F5}");
            
            var args = new RectangleTradeEventArgs
            {
                Symbol = _symbol,
                EntryPrice = _lowerPrice,
                StopLossPrice = _upperPrice,
                TradeType = "sell"
            };

            SellClicked?.Invoke(this, args);
        }

        private void RiskButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string riskString)
            {
                if (double.TryParse(riskString, out double risk))
                {
                    _selectedRisk = risk;
                    
                    // Подсвечиваем выбранную кнопку
                    HighlightSelectedRiskButton(risk);
                    
                    Logger.LogTagInfo("TradeRectangleControl", $"Risk changed to {risk}");
                }
            }
        }



        /// <summary>
        /// Подсвечивает выбранную кнопку риска
        /// </summary>
        /// <param name="risk">Выбранный риск</param>
        public void HighlightSelectedRiskButton(double risk)
        {
            var riskButtons = FindVisualChildren<Button>(this).Where(b => b.Tag is string && double.TryParse(b.Tag.ToString(), out _));
            foreach (var button in riskButtons)
            {
                if (double.TryParse(button.Tag?.ToString(), out double val))
                {
                    button.Background = (val == risk) ? System.Windows.Media.Brushes.Orange : System.Windows.Media.Brushes.LightGray;
                }
            }
        }

        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                if (child is T t) yield return t;
                foreach (T childOfChild in FindVisualChildren<T>(child)) yield return childOfChild;
            }
        }
    }

    /// <summary>
    /// Аргументы события торговли по прямоугольнику
    /// </summary>
    public class RectangleTradeEventArgs : EventArgs
    {
        public string Symbol { get; set; }
        public double EntryPrice { get; set; }
        public double StopLossPrice { get; set; }
        public string TradeType { get; set; } // "buy" или "sell"
    }
} 