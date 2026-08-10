#region Using declarations
using System;
using NinjaTrader.Cbi;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
	public class IseEliteStrategySessionOptimized : Strategy
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
		private int currentContracts;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"ISE Elite Automated Trading System - Session Optimized (1 AM CT Start)";
				Name = "IseEliteStrategySessionOptimized";
				Calculate = Calculate.OnBarClose;
				EntriesPerDirection = 1;
				EntryHandling = EntryHandling.AllEntries;
				TraceOrders = false;
				BarsRequiredToTrade = 20;
			}
			else if (State == State.Realtime)
			{
				Print("✅ ISE Elite Strategy LIVE - 1 AM CT Start, Session Optimized");
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < BarsRequiredToTrade)
				return;

			// Daily reset at midnight
			if (Time[0].Date != lastTradeDay)
			{
				lastTradeDay = Time[0].Date;
				openingEquity = Account.Get(AccountItem.CashValue, Currency.UsDollar);
				dailyPnL = 0;
				tradesThisDay = 0;
				Print(string.Format("📅 NEW DAY: Opening ${0:F0}", openingEquity));
			}

			// Determine session and contract size
			int hour = Time[0].Hour;
			GetSessionAndContracts(hour, out string session, out int contracts);

			// If not trading this hour, skip
			if (contracts == 0)
			{
				if (hasPosition)
				{
					Print(string.Format("⏸️  Pause trading window ({0}). Exiting position.", session));
					ExitLong();
					hasPosition = false;
				}
				return;
			}

			currentContracts = contracts;

			// Calculate 5-bar and 10-bar SMA
			sma5 = 0;
			sma10 = 0;
			for (int i = 0; i < 5; i++)
				sma5 += Close[i];
			sma5 /= 5;

			for (int i = 0; i < 10; i++)
				sma10 += Close[i];
			sma10 /= 10;

			// FORCE CLOSE at 3:55 PM CT (market close approach)
			if (hour == 15 && Time[0].Minute >= 55 && hasPosition)
			{
				double priceChange = Close[0] - entryPrice;
				double pnl = priceChange * 20 * currentContracts;
				dailyPnL += pnl;
				Print(string.Format("📊 FORCE CLOSE 3:55 PM: ${0:F0} | Daily P&L: ${1:F0}", pnl, dailyPnL));
				ExitLong();
				hasPosition = false;
				return;
			}

			// EXIT LOGIC if in position
			if (hasPosition)
			{
				double priceChange = Close[0] - entryPrice;

				// Profit target: +3 points
				if (priceChange >= 3.0)
				{
					double pnl = 3.0 * 20 * currentContracts;
					dailyPnL += pnl;
					Print(string.Format("📈 ENTRY @{0:F2} EXIT @{1:F2} | Target Hit +3 | P&L ${2:F0} | Daily: ${3:F0}", 
						entryPrice, Close[0], pnl, dailyPnL));
					ExitLong();
					hasPosition = false;
					return;
				}

				// Stop loss: -1 point
				if (priceChange <= -1.0)
				{
					double pnl = -1.0 * 20 * currentContracts;
					dailyPnL += pnl;
					Print(string.Format("📉 ENTRY @{0:F2} EXIT @{1:F2} | Stop Hit -1 | P&L ${2:F0} | Daily: ${3:F0}", 
						entryPrice, Close[0], pnl, dailyPnL));
					ExitLong();
					hasPosition = false;
					return;
				}

				// Timeout: 30 bars
				barsInTrade++;
				if (barsInTrade >= 30)
				{
					double pnl = priceChange * 20 * currentContracts;
					dailyPnL += pnl;
					Print(string.Format("⏱️  ENTRY @{0:F2} EXIT @{1:F2} | 30-bar Timeout | P&L ${2:F0} | Daily: ${3:F0}", 
						entryPrice, Close[0], pnl, dailyPnL));
					ExitLong();
					hasPosition = false;
					return;
				}
			}

			// ENTRY LOGIC
			if (!hasPosition && tradesThisDay < 10)
			{
				// Signal: 5-bar SMA > 10-bar SMA AND close > 5-bar SMA
				if (sma5 > sma10 && Close[0] > sma5)
				{
					entryPrice = Close[0];
					barsInTrade = 0;
					hasPosition = true;
					tradesThisDay++;
					Print(string.Format("📈 ENTRY #{0} @{1:F2} | SMA5:{2:F2} > SMA10:{3:F2} | Contracts: {4} | Session: {5}", 
						tradesThisDay, entryPrice, sma5, sma10, currentContracts, session));
					EnterLong(currentContracts, "Entry");
				}
			}

			// Daily summary at 3 PM
			if (hour == 15 && Time[0].Minute == 0)
			{
				Print(string.Format("📊 DAILY SUMMARY: {0} trades | Daily P&L: ${1:F0} | Session: Close", 
					tradesThisDay, dailyPnL));
			}
		}

		private void GetSessionAndContracts(int hour, out string session, out int contracts)
		{
			// 1:00-3:00 AM CT: London early (1 contract)
			if (hour >= 1 && hour < 4)
			{
				session = "London-Early";
				contracts = 1;
				return;
			}

			// 4:00-8:00 AM CT: SKIP (losing period)
			if (hour >= 4 && hour < 8)
			{
				session = "Pause (Lose Period)";
				contracts = 0;
				return;
			}

			// 8:00 AM - 4:00 PM CT: NY session (2 contracts)
			if (hour >= 8 && hour < 16)
			{
				session = "NY Session";
				contracts = 2;
				return;
			}

			// Default: no trading
			session = "After Hours";
			contracts = 0;
		}
	}
}
