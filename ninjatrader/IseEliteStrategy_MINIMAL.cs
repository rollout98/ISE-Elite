#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.Cbi;
using NinjaTrader.Instrument;
using NinjaTrader.Core;
using NinjaTrader.Data;
using NinjaTrader.Strategy;
#endregion

namespace NinjaTrader.Strategy
{
	public class IseEliteStrategyMinimal : Strategy
	{
		private decimal openingEquity;
		private decimal dailyPnL;
		private int tradestoday;
		private DateTime lastDate;
		private bool hasPosition;
		private decimal entryPrice;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"ISE Elite - Minimal Trading System";
				Name										= "IseEliteStrategyMinimal";
				Calculate									= Calculate.OnBarClose;
				EntriesPerDirection							= 1;
				EntryHandling								= EntryHandling.AllEntries;
				IsExitOnSessionClose						= false;
				IsFillLimitOnClose							= false;
				TraceOrders									= false;
				RealtimeErrorHandling						= RealtimeErrorHandling.TakeNoAction;
				StopOutdatedOrders							= false;
				BarsRequiredToTrade							= 20;
			}
		}

		protected override void OnBarUpdate()
		{
			// Skip if not enough bars
			if (CurrentBar < 20)
				return;

			// Daily reset
			if (Time[0].Date != lastDate)
			{
				lastDate = Time[0].Date;
				openingEquity = Account.Get(AccountItem.Cash, Currency.UsDollar);
				dailyPnL = 0;
				tradestoday = 0;
				Print($"📅 NEW DAY: {lastDate:MM-dd-yyyy}");
			}

			// Session check: 8:30 AM - 3:00 PM CT
			int hour = Time[0].Hour;
			int minute = Time[0].Minute;
			bool inSession = (hour > 8 || (hour == 8 && minute >= 30)) && (hour < 15 || (hour == 15 && minute < 0));

			if (!inSession || tradestoday >= 10)
				return;

			// Force close at 2:55 PM CT
			if (hour == 14 && minute >= 55 && hasPosition)
			{
				ExitLong(signalName: "SessionEnd");
				hasPosition = false;
				return;
			}

			// ENTRY: 5-bar > 10-bar (trend following)
			if (!hasPosition)
			{
				decimal avg5 = (Close[0] + Close[1] + Close[2] + Close[3] + Close[4]) / 5m;
				decimal avg10 = 0;
				for (int i = 0; i < 10; i++)
					avg10 += (decimal)Close[i];
				avg10 /= 10m;

				if (avg5 > avg10 && Close[0] > avg5)
				{
					EnterLong(1, "Entry");
					entryPrice = (decimal)Close[0];
					hasPosition = true;
					tradestoday++;
					Print($"📈 ENTRY @ {entryPrice}");
				}
			}

			// EXIT: +3 pts OR -1 pt OR timeout
			if (hasPosition)
			{
				decimal change = (decimal)Close[0] - entryPrice;
				decimal pnl = change * 20m; // $20/pt for MNQ

				if (change >= 3m || change <= -1m)
				{
					dailyPnL += pnl;
					ExitLong(signalName: change >= 3m ? "ProfitTarget" : "StopLoss");
					hasPosition = false;
					Print($"📉 EXIT @ {Close[0]} | PnL: {pnl}");
				}
			}
		}
	}
}
