namespace ISE.Compliance.Safety
{
    /// <summary>
    /// LiquidityValidator - Ensure sufficient market depth and tight spreads before entries
    /// Prevents trading in thin markets or wide spreads
    /// </summary>
    public class LiquidityValidator
    {
        private double _bidPrice = 0;
        private double _askPrice = 0;
        private double _bidVolume = 0;
        private double _askVolume = 0;
        private double _totalBidVolume = 0;  // 10 levels
        private double _totalAskVolume = 0;  // 10 levels
        private DateTime _lastUpdateTime = DateTime.MinValue;

        // Minimum thresholds for trading
        private const double MaxSpreadTicks = 2.0;
        private const double MinBidVolume = 500.0;
        private const double MinAskVolume = 500.0;
        private const double MinTotalVolume = 5000.0;
        private const double WarningSpreadTicks = 1.5;

        /// <summary>
        /// Is market liquid enough for entry?
        /// </summary>
        public bool IsLiquidEnoughForEntry { get; private set; } = false;

        /// <summary>
        /// Current bid-ask spread in ticks
        /// </summary>
        public double SpreadTicks { get; private set; } = 0;

        /// <summary>
        /// Liquidity depth score (0.0 = none, 1.0 = excellent)
        /// </summary>
        public double DepthScore { get; private set; } = 0;

        /// <summary>
        /// Reason if rejected
        /// </summary>
        public string? RejectionReason { get; private set; }

        /// <summary>
        /// Update bid level
        /// </summary>
        public void UpdateBid(double price, double volume)
        {
            _bidPrice = price;
            _bidVolume = volume;
            _lastUpdateTime = DateTime.Now;
            Validate();
        }

        /// <summary>
        /// Update ask level
        /// </summary>
        public void UpdateAsk(double price, double volume)
        {
            _askPrice = price;
            _askVolume = volume;
            _lastUpdateTime = DateTime.Now;
            Validate();
        }

        /// <summary>
        /// Update total 10-level volume
        /// </summary>
        public void UpdateTotalVolume(double totalBidVolume, double totalAskVolume)
        {
            _totalBidVolume = totalBidVolume;
            _totalAskVolume = totalAskVolume;
            Validate();
        }

        /// <summary>
        /// Validate liquidity conditions
        /// </summary>
        private void Validate()
        {
            // Calculate spread (in ticks, assuming 0.01 per tick for stocks)
            if (_bidPrice > 0 && _askPrice > 0)
            {
                SpreadTicks = (_askPrice - _bidPrice) / 0.01;
            }

            // Calculate depth score (0-1.0)
            CalculateDepthScore();

            // Check all conditions
            bool bidVolumeOk = _bidVolume >= MinBidVolume;
            bool askVolumeOk = _askVolume >= MinAskVolume;
            bool spreadOk = SpreadTicks <= MaxSpreadTicks;
            bool totalVolumeOk = (_totalBidVolume + _totalAskVolume) >= MinTotalVolume;

            // Decision
            if (!spreadOk)
            {
                IsLiquidEnoughForEntry = false;
                RejectionReason = $"Spread too wide: {SpreadTicks:F2} ticks > {MaxSpreadTicks} ticks";
                return;
            }

            if (!bidVolumeOk)
            {
                IsLiquidEnoughForEntry = false;
                RejectionReason = $"Bid volume thin: {_bidVolume:F0} shares < {MinBidVolume} minimum";
                return;
            }

            if (!askVolumeOk)
            {
                IsLiquidEnoughForEntry = false;
                RejectionReason = $"Ask volume thin: {_askVolume:F0} shares < {MinAskVolume} minimum";
                return;
            }

            if (!totalVolumeOk)
            {
                IsLiquidEnoughForEntry = false;
                RejectionReason = $"Total depth insufficient: {_totalBidVolume + _totalAskVolume:F0} shares < {MinTotalVolume}";
                return;
            }

            // All checks passed
            IsLiquidEnoughForEntry = true;
            RejectionReason = null;
        }

        /// <summary>
        /// Calculate depth score based on volume and spread
        /// </summary>
        private void CalculateDepthScore()
        {
            // Base score from spread (tighter = better)
            double spreadScore = Math.Max(0, 1.0 - (SpreadTicks / MaxSpreadTicks));

            // Volume score (more = better)
            double totalVolume = _totalBidVolume + _totalAskVolume;
            double volumeScore = Math.Min(1.0, totalVolume / (MinTotalVolume * 2.0));

            // Combine scores
            DepthScore = (spreadScore * 0.4) + (volumeScore * 0.6);
        }

        /// <summary>
        /// Is spread warning-level wide?
        /// </summary>
        public bool IsSpreadWide()
        {
            return SpreadTicks > WarningSpreadTicks && SpreadTicks <= MaxSpreadTicks;
        }

        /// <summary>
        /// Get detailed status
        /// </summary>
        public string GetStatus()
        {
            return $"Spread: {SpreadTicks:F2} ticks | " +
                   $"Bid: {_bidVolume:F0} | Ask: {_askVolume:F0} | " +
                   $"Total: {_totalBidVolume + _totalAskVolume:F0} | " +
                   $"Depth Score: {DepthScore:F2} | " +
                   $"Status: {(IsLiquidEnoughForEntry ? "OK" : "REJECTED")} | " +
                   (RejectionReason != null ? $"Reason: {RejectionReason}" : "");
        }

        /// <summary>
        /// Reset for new session
        /// </summary>
        public void Reset()
        {
            _bidPrice = 0;
            _askPrice = 0;
            _bidVolume = 0;
            _askVolume = 0;
            _totalBidVolume = 0;
            _totalAskVolume = 0;
            _lastUpdateTime = DateTime.MinValue;
            IsLiquidEnoughForEntry = false;
            SpreadTicks = 0;
            DepthScore = 0;
            RejectionReason = null;
        }

        public override string ToString()
        {
            return $"Liquidity: Spread={SpreadTicks:F2}t | Depth={DepthScore:F2} | " +
                   $"Status: {(IsLiquidEnoughForEntry ? "OK" : "THIN")}";
        }
    }
}
