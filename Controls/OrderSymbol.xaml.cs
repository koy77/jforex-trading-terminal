using System;
using System.Windows;
using System.Windows.Controls;

namespace ScreenCaptureApp.Controls
{
    public partial class OrderSymbol : UserControl
    {
        public string Symbol
        {
            get => SymbolText.Text;
            set => SymbolText.Text = value;
        }
        public double Lots
        {
            get => double.TryParse(LotsText.Text, out var v) ? v : 0;
            set => LotsText.Text = $"Lots: {value:F2}";
        }
        public double Percent
        {
            get => double.TryParse(PercentText.Text, out var v) ? v : 0;
            set => PercentText.Text = $"%: {value:F2}";
        }
        public double Points
        {
            get => double.TryParse(PointsText.Text, out var v) ? v : 0;
            set => PointsText.Text = $"Pts: {value:F0}";
        }

        public event Action<string> CloseClicked;
        public event Action<string> BEClicked;
        public event Action<string> TP1Clicked;
        public event Action<string> TP2Clicked;
        public event Action<string> TP3Clicked;

        public OrderSymbol()
        {
            InitializeComponent();
            CloseButton.Click += (s, e) => CloseClicked?.Invoke(Symbol);
            BEButton.Click += (s, e) => BEClicked?.Invoke(Symbol);
            TP1Button.Click += (s, e) => TP1Clicked?.Invoke(Symbol);
            TP2Button.Click += (s, e) => TP2Clicked?.Invoke(Symbol);
            TP3Button.Click += (s, e) => TP3Clicked?.Invoke(Symbol);
        }
    }
} 