using System;

namespace ScreenCaptureApp.Models
{
    /// <summary>
    /// Global state management for tracking the number of fired trades
    /// </summary>
    public class TradeCounter
    {
        private int _firedTradesCount = 0;

        /// <summary>
        /// Gets the current count of fired trades
        /// </summary>
        public int FiredTradesCount => _firedTradesCount;

        /// <summary>
        /// Increments the fired trades counter and returns the new count
        /// </summary>
        /// <returns>The new count after incrementing</returns>
        public int IncrementFiredTrades()
        {
            return ++_firedTradesCount;
        }

        /// <summary>
        /// Resets the fired trades counter to zero
        /// </summary>
        public void ResetFiredTrades()
        {
            _firedTradesCount = 0;
        }

        /// <summary>
        /// Sets the fired trades counter to a specific value
        /// </summary>
        /// <param name="count">The new count value</param>
        public void SetFiredTradesCount(int count)
        {
            _firedTradesCount = count;
        }
    }
} 