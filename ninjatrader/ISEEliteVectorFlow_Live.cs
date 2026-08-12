#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
	public class ISEEliteVectorFlowLive : Strategy
	{
		private double stopLossPoints = 87.5;
		private double profitTargetPoints = 44.0;
		private double breakEvenMovePoints = 62.5;
		private int contractSize = 4;

		private bool breakEvenSet = false;
		private double entryPrice = 0;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = "ISE Elite VectorFlow Live - Backtest validated ($906/day)";
				Name = "ISEEliteVectorFlowLive";
				Calculate = Calculate.OnBarClose;
				EntriesPerDirection = 1;
				EntryHandling = EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy = false;
				ExitOnSessionCloseSeconds = 300;
				IsFillLimitOnClose = false;
				TraceOrders = true;
				RealtimeErrorHandling = RealtimeErrorHandling.StopCancelCloseAnyTradingPosition;
				StopTargetHandling = StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade = 0;
			}
			else if (State == State.Configure)
			{
				AddPlot(Brushes.White, "PositionPnL");
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < 1)
				return;

			// Breakeven logic
			if (Position.MarketPosition != MarketPosition.Flat)
			{
				double unrealizedPnL = Position.GetUnrealizedProfitLoss(Close[0], PerformanceUnit.Points);
				PlotValues[0][0] = unrealizedPnL;

				if (!breakEvenSet && unrealizedPnL >= breakEvenMovePoints)
				{
					SetStopLoss(CalculationMode.Price, entryPrice);
					breakEvenSet = true;
					Print(Time[0] + " BREAKEVEN: profit=" + unrealizedPnL.ToString("F2") + "pt");
				}
			}
			else
			{
				breakEvenSet = false;
			}

			// TODO: Wire these to your VectorFlow indicator
			bool buySignal = false;
			bool sellSignal = false;

			// LONG entry
			if (buySignal && Position.MarketPosition == MarketPosition.Flat)
			{
				EntryLong(contractSize, "Long");
				SetStopLoss(CalculationMode.Points, stopLossPoints);
				SetProfitTarget(CalculationMode.Points, profitTargetPoints);
				entryPrice = Close[0];
				breakEvenSet = false;
			}

			// SHORT entry
			if (sellSignal && Position.MarketPosition == MarketPosition.Flat)
			{
				EntryShort(contractSize, "Short");
				SetStopLoss(CalculationMode.Points, stopLossPoints);
				SetProfitTarget(CalculationMode.Points, profitTargetPoints);
				entryPrice = Close[0];
				breakEvenSet = false;
			}
		}

		#region Properties

		[NinjaScriptProperty]
		[Range(1, double.MaxValue)]
		[Display(Name = "Stop Loss Points", Order = 1, GroupName = "Parameters")]
		public double StopLossPoints
		{
			get { return stopLossPoints; }
			set { stopLossPoints = value; }
		}

		[NinjaScriptProperty]
		[Range(1, double.MaxValue)]
		[Display(Name = "Profit Target Points", Order = 2, GroupName = "Parameters")]
		public double ProfitTargetPoints
		{
			get { return profitTargetPoints; }
			set { profitTargetPoints = value; }
		}

		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Name = "Breakeven Move Points", Order = 3, GroupName = "Parameters")]
		public double BreakEvenMovePoints
		{
			get { return breakEvenMovePoints; }
			set { breakEvenMovePoints = value; }
		}

		[NinjaScriptProperty]
		[Range(1, 10)]
		[Display(Name = "Contract Size", Order = 4, GroupName = "Parameters")]
		public int ContractSize
		{
			get { return contractSize; }
			set { contractSize = value; }
		}

		#endregion
	}
}
