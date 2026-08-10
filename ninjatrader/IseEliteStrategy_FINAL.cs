#region Using declarations
using NinjaTrader.Cbi;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
	public class IseEliteStrategyFinal : Strategy
	{
		private double sma5;
		private double sma10;
		private bool hasPosition;
		private double entryPrice;
		private int barsInTrade;
		private DateTime lastTradeDay = DateTime.MinValue;
		private int tradesThisDay;
		private double openingEquity;
		private double dailyPnL;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"ISE Elite Automated Trading System";
				Name = "IseEliteStrategyFinal";
				Calculate = Calculate.OnBarClose;
				EntriesPerDirection = 1;
				EntryHandling = EntryHandling.AllEntries;
				IsExitOnSessionClose = false;
				IsFillLimitOnClose = false;
				TraceOrders = false;
				RealtimeErrorHandling = RealtimeErrorHandling.TakeNoAction;
				StopOutdatedOrders = false;
				BarsRequiredToTrade = 20;
			}
			else if (State == State.Realtime)
			{
				Print("✅ ISE Elite Strategy LIVE");
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < BarsRequiredToTrade)
				return;

			// Daily reset
			if (Time[0].Date != lastTradeDay)
			{
				lastTradeDay = Time[0].Date;
				openingEquity = Account.Get(AccountItem.CashValue, Currency.UsDollar);
				dailyPnL = 0;
				tradesThisDay = 0;
				Print(string.Format("📅 NEW DAY: Opening ${0:F0}", openingEquity));
			}

			// Session check: 8:30 AM - 3:00 PM CT
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

			// Not in session or max trades
			bool inSession = (hour > 8 || (hour == 8 && minute >= 30)) && (hour < 15);
			if (!inSession || tradesThisDay >= 10)
				return;

			// Calculate SMAs
			sma5 = Close.Average(5);
			sma10 = Close.Average(10);

			// ENTRY LOGIC
			if (!hasPosition && tradesThisDay < 10)
			{
				if (sma5 > sma10 && Close[0] > sma5)
				{
					EnterLong(1, "Entry");
					entryPrice = Close[0];
					hasPosition = true;
					barsInTrade = 0;
					tradesThisDay++;
					Print(string.Format("📈 ENTRY @ {0:F2} | Trades today: {1}", entryPrice, tradesThisDay));
				}
			}

			// EXIT LOGIC
			if (hasPosition)
			{
				barsInTrade++;
				double priceChange = Close[0] - entryPrice;
				double pnl = priceChange * 20 * 1;

				if (priceChange >= 3.0 || priceChange <= -1.0 || barsInTrade >= 30)
				{
					dailyPnL += pnl;
					string reason = priceChange >= 3.0 ? "ProfitTarget" : (priceChange <= -1.0 ? "StopLoss" : "Timeout");
					ExitLong(signalName: reason);
					hasPosition = false;
					Print(string.Format("📉 EXIT @ {0:F2} | PnL: ${1:F0} | {2}", Close[0], pnl, reason));
				}
			}

			// Daily report at 3:00 PM
			if (hour == 15 && minute == 0)
			{
				double closingEquity = Account.Get(AccountItem.CashValue, Currency.UsDollar);
				double dayPnL = closingEquity - openingEquity;
				Print(string.Format("\n📊 DAILY SUMMARY: PnL ${0:F0} | Trades: {1}\n", dayPnL, tradesThisDay));
			}
		}
	}
}
