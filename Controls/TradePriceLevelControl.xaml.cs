using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ScreenCaptureApp.Services;
using ScreenCaptureApp.Models;

namespace ScreenCaptureApp.Controls
{
    public partial class TradePriceLevelControl : UserControl
    {
        public event EventHandler<double> RiskChanged;
        public event EventHandler<string> TakeProfitChanged;

        private string _symbol;
        private double _entryPrice;
        private double _selectedRisk = 1; // По умолчанию риск 1
        private string _selectedTakeProfit = "1x"; // По умолчанию TP 1x

        public TradePriceLevelControl()
        {
            InitializeComponent();
            
            // Устанавливаем начальное состояние - кнопка с риском 1 активна
            HighlightSelectedRiskButton(_selectedRisk);
            
            // Устанавливаем начальное состояние - кнопка TP 1x активна
            HighlightSelectedTPButton(_selectedTakeProfit);
        }

        /// <summary>
        /// Получает выбранный риск
        /// </summary>
        public double SelectedRisk => _selectedRisk;

        /// <summary>
        /// Получает выбранный Take Profit
        /// </summary>
        public string SelectedTakeProfit => _selectedTakeProfit;

        /// <summary>
        /// Устанавливает риск извне (например, из Trading Toolbar)
        /// </summary>
        /// <param name="risk">Значение риска</param>
        public void SetRisk(double risk)
        {
            _selectedRisk = risk;
            HighlightSelectedRiskButton(risk);
        }

        /// <summary>
        /// Устанавливает Take Profit извне
        /// </summary>
        /// <param name="takeProfit">Значение Take Profit ("1x", "2x", "3x", "inf")</param>
        public void SetTakeProfit(string takeProfit)
        {
            _selectedTakeProfit = takeProfit;
            HighlightSelectedTPButton(takeProfit);
        }

        /// <summary>
        /// Устанавливает данные для отображения
        /// </summary>
        public void SetPriceLevelData(string symbol, double entryPrice)
        {
            _symbol = symbol;
            _entryPrice = entryPrice;

            // Используем Dispatcher для обновления UI из другого потока
            this.Dispatcher.Invoke(() =>
            {
                SymbolLabel.Text = symbol;
                EntryLevelLabel.Text = $"Entry Level: {entryPrice:F5}";
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



        private void RiskButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string riskString)
            {
                if (double.TryParse(riskString, out double risk))
                {
                    _selectedRisk = risk;
                    
                    // Подсвечиваем выбранную кнопку (уже безопасно)
                    HighlightSelectedRiskButton(risk);
                    
                    // Вызываем событие изменения риска
                    RiskChanged?.Invoke(this, risk);
                    
                    Logger.LogTagInfo("TradePriceLevelControl", $"Risk changed to {risk}");
                }
            }
        }

        private void TPButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tpValue)
            {
                _selectedTakeProfit = tpValue;
                
                // Подсвечиваем выбранную кнопку
                HighlightSelectedTPButton(tpValue);
                
                // Вызываем событие изменения Take Profit
                TakeProfitChanged?.Invoke(this, tpValue);
                
                Logger.LogTagInfo("TradePriceLevelControl", $"Take Profit changed to {tpValue}");
            }
        }

        /// <summary>
        /// Подсвечивает выбранную кнопку риска
        /// </summary>
        /// <param name="risk">Выбранный риск</param>
        public void HighlightSelectedRiskButton(double risk)
        {
            if (Dispatcher.CheckAccess())
            {
                UpdateRiskButtonColors(risk);
            }
            else
            {
                Dispatcher.Invoke(() => UpdateRiskButtonColors(risk));
            }
        }

        /// <summary>
        /// Обновляет цвета кнопок риска (выполняется в UI потоке)
        /// </summary>
        /// <param name="risk">Выбранный риск</param>
        private void UpdateRiskButtonColors(double risk)
        {
            var riskButtons = FindVisualChildren<Button>(this).Where(b => b.Tag is string && double.TryParse(b.Tag.ToString(), out _));
            foreach (var button in riskButtons)
            {
                if (double.TryParse(button.Tag?.ToString(), out double val))
                {
                    if (val == risk)
                    {
                        button.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFA500"));
                        button.Foreground = System.Windows.Media.Brushes.White;
                    }
                    else
                    {
                        button.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4A4A4A"));
                        button.Foreground = System.Windows.Media.Brushes.White;
                    }
                }
            }
        }

        /// <summary>
        /// Подсвечивает выбранную кнопку Take Profit
        /// </summary>
        /// <param name="takeProfit">Выбранный Take Profit</param>
        public void HighlightSelectedTPButton(string takeProfit)
        {
            if (Dispatcher.CheckAccess())
            {
                UpdateTPButtonColors(takeProfit);
            }
            else
            {
                Dispatcher.Invoke(() => UpdateTPButtonColors(takeProfit));
            }
        }

        /// <summary>
        /// Обновляет цвета кнопок Take Profit (выполняется в UI потоке)
        /// </summary>
        /// <param name="takeProfit">Выбранный Take Profit</param>
        private void UpdateTPButtonColors(string takeProfit)
        {
            var tpButtons = FindVisualChildren<Button>(this).Where(b => b.Tag is string && (b.Tag.ToString() == "1x" || b.Tag.ToString() == "2x" || b.Tag.ToString() == "3x" || b.Tag.ToString() == "inf"));
            foreach (var button in tpButtons)
            {
                if (button.Tag?.ToString() == takeProfit)
                {
                    button.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFA500"));
                    button.Foreground = System.Windows.Media.Brushes.White;
                }
                else
                {
                    button.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4A4A4A"));
                    button.Foreground = System.Windows.Media.Brushes.White;
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


} 