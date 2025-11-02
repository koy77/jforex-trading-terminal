using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Media.Effects;

namespace ScreenCaptureApp.Services
{
    public enum ToastType
    {
        Success,
        Error,
        Info,
        BreakoutUp,
        BreakoutDown
    }

    public class ToastNotifyService
    {
        private readonly List<ToastItem> _activeToasts = new List<ToastItem>();
        private readonly object _lockObject = new object();
        private const int MaxToasts = 5;
        private const double ToastHeight = 60;
        private const double ToastWidth = 300;
        private const double ToastSpacing = 10;
        private const double BottomMargin = 20;
        private const double LeftMargin = 20;

        public void ShowToast(string message, ToastType type, int durationMs = 2500)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                lock (_lockObject)
                {
                    // Удаляем истекшие тосты
                    RemoveExpiredToasts();

                    // Если достигнут лимит, удаляем самый старый
                    if (_activeToasts.Count >= MaxToasts)
                    {
                        RemoveOldestToast();
                    }

                    // Создаем новый тост
                    var toastItem = CreateToastItem(message, type, durationMs);
                    _activeToasts.Add(toastItem);

                    // Пересчитываем позиции всех тостов
                    RecalculateToastPositions();

                    // Запускаем анимацию появления для нового тоста
                    AnimateToastIn(toastItem);
                }
            });
        }
        
        /// <summary>
        /// Принудительно обновляет позиции всех активных тостов
        /// </summary>
        public void RefreshToastPositions()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                lock (_lockObject)
                {
                    if (_activeToasts.Count > 0)
                    {
                        RecalculateToastPositions();
                    }
                }
            });
        }

        private void RemoveExpiredToasts()
        {
            var expiredToasts = _activeToasts.Where(t => t.IsExpired && !t.IsRemoving).ToList();
            foreach (var toast in expiredToasts)
            {
                AnimateToastOut(toast);
            }
        }

        private void RemoveOldestToast()
        {
            if (_activeToasts.Count > 0)
            {
                var oldestToast = _activeToasts[0];
                AnimateToastOut(oldestToast);
            }
        }

        private void RecalculateToastPositions()
        {
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            var bottomY = screen.Bounds.Bottom - BottomMargin - ToastHeight;
            var stableToasts = _activeToasts.Where(t => !t.IsRemoving).OrderBy(t => t.CreatedAt).ToList();

            for (int i = 0; i < stableToasts.Count; i++)
            {
                var toast = stableToasts[i];
                var newTargetLeft = screen.Bounds.Left + LeftMargin + (ToastWidth + ToastSpacing) * i;
                
                // Если позиция изменилась, анимируем горизонтальное перемещение
                if (Math.Abs(toast.CurrentLeft - newTargetLeft) > 1 && !toast.IsRemoving)
                {
                    AnimateToastReposition(toast, newTargetLeft, i);
                }
                else
                {
                    toast.TargetPosition = i;
                    toast.TargetLeft = newTargetLeft;
                    if (!toast.IsAnimating)
                    {
                        toast.CurrentLeft = newTargetLeft;
                        toast.Window.Left = newTargetLeft;
                    }
                }
                
                // Вертикальная позиция всегда одинакова - внизу экрана
                toast.TargetTop = bottomY;
                if (!toast.IsAnimating)
                {
                    toast.CurrentTop = bottomY;
                    toast.Window.Top = bottomY;
                }
            }
        }

        private void AnimateToastReposition(ToastItem toastItem, double newTargetLeft, int newPosition)
        {
            if (toastItem.IsAnimating || toastItem.IsRemoving) return;

            toastItem.IsAnimating = true;
            toastItem.TargetPosition = newPosition;
            toastItem.TargetLeft = newTargetLeft;

            var animation = new DoubleAnimation
            {
                From = toastItem.CurrentLeft,
                To = newTargetLeft,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            animation.Completed += (s, e) =>
            {
                toastItem.IsAnimating = false;
                toastItem.CurrentLeft = newTargetLeft;
            };

            toastItem.Window.BeginAnimation(Window.LeftProperty, animation);
        }

        private ToastItem CreateToastItem(string message, ToastType type, int durationMs)
        {
            var window = new Window
            {
                Width = ToastWidth,
                Height = ToastHeight,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                Topmost = true,
                ResizeMode = ResizeMode.NoResize,
                ShowActivated = false,
                Focusable = false
            };
            
            // Устанавливаем максимальный Z-индекс для тостов
            window.SetValue(Panel.ZIndexProperty, int.MaxValue);

            var border = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = GetBackgroundBrush(type),
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(2),
                Opacity = 0.0, // Начинаем с прозрачности 0
                Margin = new Thickness(8),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 5,
                    Opacity = 0.3,
                    BlurRadius = 10
                },
                Child = new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 16,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.CharacterEllipsis
                }
            };

            window.Content = border;

            // Позиционируем за пределами экрана (слева)
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            var bottomY = screen.Bounds.Bottom - BottomMargin - ToastHeight;
            window.Top = bottomY;
            window.Left = screen.Bounds.Left - ToastWidth - 100; // Начинаем за пределами экрана слева

            var toastItem = new ToastItem
            {
                Window = window,
                Message = message,
                Type = type,
                CreatedAt = DateTime.Now,
                DurationMs = durationMs,
                IsAnimating = false,
                TargetPosition = 0,
                IsRemoving = false,
                CurrentLeft = screen.Bounds.Left - ToastWidth - 100,
                CurrentTop = bottomY,
                TargetLeft = 0,
                TargetTop = bottomY
            };

            // Создаем таймер для удаления
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                AnimateToastOut(toastItem);
            };
            timer.Start();
            toastItem.Timer = timer;

            window.Show();
            return toastItem;
        }

        private void AnimateToastIn(ToastItem toastItem)
        {
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            var bottomY = screen.Bounds.Bottom - BottomMargin - ToastHeight;
            
            // Определяем позицию: сколько тостов уже есть (включая анимирующиеся)
            int targetIndex = _activeToasts.Count - 1; // Новый тост всегда последний
            var targetLeft = screen.Bounds.Left + LeftMargin + (ToastWidth + ToastSpacing) * targetIndex;
            
            toastItem.TargetPosition = targetIndex;
            toastItem.TargetLeft = targetLeft;
            toastItem.TargetTop = bottomY;
            toastItem.IsAnimating = true;

            // Анимация горизонтального появления (слева направо)
            var positionAnimation = new DoubleAnimation
            {
                From = screen.Bounds.Left - ToastWidth - 100,
                To = targetLeft,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new CubicEase 
                { 
                    EasingMode = EasingMode.EaseOut
                }
            };

            // Анимация прозрачности
            var opacityAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = 0.95,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            positionAnimation.Completed += (s, e) =>
            {
                toastItem.IsAnimating = false;
                toastItem.CurrentLeft = targetLeft;
                toastItem.CurrentTop = bottomY;
            };

            // Устанавливаем вертикальную позицию сразу (без анимации)
            toastItem.Window.Top = bottomY;

            // Запускаем анимации появления
            toastItem.Window.BeginAnimation(Window.LeftProperty, positionAnimation);
            ((Border)toastItem.Window.Content).BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
        }

        private void AnimateToastOut(ToastItem toastItem)
        {
            if (toastItem.IsRemoving) return;
            
            toastItem.IsRemoving = true;
            toastItem.Timer?.Stop();

            // Анимация исчезновения (только затухание, без движения)
            var opacityAnimation = new DoubleAnimation
            {
                From = 0.95,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            opacityAnimation.Completed += (s, e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    lock (_lockObject)
                    {
                        if (_activeToasts.Contains(toastItem))
                        {
                            _activeToasts.Remove(toastItem);
                            toastItem.Window.Close();
                            
                            // Пересчитываем позиции оставшихся тостов (сдвигаем влево)
                            RecalculateToastPositions();
                        }
                    }
                });
            };

            // Запускаем анимацию затухания
            ((Border)toastItem.Window.Content).BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
        }

        private void RemoveToast(ToastItem toastItem)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                lock (_lockObject)
                {
                    if (_activeToasts.Contains(toastItem))
                    {
                        _activeToasts.Remove(toastItem);
                        toastItem.Window.Close();
                    }
                }
            });
        }

        private Brush GetBackgroundBrush(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success:
                    return new SolidColorBrush(Color.FromRgb(60, 180, 75));
                case ToastType.Error:
                    return new SolidColorBrush(Color.FromRgb(0, 0, 0));
                case ToastType.Info:
                    return new SolidColorBrush(Color.FromRgb(0, 120, 215));
                case ToastType.BreakoutUp:
                    return new SolidColorBrush(Color.FromRgb(60, 180, 75));
                case ToastType.BreakoutDown:
                    return new SolidColorBrush(Color.FromRgb(220, 50, 47));
                default:
                    throw new ArgumentException("Unknown toast type");
            }
        }

        private class ToastItem
        {
            public Window Window { get; set; }
            public string Message { get; set; }
            public ToastType Type { get; set; }
            public DateTime CreatedAt { get; set; }
            public int DurationMs { get; set; }
            public DispatcherTimer Timer { get; set; }
            public bool IsAnimating { get; set; }
            public int TargetPosition { get; set; }
            public bool IsRemoving { get; set; }
            public double CurrentLeft { get; set; }
            public double CurrentTop { get; set; }
            public double TargetLeft { get; set; }
            public double TargetTop { get; set; }

            public bool IsExpired => DateTime.Now - CreatedAt > TimeSpan.FromMilliseconds(DurationMs);
        }
    }
} 