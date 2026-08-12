#region Using declarations
using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
	/// <summary>
	/// ISE ELITE - MGC BRIDGE STRATEGY
	///
	/// Executes VectorFlow signals that originate in TradingView. NinjaTrader does NOT
	/// compute the signal: a port was attempted and verified against the real indicator
	/// on 2026-08-12, and it fired 11 times where VectorFlow fired 7, consistently 5-65
	/// minutes late. Rather than trade an approximation, this reads the genuine signal.
	///
	/// SIGNAL FILE (append-only CSV, one line per signal):
	///     id,timestampUtc,instrument,action
	///     a1b2,2026-08-12T14:35:00Z,MGC,BUY
	///
	/// action = BUY | SELL | FLAT
	///
	/// Each line is processed at most once (tracked by id) and ignored entirely if it
	/// is older than SignalMaxAgeMinutes, so a backlog after a restart cannot fire a
	/// burst of stale entries into a live market.
	///
	/// RISK MODEL - from the 74-day MGC backtest:
	///   - Hold to reversal. An opposite signal closes and reverses; a same-side signal
	///     while in position is ignored ("exit governs entry").
	///   - Anti-martingale ladder: start at StartContracts, step DOWN one after a
	///     losing trade (floor 1), step UP one after a winner.
	///   - EOD drawdown guard: halts new entries when the account's closing-balance
	///     drawdown approaches the prop limit, because that limit kills the account
	///     outright and no trade is worth the last few hundred dollars of it.
	///
	/// UNVERIFIED: these settings come from an in-sample backtest over Apr-Aug 2026.
	/// No out-of-sample test has been run. Sim first.
	/// </summary>
	public class ISEEliteMGCBridge : Strategy
	{
		#region Parameters

		private string signalFilePath = "";
		private int startContracts = 2;
		private double stopPoints = 15.0;
		private double eodDrawdownLimit = 2000.0;
		private double drawdownBuffer = 400.0;
		private int signalMaxAgeMinutes = 10;
		private bool useLadder = true;

		#endregion

		#region State

		private long lastLineRead = 0;
		private System.Collections.Generic.HashSet<string> processedIds
			= new System.Collections.Generic.HashSet<string>();

		private int ladderSize;
		private double sessionStartBalance;
		private double peakClosingBalance;
		private DateTime currentTradingDay = DateTime.MinValue;
		private bool haltedForDrawdown = false;

		#endregion

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = "Executes TradingView VectorFlow signals on MGC via a file bridge";
				Name        = "ISEEliteMGCBridge";
				Calculate   = Calculate.OnEachTick;   // poll the file frequently, not once per bar
				EntriesPerDirection = 1;
				EntryHandling = EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy = false;  // runners are held overnight by design
				IncludeCommission = true;
				BarsRequiredToTrade = 1;

				SignalFilePath = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
					"NinjaTrader 8", "ISEEliteResearch", "signals-mgc.csv");
			}
			else if (State == State.Configure)
			{
				ladderSize = StartContracts;
			}
			else if (State == State.DataLoaded)
			{
				sessionStartBalance = AccountBalance();
				peakClosingBalance  = sessionStartBalance;
				Print(string.Format("ISE-BRIDGE START file={0} start={1} stop={2}pt limit=${3}",
					SignalFilePath, StartContracts, StopPoints, EodDrawdownLimit));
			}
		}

		protected override void OnBarUpdate()
		{
			if (State != State.Realtime && State != State.Historical) return;

			RollTradingDay();
			CheckDrawdownGuard();
			ProcessSignalFile();
		}

		/// <summary>
		/// The prop limit trails the highest CLOSING balance, so the guard tracks
		/// closing balances rather than intraday equity. Entries stop while there is
		/// less than DrawdownBuffer left, since an account that survives can be traded
		/// tomorrow and one that breaches cannot be traded at all.
		/// </summary>
		private void CheckDrawdownGuard()
		{
			double bal = AccountBalance();
			if (bal > peakClosingBalance) peakClosingBalance = bal;

			double dd = peakClosingBalance - bal;
			bool shouldHalt = dd >= (EodDrawdownLimit - DrawdownBuffer);

			if (shouldHalt && !haltedForDrawdown)
			{
				haltedForDrawdown = true;
				Print(string.Format("ISE-BRIDGE HALT drawdown=${0:F0} of ${1:F0} limit - no new entries",
					dd, EodDrawdownLimit));
			}
			else if (!shouldHalt && haltedForDrawdown)
			{
				haltedForDrawdown = false;
				Print("ISE-BRIDGE RESUME drawdown recovered");
			}
		}

		private void RollTradingDay()
		{
			var day = Time[0].Date;
			if (day == currentTradingDay) return;
			currentTradingDay = day;
			// Closing balance becomes the new high-water mark reference if it exceeds it.
			double bal = AccountBalance();
			if (bal > peakClosingBalance) peakClosingBalance = bal;
		}

		private double AccountBalance()
		{
			try { return Account.Get(AccountItem.CashValue, Currency.UsDollar); }
			catch { return sessionStartBalance; }
		}

		/// <summary>
		/// Read any lines appended since the last poll. Tracks byte position so a large
		/// file is not re-parsed on every tick, and dedupes by signal id so a re-read
		/// can never double-fire an order.
		/// </summary>
		private void ProcessSignalFile()
		{
			if (!File.Exists(SignalFilePath)) return;

			try
			{
				using (var fs = new FileStream(SignalFilePath, FileMode.Open,
							FileAccess.Read, FileShare.ReadWrite))
				{
					if (fs.Length < lastLineRead) lastLineRead = 0;   // file was rotated
					fs.Seek(lastLineRead, SeekOrigin.Begin);

					using (var sr = new StreamReader(fs))
					{
						string line;
						while ((line = sr.ReadLine()) != null)
						{
							HandleSignalLine(line);
						}
						lastLineRead = fs.Position;
					}
				}
			}
			catch (IOException)
			{
				// Writer holds the file this instant; next tick will pick it up.
			}
		}

		private void HandleSignalLine(string line)
		{
			if (string.IsNullOrWhiteSpace(line)) return;
			var f = line.Split(',');
			if (f.Length < 4) return;
			if (f[0].Trim().Equals("id", StringComparison.OrdinalIgnoreCase)) return;  // header

			string id = f[0].Trim();
			if (processedIds.Contains(id)) return;

			DateTime tsUtc;
			if (!DateTime.TryParse(f[1].Trim(), CultureInfo.InvariantCulture,
					DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out tsUtc))
				return;

			string instrument = f[2].Trim();
			string action     = f[3].Trim().ToUpperInvariant();

			processedIds.Add(id);

			if (!instrument.StartsWith(Instrument.MasterInstrument.Name,
					StringComparison.OrdinalIgnoreCase))
				return;

			// Stale-signal guard. After a restart or a connectivity gap the file may hold
			// signals from hours ago; acting on them would enter at prices that no longer
			// exist. Mark them processed and move on.
			double ageMin = (DateTime.UtcNow - tsUtc).TotalMinutes;
			if (ageMin > SignalMaxAgeMinutes)
			{
				Print(string.Format("ISE-BRIDGE SKIP stale id={0} age={1:F1}m action={2}",
					id, ageMin, action));
				return;
			}

			Print(string.Format("ISE-BRIDGE SIGNAL id={0} action={1} age={2:F1}m pos={3}",
				id, action, ageMin, Position.MarketPosition));

			switch (action)
			{
				case "BUY":  Act(MarketPosition.Long);  break;
				case "SELL": Act(MarketPosition.Short); break;
				case "FLAT": Flatten("signal");         break;
			}
		}

		/// <summary>
		/// "Exit governs entry": an opposite signal closes and reverses, a same-side
		/// signal while already positioned does nothing. Matches the backtested rule.
		/// </summary>
		private void Act(MarketPosition want)
		{
			if (Position.MarketPosition == want) return;   // same side - ignore

			if (Position.MarketPosition != MarketPosition.Flat)
				Flatten("reversal");

			if (haltedForDrawdown)
			{
				Print("ISE-BRIDGE entry suppressed - drawdown guard active");
				return;
			}

			int qty = UseLadder ? Math.Max(1, Math.Min(ladderSize, StartContracts)) : StartContracts;

			if (want == MarketPosition.Long)  EnterLong(qty, "ISE_L");
			else                              EnterShort(qty, "ISE_S");

			SetStopLoss(CalculationMode.Ticks, StopPoints / TickSize);
			Print(string.Format("ISE-BRIDGE ENTER {0} qty={1} stop={2}pt", want, qty, StopPoints));
		}

		private void Flatten(string why)
		{
			if (Position.MarketPosition == MarketPosition.Long)  ExitLong("ISE_X", "ISE_L");
			else if (Position.MarketPosition == MarketPosition.Short) ExitShort("ISE_X", "ISE_S");
			if (Position.MarketPosition != MarketPosition.Flat)
				Print("ISE-BRIDGE FLATTEN reason=" + why);
		}

		/// <summary>
		/// Ladder steps on realised outcome: down after a loss, up after a win. Scratches
		/// leave the rung alone - they are neither a reason to press nor to retreat.
		/// </summary>
		protected override void OnPositionUpdate(Position position, double averagePrice,
			int quantity, MarketPosition marketPosition)
		{
			if (marketPosition != MarketPosition.Flat) return;
			if (SystemPerformance.AllTrades.Count == 0) return;

			var last = SystemPerformance.AllTrades[SystemPerformance.AllTrades.Count - 1];
			double pnl = last.ProfitCurrency;

			if (UseLadder)
			{
				if (pnl < 0)      ladderSize = Math.Max(1, ladderSize - 1);
				else if (pnl > 0) ladderSize = Math.Min(StartContracts, ladderSize + 1);
				Print(string.Format("ISE-BRIDGE LADDER pnl=${0:F0} next size={1}", pnl, ladderSize));
			}
		}

		#region Properties

		[NinjaScriptProperty]
		[Display(Name = "Signal file path", Order = 1, GroupName = "Bridge")]
		public string SignalFilePath
		{
			get { return signalFilePath; }
			set { signalFilePath = value; }
		}

		[NinjaScriptProperty]
		[Range(1, 60)]
		[Display(Name = "Signal max age (minutes)", Order = 2, GroupName = "Bridge")]
		public int SignalMaxAgeMinutes
		{
			get { return signalMaxAgeMinutes; }
			set { signalMaxAgeMinutes = value; }
		}

		[NinjaScriptProperty]
		[Range(1, 10)]
		[Display(Name = "Start contracts (ladder ceiling)", Order = 3, GroupName = "Risk")]
		public int StartContracts
		{
			get { return startContracts; }
			set { startContracts = value; }
		}

		[NinjaScriptProperty]
		[Display(Name = "Use size ladder", Order = 4, GroupName = "Risk")]
		public bool UseLadder
		{
			get { return useLadder; }
			set { useLadder = value; }
		}

		[NinjaScriptProperty]
		[Range(1, 500)]
		[Display(Name = "Stop (points)", Order = 5, GroupName = "Risk")]
		public double StopPoints
		{
			get { return stopPoints; }
			set { stopPoints = value; }
		}

		[NinjaScriptProperty]
		[Range(100, 100000)]
		[Display(Name = "EOD drawdown limit ($)", Order = 6, GroupName = "Risk")]
		public double EodDrawdownLimit
		{
			get { return eodDrawdownLimit; }
			set { eodDrawdownLimit = value; }
		}

		[NinjaScriptProperty]
		[Range(0, 10000)]
		[Display(Name = "Halt buffer ($)", Order = 7, GroupName = "Risk")]
		public double DrawdownBuffer
		{
			get { return drawdownBuffer; }
			set { drawdownBuffer = value; }
		}

		#endregion
	}
}
