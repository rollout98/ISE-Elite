using System;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.Cbi;
using NinjaTrader.Instruments;
using NinjaTrader.Core;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;

// CORRECT VectorFlow V1-S NinjaScript Implementation
// Based on: VectorFlow_Algo_V1-S-NQ-EXIT Pine v6
// Date: August 12, 2026
// Purpose: Trend-following system (not scalping) - hold until reversal

namespace NinjaTrader.NinjaScript.Strategies
{
	public class ISEEliteVectorFlowCORRECT : Strategy
	{
		// ==========================================================================
		// FTC PARAMETERS (Fundamental Trend Channel)
		// ==========================================================================
		private int ftcPeriod = 100;      // SMA lookback for trend
		private int atrPeriod = 100;      // ATR lookback for channel width
		
		// ==========================================================================
		// VIDYA PARAMETERS (Volatility-Adjusted EMA)
		// ==========================================================================
		private int vidyaPeriod = 20;     // EMA lookback for VIDYA
		private int vidyaMomentum = 20;   // CMO momentum lookback
		private int vidyaSmooth = 15;     // SMA smoothing of VIDYA
		private int atrBandPeriod = 200;  // ATR for VIDYA band distance
		private double bandDistance = 2.0; // Multiplier for band width
		
		// ==========================================================================
		// POSITION MANAGEMENT PARAMETERS
		// ==========================================================================
		private double stopLossTicks = 87.5;     // 87.5 ticks = 21.875 points on MNQ
		private int contracts = 4;
		
		// ==========================================================================
		// STATE TRACKING (LATCHED)
		// ==========================================================================
		// FTC Trend: TRUE = bullish, FALSE = bearish
		private bool ftcTrendLatch = false;
		private bool prevFtcTrendLatch = false;
		
		// VIDYA Up: TRUE = above upper band, FALSE = below lower band
		private bool vidyaUpLatch = false;
		private bool prevVidyaUpLatch = false;
		
		// Position tracking
		private bool inLongTrade = false;
		private bool inShortTrade = false;
		private double entryPrice = 0;
		private double positionStopPrice = 0;
		
		// VIDYA rolling state
		private double vidyaValue = 0;
		private double prevVidyaValue = 0;
		
		// VIDYA smoothing buffer (track recent values for SMA)
		private double[] vidyaSmoothBuffer = new double[20];
		private int smoothBufferIndex = 0;
		
		// ==========================================================================
		// INITIALIZATION
		// ==========================================================================
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = "ISE Elite - VectorFlow V1-S (CORRECT: Latched, Hold-Until-Reversal)";
				Name = "ISEEliteVectorFlowCORRECT";
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
				vidyaValue = Close[0];
			}
		}

		// ==========================================================================
		// MAIN SIGNAL GENERATION
		// ==========================================================================
		protected override void OnBarUpdate()
		{
			if (CurrentBar < ftcPeriod)
				return;

			// Calculate FTC (Fundamental Trend Channel)
			double smaFTC = SMA(Close, ftcPeriod)[0];
			double atrFTC = ATR(Close, atrPeriod)[0];
			double ftcUpper = smaFTC + atrFTC;
			double ftcLower = smaFTC - atrFTC;

			// ===== FTC TREND LATCH =====
			// Becomes TRUE on crossover above upper band
			if (Close[0] > ftcUpper && !ftcTrendLatch)
			{
				ftcTrendLatch = true;
			}
			// Becomes FALSE on crossunder below lower band
			else if (Close[0] < ftcLower && ftcTrendLatch)
			{
				ftcTrendLatch = false;
			}

			// Calculate VIDYA with CMO-based adaptive alpha
			vidyaValue = CalculateVIDYA();
			
			// Smooth VIDYA with manual SMA (simple moving average)
			double vidyaSmoothed = CalculateSimpleMA(vidyaValue, vidyaSmooth);
			
			// Calculate VIDYA bands
			double atrBands = ATR(Close, atrBandPeriod)[0];
			double vidyaUpper = vidyaSmoothed + (bandDistance * atrBands);
			double vidyaLower = vidyaSmoothed - (bandDistance * atrBands);

			// ===== VIDYA UP LATCH =====
			// Becomes TRUE on crossover above upper band
			if (Close[0] > vidyaUpper && !vidyaUpLatch)
			{
				vidyaUpLatch = true;
			}
			// Becomes FALSE on crossunder below lower band
			else if (Close[0] < vidyaLower && vidyaUpLatch)
			{
				vidyaUpLatch = false;
			}

			// ==========================================================================
			// SIGNAL DETECTION (LATCHED STATE ALIGNMENT)
			// ==========================================================================
			bool buyCondition = ftcTrendLatch && vidyaUpLatch;       // Both bullish
			bool sellCondition = !ftcTrendLatch && !vidyaUpLatch;    // Both bearish
			
			// Track previous state for EDGE detection
			bool prevBuyCondition = prevFtcTrendLatch && prevVidyaUpLatch;
			bool prevSellCondition = !prevFtcTrendLatch && !prevVidyaUpLatch;
			
			// ===== ENTRY SIGNALS (EDGE TRIGGERED) =====
			bool buyNow = buyCondition && !prevBuyCondition && Position.MarketPosition == MarketPosition.Flat;
			bool sellNow = sellCondition && !prevSellCondition && Position.MarketPosition == MarketPosition.Flat;

			// ==========================================================================
			// ENTRY EXECUTION
			// ==========================================================================
			if (buyNow)
			{
				Print(Time[0] + " BUY SIGNAL at " + Close[0]);
				EnterLong(contracts, "Long");
				inLongTrade = true;
				inShortTrade = false;
				entryPrice = Close[0];
				positionStopPrice = Close[0] - (stopLossTicks * 0.25); // Convert ticks to points
				SetStopLoss(CalculationMode.Price, positionStopPrice);
			}
			else if (sellNow)
			{
				Print(Time[0] + " SELL SIGNAL at " + Close[0]);
				EnterShort(contracts, "Short");
				inShortTrade = true;
				inLongTrade = false;
				entryPrice = Close[0];
				positionStopPrice = Close[0] + (stopLossTicks * 0.25); // Convert ticks to points
				SetStopLoss(CalculationMode.Price, positionStopPrice);
			}

			// ==========================================================================
			// EXIT LOGIC (HOLD UNTIL REVERSAL)
			// ==========================================================================
			// Exit LONG when SELL signal fires (alignment reverses to bearish)
			if (inLongTrade && sellNow)
			{
				Print(Time[0] + " EXIT LONG on SELL signal at " + Close[0]);
				ExitLong();
				inLongTrade = false;
			}
			
			// Exit SHORT when BUY signal fires (alignment reverses to bullish)
			if (inShortTrade && buyNow)
			{
				Print(Time[0] + " EXIT SHORT on BUY signal at " + Close[0]);
				ExitShort();
				inShortTrade = false;
			}

			// ==========================================================================
			// UPDATE STATE FOR NEXT BAR
			// ==========================================================================
			prevFtcTrendLatch = ftcTrendLatch;
			prevVidyaUpLatch = vidyaUpLatch;
			prevVidyaValue = vidyaValue;
		}

		// ==========================================================================
		// VIDYA CALCULATION (CMO-based adaptive EMA)
		// ==========================================================================
		private double CalculateVIDYA()
		{
			if (CurrentBar < vidyaMomentum)
				return Close[0];
			
			// Calculate momentum sum (positive and negative)
			double positiveSum = 0;
			double negativeSum = 0;
			
			for (int i = 0; i < vidyaMomentum; i++)
			{
				double change = Close[i] - Close[i + 1];
				if (change >= 0)
					positiveSum += change;
				else
					negativeSum += Math.Abs(change);
			}
			
			// Calculate CMO (Chande Momentum Oscillator)
			double totalSum = positiveSum + negativeSum;
			double cmo = totalSum > 0 ? Math.Abs(100 * (positiveSum - negativeSum) / totalSum) : 0;
			
			// Adaptive alpha based on CMO
			double baseAlpha = 2.0 / (vidyaPeriod + 1);
			double adaptiveAlpha = baseAlpha * (cmo / 100.0);
			
			// Update VIDYA with adaptive alpha
			double newVIDYA = (adaptiveAlpha * Close[0]) + ((1 - adaptiveAlpha) * vidyaValue);
			
			return newVIDYA;
		}

		// ==========================================================================
		// SIMPLE MOVING AVERAGE HELPER FOR VIDYA SMOOTHING
		// ==========================================================================
		private double CalculateSimpleMA(double newValue, int period)
		{
			// Add new value to buffer (circular)
			vidyaSmoothBuffer[smoothBufferIndex % period] = newValue;
			smoothBufferIndex++;
			
			// Calculate average
			double sum = 0;
			int count = Math.Min(smoothBufferIndex, period);
			for (int i = 0; i < count; i++)
			{
				sum += vidyaSmoothBuffer[i];
			}
			
			return sum / count;
		}
	}
}
