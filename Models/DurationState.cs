using System;

namespace ScreenCaptureApp.Models
{
    public class DurationState
    {
        private int _currentDuration = 2;
        public int CurrentDuration
        {
            get => _currentDuration;
            set
            {
                if (_currentDuration != value)
                {
                    _currentDuration = value;
                    DurationChanged?.Invoke(this, value);
                }
            }
        }
        public event EventHandler<int> DurationChanged;
    }
} 