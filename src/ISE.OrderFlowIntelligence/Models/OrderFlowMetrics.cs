namespace ISE.OrderFlowIntelligence.Models
{
    /// <summary>
    /// Order flow analysis metrics
    /// Combines DOM data to produce actionable signals
    /// </summary>
    public class OrderFlowMetrics
    {
        /// <summary>
        /// Timestamp of this analysis
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Order flow bias score (-100 to +100)
        /// -100 = pure buying pressure, 0 = balanced, +100 = pure selling pressure
        /// Calculated from cumulative bid/ask volume imbalance
        /// </summary>
        public double OrderFlowBias { get; set; }

        /// <summary>
        /// True if order flow is strongly bullish (bias < -50)
        /// Indicates institutional buyers stepping in
        /// </summary>
        public bool IsBullishOrderFlow => OrderFlowBias < -50.0;

        /// <summary>
        /// True if order flow is strongly bearish (bias > +50)
        /// Indicates institutional sellers taking over
        /// </summary>
        public bool IsBearishOrderFlow => OrderFlowBias > 50.0;

        /// <summary>
        /// Cumulative volume at detected support level
        /// Higher volume = stronger support
        /// </summary>
        public long SupportClusterVolume { get; set; }

        /// <summary>
        /// Price level where support is detected
        /// </summary>
        public double SupportLevel { get; set; }

        /// <summary>
        /// Cumulative volume at detected resistance level
        /// Higher volume = stronger resistance
        /// </summary>
        public long ResistanceClusterVolume { get; set; }

        /// <summary>
        /// Price level where resistance is detected
        /// </summary>
        public double ResistanceLevel { get; set; }

        /// <summary>
        /// True if price rejected at resistance
        /// Indicates sellers defending a level
        /// </summary>
        public bool RejectionAtResistance { get; set; }

        /// <summary>
        /// True if price bounced at support
        /// Indicates buyers defending a level
        /// </summary>
        public bool RejectionAtSupport { get; set; }

        /// <summary>
        /// Absorption detection: large institutional order execution
        /// True if order book is being "absorbed" (orders disappearing into large blocks)
        /// Indicates significant institutional activity
        /// </summary>
        public bool InstitutionalAbsorption { get; set; }

        /// <summary>
        /// Absorption strength (0.0 to 1.0)
        /// How confident we are that absorption is happening
        /// 1.0 = high confidence institutional buyer/seller present
        /// </summary>
        public double AbsorptionStrength { get; set; }

        /// <summary>
        /// Bid/ask spread (in price units)
        /// Used for entry validation (skip if spread too wide)
        /// </summary>
        public double Spread { get; set; }

        /// <summary>
        /// Book depth quality (0.0 to 1.0)
        /// 1.0 = excellent liquidity (10+ levels, tight spreads)
        /// 0.0 = poor liquidity (1-2 levels, wide spreads)
        /// </summary>
        public double LiquidityQuality { get; set; }

        /// <summary>
        /// True if liquidity is sufficient for entry
        /// Typically: spread < 3 ticks AND LiquidityQuality > 0.6
        /// </summary>
        public bool IsLiquidEnoughForEntry { get; set; }

        /// <summary>
        /// DOM data quality (0.0 to 1.0)
        /// 1.0 = fresh real-time data, 0.0 = stale/incomplete
        /// </summary>
        public double DataFreshness { get; set; }

        public OrderFlowMetrics()
        {
            Timestamp = DateTime.UtcNow;
            OrderFlowBias = 0.0;
            SupportLevel = 0.0;
            SupportClusterVolume = 0;
            ResistanceLevel = 0.0;
            ResistanceClusterVolume = 0;
            Spread = 0.0;
            LiquidityQuality = 0.0;
            AbsorptionStrength = 0.0;
            DataFreshness = 1.0;
        }

        /// <summary>
        /// Get entry recommendation based on order flow
        /// Returns: (recommendLong, recommendShort, confidence 0-1)
        /// </summary>
        public (bool recommendLong, bool recommendShort, double confidence) GetEntryRecommendation()
        {
            bool canEntry = IsLiquidEnoughForEntry && DataFreshness > 0.8;

            if (!canEntry)
                return (false, false, 0.0);

            bool recommendLong = IsBullishOrderFlow && RejectionAtSupport;
            bool recommendShort = IsBearishOrderFlow && RejectionAtResistance;

            double confidence = 0.0;
            if (recommendLong)
                confidence = Math.Min(1.0, (Math.Abs(OrderFlowBias) / 100.0) * LiquidityQuality);
            else if (recommendShort)
                confidence = Math.Min(1.0, (OrderFlowBias / 100.0) * LiquidityQuality);

            return (recommendLong, recommendShort, confidence);
        }

        public override string ToString()
        {
            return $"{Timestamp:HH:mm:ss} | OrderFlow: {OrderFlowBias:F1} | Support: {SupportLevel:F2} | Resistance: {ResistanceLevel:F2} | Absorption: {InstitutionalAbsorption}";
        }
    }
}
