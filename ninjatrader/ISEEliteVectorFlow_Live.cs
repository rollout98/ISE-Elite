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
				Description = "ISE Elite VectorFlow Live";
				Name = "ISEEliteVectorFlowLive";
				Calculate = Calculate.OnBarClose;
				EntriesPerDirection = 1;
				EntryHandling = EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy = false;
				ExitOnSessionCloseSeconds = 300;
				StopTargetHandling = StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade = 0;
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < 1)
				return;

			// Breakeven logic - move stop to entry once in profit
			if (Position.MarketPosition == MarketPosition.Long && !breakEvenSet)
			{
				if (Close[0] > entryPrice + (breakEvenMovePoints * 0.25))  // 0.25 = point value for MNQ
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

			// Wire VectorFlow indicator signals
			// The VectorFlow indicator plots BUY/SELL columns; check if they fired this bar
			bool buySignal = false;
			bool sellSignal = false;

			try
			{
				// Try to get VectorFlow indicator (check common naming patterns)
				dynamic vf = null;
				
				// Attempt 1: VectorFlowV1S (most likely based on file naming)
				try { vf = Indicators.VectorFlowV1S(Close); }
				catch { }
				
				// Attempt 2: VectorFlowAlgoV1S
				if (vf == null)
					try { vf = Indicators.VectorFlowAlgoV1S(Close); }
					catch { }
				
				// Attempt 3: VectorFlowV1SNT8
				if (vf == null)
					try { vf = Indicators.VectorFlowV1SNT8(Close); }
					catch { }

				// If we found the indicator, read Buy Signal and Sell Signal columns
				if (vf != null)
				{
					try
					{
						buySignal = (vf.BuySignal != null && vf.BuySignal[0] > 0);
						sellSignal = (vf.SellSignal != null && vf.SellSignal[0] > 0);
					}
					catch
					{
						// Column names might be different; try alternative names
						try
						{
							buySignal = (vf.Buy != null && vf.Buy[0] > 0);
							sellSignal = (vf.Sell != null && vf.Sell[0] > 0);
						}
						catch { }
					}
				}
			}
			catch
			{
				// If indicator not found, signals remain false
				// Check NinjaTrader Output window for details
			}

			// LONG entry
			if (buySignal && Position.MarketPosition == MarketPosition.Flat)
			{
				EnterLong(contractSize, "Long");
				SetStopLoss(CalculationMode.Price, Close[0] - (stopLossPoints * 0.25));  // 87.5 points down
				SetProfitTarget(CalculationMode.Price, Close[0] + (profitTargetPoints * 0.25));  // 44 points up
				entryPrice = Close[0];
				breakEvenSet = false;
				Print(Time[0] + " LONG entry at " + Close[0] + " | Stop: " + (Close[0] - (stopLossPoints * 0.25)) + " | Target: " + (Close[0] + (profitTargetPoints * 0.25)));
			}

			// SHORT entry
			if (sellSignal && Position.MarketPosition == MarketPosition.Flat)
			{
				EnterShort(contractSize, "Short");
				SetStopLoss(CalculationMode.Price, Close[0] + (stopLossPoints * 0.25));  // 87.5 points up
				SetProfitTarget(CalculationMode.Price, Close[0] - (profitTargetPoints * 0.25));  // 44 points down
				entryPrice = Close[0];
				breakEvenSet = false;
				Print(Time[0] + " SHORT entry at " + Close[0] + " | Stop: " + (Close[0] + (stopLossPoints * 0.25)) + " | Target: " + (Close[0] - (profitTargetPoints * 0.25)));
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
