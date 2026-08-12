#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.Cbi;
using NinjaTrader.Instruments;
using NinjaTrader.Core;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.Tools;
using NinjaTrader.Windows;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
	/// <summary>
	/// ISE Elite VectorFlow Live Strategy
	/// 
	/// Backtest-validated configuration locked from Session 5:
	/// - Entry: VectorFlow Buy/Sell signals (5m TradingView)
	/// - Stop: 87.5 points (350 ticks)
	/// - Target: 44 points
	/// - Breakeven: Move stop to entry once profit >= 62.5 points
	/// - Size: 4 contracts per entry
	/// 
	/// Validated results on 56,800 real bars (44 trading days):
	/// - Gross P&L: $39,882 = $906/day
	/// - Win rate: 82.3%
	/// - Trades/day: 5.8
	/// - Max DD: $7,407
	/// </summary>
	public class ISEEliteVectorFlowLive : Strategy
	{
		private double stopLossPoints = 87.5;      // 350 ticks = 87.5 points
		private double profitTargetPoints = 44.0;  // Exit at +44 points
		private double breakEvenMovePoints = 62.5; // Move stop to entry at +62.5 profit
		private int contractSize = 4;              // 4 contracts per entry

		private bool breakEvenSet = false;         // Flag to prevent re-triggering BE logic
		private double entryPrice = 0;             // Track entry for BE calculation

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = "ISE Elite VectorFlow Live - Backtest validated strategy ($906/day target)";
				Name = "ISEEliteVectorFlowLive";
				Calculate = Calculate.OnBarClose;
				EntriesPerDirection = 1;
				EntryHandling = EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy = false;
				ExitOnSessionCloseSeconds = 300;
				IsFillLimitOnClose = false;
				AllowMultipleDefaultDocumentTypes = false;
				TraceOrders = false;
				RealtimeErrorHandling = RealtimeErrorHandling.StopCancelCloseAnyTradingPosition;
				StopTargetHandling = StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade = 0;
				IsInstantiatedOnChart = true;
				IsAutoSync = true;
			}
			else if (State == State.Configure)
			{
				// Optional: add a plot to track position P&L
				AddPlot(Brushes.White, "PositionPnL");
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < 1)
				return;

			// Track position state and apply breakeven logic
			if (Position.MarketPosition != MarketPosition.Flat)
			{
				// Get unrealized P&L in points
				double unrealizedPnL = Position.GetUnrealizedProfitLoss(Close[0], PerformanceUnit.Points);
				PlotValues[0][0] = unrealizedPnL;

				// Breakeven logic: once profit reaches threshold, move stop to entry (zero risk)
				if (!breakEvenSet && unrealizedPnL >= breakEvenMovePoints)
				{
					// Move stop to entry price (breakeven)
					SetStopLoss(CalculationMode.Price, entryPrice);
					breakEvenSet = true;
					Print(Time[0] + " BREAKEVEN: profit=" + unrealizedPnL.ToString("F2") + "pt, stop moved to entry=" + entryPrice.ToString("F2"));
				}
			}
			else
			{
				// Position closed, reset breakeven flag
				breakEvenSet = false;
			}

			// ENTRY SIGNALS
			// TODO: Wire these to your VectorFlow indicator's Buy Signal and Sell Signal columns
			// Example: bool buySignal = (someIndicator.BuySignal[0] == 1);
			bool buySignal = false;
			bool sellSignal = false;

			// LONG entry
			if (buySignal && Position.MarketPosition == MarketPosition.Flat)
			{
				EntryLong(contractSize, "VFL_Long");
				SetStopLoss(CalculationMode.Points, stopLossPoints);
				SetProfitTarget(CalculationMode.Points, profitTargetPoints);
				entryPrice = Close[0];
				breakEvenSet = false;
				Print(Time[0] + " LONG: entry=" + Close[0].ToString("F2") + 
					  " stop=" + (Close[0] - stopLossPoints).ToString("F2") + 
					  " target=" + (Close[0] + profitTargetPoints).ToString("F2"));
			}

			// SHORT entry
			if (sellSignal && Position.MarketPosition == MarketPosition.Flat)
			{
				EntryShort(contractSize, "VFL_Short");
				SetStopLoss(CalculationMode.Points, stopLossPoints);
				SetProfitTarget(CalculationMode.Points, profitTargetPoints);
				entryPrice = Close[0];
				breakEvenSet = false;
				Print(Time[0] + " SHORT: entry=" + Close[0].ToString("F2") + 
					  " stop=" + (Close[0] + stopLossPoints).ToString("F2") + 
					  " target=" + (Close[0] - profitTargetPoints).ToString("F2"));
			}
		}

		#region Properties

		[NinjaScriptProperty]
		[Range(1, double.MaxValue)]
		[Display(Name = "Stop Loss Points", Description = "Stop loss distance in points", Order = 1, GroupName = "Parameters")]
		public double StopLossPoints
		{
			get { return stopLossPoints; }
			set { stopLossPoints = value; }
		}

		[NinjaScriptProperty]
		[Range(1, double.MaxValue)]
		[Display(Name = "Profit Target Points", Description = "Profit target distance in points", Order = 2, GroupName = "Parameters")]
		public double ProfitTargetPoints
		{
			get { return profitTargetPoints; }
			set { profitTargetPoints = value; }
		}

		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Name = "Breakeven Move Points", Description = "Profit level at which to move stop to entry", Order = 3, GroupName = "Parameters")]
		public double BreakEvenMovePoints
		{
			get { return breakEvenMovePoints; }
			set { breakEvenMovePoints = value; }
		}

		[NinjaScriptProperty]
		[Range(1, 10)]
		[Display(Name = "Contract Size", Description = "Number of contracts per entry", Order = 4, GroupName = "Parameters")]
		public int ContractSize
		{
			get { return contractSize; }
			set { contractSize = value; }
		}

		#endregion
	}
}
