using System;
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

        public TradeRectangleControl()
        {
            InitializeComponent();
        }

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