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
        BreakoutUp,
        BreakoutDown
    }

    public class ToastNotifyService
    {
        private readonly List<ToastItem> _activeToasts = new List<ToastItem>();
        private readonly object _lockObject = new object();
        private const int MaxToasts = 5;
        private const double ToastHeight = 60;
        private const double ToastSpacing = 10;
        private const double BottomMargin = 60;

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
                _activeToasts.RemoveAt(0);
                AnimateToastOut(oldestToast);
            }
        }

        private void RecalculateToastPositions()
        {
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            var stableToasts = _activeToasts.Where(t => !t.IsAnimating && !t.IsRemoving).ToList();

            for (int i = 0; i < stableToasts.Count; i++)
            {
                var toast = stableToasts[i];
                var newTargetTop = screen.Bounds.Bottom - BottomMargin - (ToastHeight + ToastSpacing) * (i + 1);
                
                // Если позиция изменилась, анимируем перемещение
                if (Math.Abs(toast.CurrentTop - newTargetTop) > 1)
                {
                    AnimateToastReposition(toast, newTargetTop, i);
                }
                else
                {
                    toast.TargetPosition = i;
                    toast.TargetTop = newTargetTop;
                }
            }
        }

        private void AnimateToastReposition(ToastItem toastItem, double newTargetTop, int newPosition)
        {
            if (toastItem.IsAnimating || toastItem.IsRemoving) return;

            toastItem.IsAnimating = true;
            toastItem.TargetPosition = newPosition;
            toastItem.TargetTop = newTargetTop;

            var animation = new DoubleAnimation
            {
                From = toastItem.CurrentTop,
                To = newTargetTop,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            animation.Completed += (s, e) =>
            {
                toastItem.IsAnimating = false;
                toastItem.CurrentTop = newTargetTop;
            };

            toastItem.Window.BeginAnimation(Window.TopProperty, animation);
        }

        private ToastItem CreateToastItem(string message, ToastType type, int durationMs)
        {
            var window = new Window
            {
                Width = 400,
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
                    FontSize = 18,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                }
            };

            window.Content = border;

            // Позиционируем за пределами экрана (снизу)
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            window.Left = screen.Bounds.Left + (screen.Bounds.Width - window.Width) / 2;
            window.Top = screen.Bounds.Bottom + 100; // Начинаем за пределами экрана

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
                CurrentTop = screen.Bounds.Bottom + 100,
                TargetTop = 0
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
            
            // Находим свободную позицию (не занятую анимирующимися тостами)
            var occupiedPositions = _activeToasts
                .Where(t => t != toastItem && t.IsAnimating)
                .Select(t => t.TargetPosition)
                .ToHashSet();

            int targetIndex = 0;
            while (occupiedPositions.Contains(targetIndex))
            {
                targetIndex++;
            }

            var targetTop = screen.Bounds.Bottom - BottomMargin - (ToastHeight + ToastSpacing) * (targetIndex + 1);
            
            toastItem.TargetPosition = targetIndex;
            toastItem.TargetTop = targetTop;
            toastItem.IsAnimating = true;

            // Анимация позиции
            var positionAnimation = new DoubleAnimation
            {
                From = screen.Bounds.Bottom + 100,
                To = targetTop,
                Duration = TimeSpan.FromMilliseconds(800),
                EasingFunction = new ElasticEase 
                { 
                    EasingMode = EasingMode.EaseOut,
                    Oscillations = 1,
                    Springiness = 3
                }
            };

            // Анимация прозрачности
            var opacityAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = 0.95,
                Duration = TimeSpan.FromMilliseconds(600),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            positionAnimation.Completed += (s, e) =>
            {
                toastItem.IsAnimating = false;
                toastItem.CurrentTop = targetTop;
            };

            // Запускаем обе анимации одновременно
            toastItem.Window.BeginAnimation(Window.TopProperty, positionAnimation);
            ((Border)toastItem.Window.Content).BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
        }

        private void AnimateToastOut(ToastItem toastItem)
        {
            if (toastItem.IsRemoving) return;
            
            toastItem.IsRemoving = true;
            toastItem.Timer?.Stop();

            var screen = System.Windows.Forms.Screen.PrimaryScreen;

            // Анимация исчезновения (движение вниз + затухание)
            var positionAnimation = new DoubleAnimation
            {
                From = toastItem.CurrentTop,
                To = screen.Bounds.Bottom + 100,
                Duration = TimeSpan.FromMilliseconds(600),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            var opacityAnimation = new DoubleAnimation
            {
                From = 0.95,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            positionAnimation.Completed += (s, e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    lock (_lockObject)
                    {
                        if (_activeToasts.Contains(toastItem))
                        {
                            _activeToasts.Remove(toastItem);
                            toastItem.Window.Close();
                            
                            // Пересчитываем позиции оставшихся тостов
                            RecalculateToastPositions();
                        }
                    }
                });
            };

            // Запускаем анимации исчезновения
            toastItem.Window.BeginAnimation(Window.TopProperty, positionAnimation);
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
            public double CurrentTop { get; set; }
            public double TargetTop { get; set; }

            public bool IsExpired => DateTime.Now - CreatedAt > TimeSpan.FromMilliseconds(DurationMs);
        }
    }
} 