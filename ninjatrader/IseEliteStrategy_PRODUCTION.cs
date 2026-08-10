#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using NinjaTrader.Cbi;
using NinjaTrader.Instrument;
using NinjaTrader.Core;
using NinjaTrader.Data;
using NinjaTrader.Strategy;
#endregion

namespace NinjaTrader.Strategy
{
	public class IseEliteStrategyProduction : Strategy
	{
		private double sma5 = 0;
		private double sma10 = 0;
		private bool hasPosition = false;
		private double entryPrice = 0;
		private int barsInTrade = 0;
		private DateTime lastTradeDay = DateTime.MinValue;
		private int tradesThisDay = 0;
		private double openingEquity = 0;
		private double dailyPnL = 0;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"ISE Elite Production Strategy";
				Name										= "IseEliteStrategyProduction";
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
			else if (State == State.Realtime)
			{
				Print("✅ ISE Elite Strategy LIVE");
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < 20)
				return;

			// === DAILY RESET ===
			if (Time[0].Date != lastTradeDay)
			{
				lastTradeDay = Time[0].Date;
				openingEquity = Account.Get(AccountItem.CashValue, Currency.UsDollar);
				dailyPnL = 0;
				tradesThisDay = 0;
				Print($"📅 NEW DAY: Opening ${openingEquity:F0}");
			}

			// === SESSION CHECK ===
			// 8:30 AM - 3:00 PM CT (9:30 AM - 4:00 PM ET)
			int hour = Time[0].Hour;
			int minute = Time[0].Minute;
			
			// Force close at 2:55 PM CT
			if (hour == 14 && minute >= 55 && hasPosition)
			{
				ExitLong(signalName: "ForcedClose");
				hasPosition = false;
				Print("⏰ FORCED CLOSE at 14:55 CT");
				return;
			}

			// Not in session
			bool inSession = (hour > 8 || (hour == 8 && minute >= 30)) && (hour < 15);
			if (!inSession || tradesThisDay >= 10)
				return;

			// === CALCULATE SMAs ===
			sma5 = Close.Average(5);
			sma10 = Close.Average(10);

			// === ENTRY LOGIC ===
			if (!hasPosition && tradesThisDay < 10)
			{
				if (sma5 > sma10 && Close[0] > sma5)
				{
					EnterLong(1, "Entry");
					entryPrice = Close[0];
					hasPosition = true;
					barsInTrade = 0;
					tradesThisDay++;
					Print($"📈 ENTRY @ {entryPrice:F2} | Trades today: {tradesThisDay}");
				}
			}

			// === EXIT LOGIC ===
			if (hasPosition)
			{
				barsInTrade++;
				double priceChange = Close[0] - entryPrice;
				double pnl = priceChange * 20 * 1; // $20/point per contract

				// Exit: +3 points, -1 point, or 30 bars
				if (priceChange >= 3.0 || priceChange <= -1.0 || barsInTrade >= 30)
				{
					dailyPnL += pnl;
					string reason = priceChange >= 3.0 ? "ProfitTarget" : priceChange <= -1.0 ? "StopLoss" : "Timeout";
					ExitLong(signalName: reason);
					hasPosition = false;
					Print($"📉 EXIT @ {Close[0]:F2} | PnL: ${pnl:F0} | {reason}");
				}
			}

			// === DAILY REPORT AT 3:00 PM ===
			if (hour == 15 && minute == 0)
			{
				double closingEquity = Account.Get(AccountItem.CashValue, Currency.UsDollar);
				double dayPnL = closingEquity - openingEquity;
				Print($"\n📊 DAILY SUMMARY: PnL ${dayPnL:F0} | Trades: {tradesThisDay}\n");
			}
		}
	}
}
