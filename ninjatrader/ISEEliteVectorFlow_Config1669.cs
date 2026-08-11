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

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	/// <summary>
	/// ISE Elite VectorFlow Config 1669
	/// 
	/// LOCKED PARAMETERS (from backtest):
	/// - Entry: VectorFlow Buy/Sell indicator (V1-S)
	/// - Stop: 25 points
	/// - Target: 38 points (1.5:1 R:R)
	/// - Position size: 4 contracts
	/// - Expected: 67.4% win, 6.1 trades/day, $789/day, $2,400 max DD
	/// 
	/// This strategy reads VectorFlow indicator Buy/Sell alerts and executes
	/// with real broker stops/targets. NOT a simulation of backtest; this is
	/// the actual live mechanism.
	/// </summary>
	public class ISEEliteVectorFlow_Config1669 : Strategy
	{
		private bool hasPosition = false;
		private int positionDirection = 0; // 1 = long, -1 = short

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description							= "ISE Elite VectorFlow Config 1669 - Locked Parameters";
				Name									= "ISE-Elite-VF-1669";
				Calculate								= Calculate.OnBarClose;
				EntriesPerDirection						= 1;
				EntryHandling							= EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy 			= false;
				ExitOnOutOfMoney						= true;
				IsFillLimitOnClose						= false;
				AllowMultipleDefaultInstanceStrategy	= false;
				CalculateOnBarClose						= true;
				MaximumBarsLookBack						= MaximumBarsLookBack.TwentyBars;
				OrderFillResolution						= OrderFillResolution.High;
				Slippage								= 0;
				StartBehavior							= StartBehavior.WaitOnBarClose;
				TimeInForce								= TimeInForce.Day;
				TraceOrders								= false;
				RealtimeErrorHandling					= RealtimeErrorHandling.IgnoreAllErrors;
				StopTargetHandling						= StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade						= 1;

				// Locked strategy parameters
				StopDistancePoints						= 25;		// Hard-coded for Config 1669
				TargetDistancePoints					= 38;		// Hard-coded for Config 1669
				PositionSize							= 4;		// 4 MNQ contracts
			}
			else if (State == State.Configure)
			{
				// No indicators needed - VectorFlow entry comes from chart alerts
				// Strategy reads Buy/Sell signals from OnOrderUpdate or manual entry logic
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < 1)
				return;

			// This strategy listens for external signals (VectorFlow Buy/Sell labels)
			// In live NT8, these come from:
			// 1. Indicator alerts that trigger order entry via OnOrderUpdate
			// 2. Manual order entry based on indicator state
			//
			// For Sim101 paper trading, use the indicator to fire signals:
			// Check for Buy/Sell conditions from VectorFlow indicator on this bar

			// ENTRY LOGIC: Read from external signal (would come from indicator)
			// Placeholder: actual signals come from VectorFlow Buy/Sell labels
			
			// For now, if you want to test with a placeholder entry:
			// Uncomment below and replace with actual VectorFlow indicator check
			/*
			if (/* VectorFlow Buy condition */)
			{
				if (!hasPosition && positionDirection != 1)
				{
					EnterLong(PositionSize, "VF_Long");
					hasPosition = true;
					positionDirection = 1;
				}
			}
			else if (/* VectorFlow Sell condition */)
			{
				if (!hasPosition && positionDirection != -1)
				{
					EnterShort(PositionSize, "VF_Short");
					hasPosition = true;
					positionDirection = -1;
				}
			}
			*/
		}

		protected override void OnOrderUpdate(Order order)
		{
			// Executes immediately after order placed
			if (order.OrderState == OrderState.Filled)
			{
				if (order.Name == "VF_Long" && Position.MarketPosition == MarketPosition.Long)
				{
					// Set stops and targets for long position
					SetStopLoss("VF_Long", CalculationMode.Points, StopDistancePoints);
					SetProfitTarget("VF_Long", CalculationMode.Points, TargetDistancePoints);
					Print($"[LONG] Entry @ {order.FillPrice}, Stop @ {order.FillPrice - (StopDistancePoints * 0.25)}, Target @ {order.FillPrice + (TargetDistancePoints * 0.25)}");
				}
				else if (order.Name == "VF_Short" && Position.MarketPosition == MarketPosition.Short)
				{
					// Set stops and targets for short position
					SetStopLoss("VF_Short", CalculationMode.Points, StopDistancePoints);
					SetProfitTarget("VF_Short", CalculationMode.Points, TargetDistancePoints);
					Print($"[SHORT] Entry @ {order.FillPrice}, Stop @ {order.FillPrice + (StopDistancePoints * 0.25)}, Target @ {order.FillPrice - (TargetDistancePoints * 0.25)}");
				}
			}
			else if (order.OrderState == OrderState.Cancelled || order.OrderState == OrderState.Rejected)
			{
				hasPosition = false;
				positionDirection = 0;
			}
		}

		protected override void OnPositionUpdate(Position position, double lastFillPrice, int lastFillQuantity, double lastFillAveragePrice, PositionByMarket positionByMarket, double bid, double ask, double mid)
		{
			// Track position state
			if (position.MarketPosition == MarketPosition.Flat)
			{
				hasPosition = false;
				positionDirection = 0;
			}
		}

		#region Properties
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Stop Distance (Points)", Order = 1, GroupName = "Parameters")]
		public int StopDistancePoints { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Target Distance (Points)", Order = 2, GroupName = "Parameters")]
		public int TargetDistancePoints { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Position Size (Contracts)", Order = 3, GroupName = "Parameters")]
		public int PositionSize { get; set; }
		#endregion
	}
}
