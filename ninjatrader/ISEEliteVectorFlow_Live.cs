#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
	public class ISEEliteVectorFlowLive : Strategy
	{
		// FTC + VIDYA parameters
		private int ftcPeriod = 20;
		private int atrPeriod = 14;
		private int vidyaPeriod = 20;

		// Entry parameters (locked from backtest)
		private double stopLossPoints = 87.5;
		private double profitTargetPoints = 44.0;
		private double breakEvenMovePoints = 62.5;
		private int contractSize = 4;

		// State tracking
		private bool breakEvenSet = false;
		private double entryPrice = 0;
		
		// Previous alignment state (for edge detection)
		private bool prevFtcVidyaLong = false;
		private bool prevFtcVidyaShort = false;
		
		// VIDYA state (rolling calculation)
		private double prevVidya = 0;
		
		// Alignment confirmation buffer (reduce false signals)
		private int alignmentConfirmBars = 0;
		private const int CONFIRM_THRESHOLD = 3;  // Require 3 consecutive bars of alignment

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = "ISE Elite VectorFlow Live (Merged)";
				Name = "ISEEliteVectorFlowLive";
				Calculate = Calculate.OnBarClose;
				EntriesPerDirection = 1;
				EntryHandling = EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy = false;
				ExitOnSessionCloseSeconds = 300;
				StopTargetHandling = StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade = ftcPeriod + 1;
			}
			else if (State == State.Configure)
			{
				// Initialize VIDYA state
				prevVidya = 0;
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < ftcPeriod)
				return;

			// ========== SIGNAL GENERATION ==========
			// Calculate FTC (Fundamental Trend Channel): SMA ± ATR
			double sma = SMA(Close, ftcPeriod)[0];
			double atr = ATR(Close, atrPeriod)[0];
			double ftcUpper = sma + atr;
			double ftcLower = sma - atr;

			// Calculate VIDYA (adaptive EMA based on momentum ratio)
			double vidya = CalculateVIDYA();

			// Detect alignment: FTC and VIDYA point same direction
			bool ftcVidyaLongAlign = (vidya > sma) && (Close[0] > ftcLower);  // Both bullish
			bool ftcVidyaShortAlign = (vidya < sma) && (Close[0] < ftcUpper);  // Both bearish

			// Alignment confirmation buffer: require alignment to hold for 3+ consecutive bars
			// This reduces false signals from transient oscillations
			if (ftcVidyaLongAlign || ftcVidyaShortAlign)
			{
				alignmentConfirmBars++;
			}
			else
			{
				alignmentConfirmBars = 0;
			}

			// Generate buy/sell only if alignment has held for 3+ bars AND transitions
			bool buySignal = ftcVidyaLongAlign && alignmentConfirmBars >= CONFIRM_THRESHOLD && !prevFtcVidyaLong;
			bool sellSignal = ftcVidyaShortAlign && alignmentConfirmBars >= CONFIRM_THRESHOLD && !prevFtcVidyaShort;

			// Update state for next bar
			prevFtcVidyaLong = ftcVidyaLongAlign;
			prevFtcVidyaShort = ftcVidyaShortAlign;

			// ========== BREAKEVEN LOGIC ==========
			if (Position.MarketPosition == MarketPosition.Long && !breakEvenSet)
			{
				if (Close[0] > entryPrice + (breakEvenMovePoints * 0.25))
				{
					SetStopLoss(CalculationMode.Price, entryPrice);
					breakEvenSet = true;
					Print(Time[0] + " LONG BREAKEVEN at " + Close[0]);
				}
			}
			else if (Position.MarketPosition == MarketPosition.Short && !breakEvenSet)
			{
				if (Close[0] < entryPrice - (breakEvenMovePoints * 0.25))
				{
					SetStopLoss(CalculationMode.Price, entryPrice);
					breakEvenSet = true;
					Print(Time[0] + " SHORT BREAKEVEN at " + Close[0]);
				}
			}
			else if (Position.MarketPosition == MarketPosition.Flat)
			{
				breakEvenSet = false;
			}

			// ========== ENTRY LOGIC ==========
			if (buySignal && Position.MarketPosition == MarketPosition.Flat)
			{
				EnterLong(contractSize, "Long");
				SetStopLoss(CalculationMode.Price, Close[0] - (stopLossPoints * 0.25));
				SetProfitTarget(CalculationMode.Price, Close[0] + (profitTargetPoints * 0.25));
				entryPrice = Close[0];
				breakEvenSet = false;
				Print(Time[0] + " LONG entry at " + Close[0] + " | Stop: " + (Close[0] - (stopLossPoints * 0.25)) + " | Target: " + (Close[0] + (profitTargetPoints * 0.25)));
			}

			if (sellSignal && Position.MarketPosition == MarketPosition.Flat)
			{
				EnterShort(contractSize, "Short");
				SetStopLoss(CalculationMode.Price, Close[0] + (stopLossPoints * 0.25));
				SetProfitTarget(CalculationMode.Price, Close[0] - (profitTargetPoints * 0.25));
				entryPrice = Close[0];
				breakEvenSet = false;
				Print(Time[0] + " SHORT entry at " + Close[0] + " | Stop: " + (Close[0] + (stopLossPoints * 0.25)) + " | Target: " + (Close[0] - (profitTargetPoints * 0.25)));
			}
		}

		/// <summary>
		/// Calculate VIDYA (Volatility Index Dynamic Average)
		/// Uses momentum ratio to adaptively weight the EMA
		/// </summary>
		private double CalculateVIDYA()
		{
			// On first bar, initialize to close
			if (CurrentBar == 0)
			{
				prevVidya = Close[0];
				return Close[0];
			}

			// Momentum: current close relative to N periods ago
			double momentum = Math.Abs(Close[0] - Close[Math.Min(vidyaPeriod, CurrentBar)]);
			double sumAbsMomentum = 0;

			// Sum of absolute price changes over last N periods
			int lookback = Math.Min(vidyaPeriod, CurrentBar);
			for (int i = 0; i < lookback; i++)
			{
				sumAbsMomentum += Math.Abs(Close[i] - Close[i + 1]);
			}

			// Avoid division by zero
			if (sumAbsMomentum == 0)
				return prevVidya;

			// Momentum ratio (0 to 1): how much momentum vs total volatility
			double momentumRatio = momentum / sumAbsMomentum;
			double baseAlpha = 2.0 / (vidyaPeriod + 1.0);

			// Adaptive alpha: scale by momentum ratio
			double adaptiveAlpha = baseAlpha * momentumRatio;

			// EMA with adaptive alpha
			double vidya = prevVidya + adaptiveAlpha * (Close[0] - prevVidya);
			prevVidya = vidya;

			return vidya;
		}

		#region Properties

		[NinjaScriptProperty]
		[Range(5, 100)]
		[Display(Name = "FTC Period", Order = 1, GroupName = "FTC Parameters")]
		public int FtcPeriod
		{
			get { return ftcPeriod; }
			set { ftcPeriod = Math.Max(5, value); }
		}

		[NinjaScriptProperty]
		[Range(5, 100)]
		[Display(Name = "ATR Period", Order = 2, GroupName = "FTC Parameters")]
		public int AtrPeriod
		{
			get { return atrPeriod; }
			set { atrPeriod = Math.Max(5, value); }
		}

		[NinjaScriptProperty]
		[Range(5, 100)]
		[Display(Name = "VIDYA Period", Order = 3, GroupName = "Signal Parameters")]
		public int VidyaPeriod
		{
			get { return vidyaPeriod; }
			set { vidyaPeriod = Math.Max(5, value); }
		}

		[NinjaScriptProperty]
		[Range(1, double.MaxValue)]
		[Display(Name = "Stop Loss Points", Order = 1, GroupName = "Entry Parameters")]
		public double StopLossPoints
		{
			get { return stopLossPoints; }
			set { stopLossPoints = value; }
		}

		[NinjaScriptProperty]
		[Range(1, double.MaxValue)]
		[Display(Name = "Profit Target Points", Order = 2, GroupName = "Entry Parameters")]
		public double ProfitTargetPoints
		{
			get { return profitTargetPoints; }
			set { profitTargetPoints = value; }
		}

		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Name = "Breakeven Move Points", Order = 3, GroupName = "Entry Parameters")]
		public double BreakEvenMovePoints
		{
			get { return breakEvenMovePoints; }
			set { breakEvenMovePoints = value; }
		}

		[NinjaScriptProperty]
		[Range(1, 10)]
		[Display(Name = "Contract Size", Order = 4, GroupName = "Entry Parameters")]
		public int ContractSize
		{
			get { return contractSize; }
			set { contractSize = value; }
		}

		#endregion
	}
}
