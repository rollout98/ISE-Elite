#region Using declarations
using System;
using System.IO;
using System.Text;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
	/// <summary>
	/// ISE ELITE - VECTORFLOW SIGNAL DUMP (VERIFICATION TOOL)
	///
	/// Purpose: prove that the NinjaTrader port of VectorFlow fires the SAME signals,
	/// on the SAME bars, as the TradingView indicator. Until that is demonstrated we
	/// cannot trust anything the NT strategy does, because the backtest that justified
	/// it was driven by TradingView's exported signals, not by this code.
	///
	/// Why this matters here specifically: every backtest in this project before
	/// 2026-08-12 read the CSV columns literally named "Buy Signal"/"Sell Signal",
	/// which belong to a DIFFERENT indicator. VectorFlow's real BUY/SELL come from two
	/// untitled plotshape() calls that TradingView exports as two columns both named
	/// "Shapes". Any earlier claim that this port was "100% verified" was measured
	/// against the wrong reference and means nothing.
	///
	/// Usage:
	///   1. Attach to an MGC or MNQ chart at the SAME timeframe as the TradingView
	///      export (5-minute).
	///   2. It writes every bar plus a BUY/SELL/NONE column to:
	///        Documents\NinjaTrader 8\ISEEliteResearch\ntdump-{INSTRUMENT}-{tf}.csv
	///   3. Diff that file against the TradingView export's "Shapes" columns.
	///
	/// A match means fire-for-fire on the same timestamps. Anything less - a one-bar
	/// lag, a different count, extra fires - means the port diverges and the strategy
	/// would trade something other than what we measured.
	/// </summary>
	public class ISEEliteVectorFlowSignalDump : Indicator
	{
		// ---- VectorFlow parameters, read from the Pine source ----
		// FTC trend channel: SMA(100) +/- ATR(100)
		private const int FtcPeriod = 100;
		private const int AtrPeriod = 100;

		// VIDYA: CMO-adaptive EMA, smoothed by SMA(15), bands at 2 * ATR(200)
		private const int VidyaPeriod    = 20;
		private const int VidyaMomentum  = 20;
		private const int VidyaSmooth    = 15;
		private const int AtrBandPeriod  = 200;
		private const double BandMult    = 2.0;

		// ---- Latched state. Both latches hold until the opposite crossover. ----
		private bool ftcTrendLatch = false;
		private bool vidyaUpLatch  = false;
		private bool prevAligned   = false;   // for edge detection
		private bool prevAlignedDn = false;

		// ---- VIDYA rolling state ----
		private double vidyaValue = 0;
		private double[] vidyaSmoothBuf = new double[VidyaSmooth];
		private int vidyaSmoothCount = 0;

		private StreamWriter writer;
		private string outPath;
		private int buyCount, sellCount;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = "Dumps VectorFlow BUY/SELL signals for verification against TradingView";
				Name        = "ISEEliteVectorFlowSignalDump";
				Calculate   = Calculate.OnBarClose;   // must match TradingView bar-close evaluation
				IsOverlay   = false;
				DisplayInDataBox = false;
				BarsRequiredToPlot = AtrBandPeriod + 1;
			}
			else if (State == State.Configure)
			{
				vidyaValue = 0;
				vidyaSmoothCount = 0;
				buyCount = sellCount = 0;
			}
			else if (State == State.DataLoaded)
			{
				var dir = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
					"NinjaTrader 8", "ISEEliteResearch");
				Directory.CreateDirectory(dir);

				var tf = string.Format("{0}{1}", BarsPeriod.Value, BarsPeriod.BarsPeriodType);
				outPath = Path.Combine(dir, string.Format("ntdump-{0}-{1}.csv",
					Instrument.MasterInstrument.Name, tf));

				writer = new StreamWriter(outPath, false, Encoding.UTF8);
				writer.WriteLine("time,close,ftc_trend,vidya_up,signal");
				Print("ISE-DUMP START -> " + outPath);
			}
			else if (State == State.Terminated)
			{
				if (writer != null)
				{
					writer.Flush();
					writer.Close();
					writer = null;
					Print(string.Format("ISE-DUMP COMPLETE buys={0} sells={1} file={2}",
						buyCount, sellCount, outPath));
				}
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < AtrBandPeriod) return;

			// ---- FTC trend latch: close crossing SMA(100) +/- ATR(100) ----
			double sma = SMA(Close, FtcPeriod)[0];
			double atr = ATR(Close, AtrPeriod)[0];
			if (Close[0] > sma + atr)      ftcTrendLatch = true;
			else if (Close[0] < sma - atr) ftcTrendLatch = false;

			// ---- VIDYA: EMA whose alpha scales with the Chande Momentum Oscillator ----
			double cmo = Math.Abs(CalcCmo(VidyaMomentum));
			double alpha = 2.0 / (VidyaPeriod + 1.0);
			if (vidyaValue == 0) vidyaValue = Close[0];
			vidyaValue = Close[0] * alpha * cmo + vidyaValue * (1 - alpha * cmo);

			// Smooth with SMA(15) over the VIDYA series itself
			vidyaSmoothBuf[vidyaSmoothCount % VidyaSmooth] = vidyaValue;
			vidyaSmoothCount++;
			int n = Math.Min(vidyaSmoothCount, VidyaSmooth);
			double sum = 0;
			for (int i = 0; i < n; i++) sum += vidyaSmoothBuf[i];
			double vidyaSmoothed = sum / n;

			double bandAtr = ATR(Close, AtrBandPeriod)[0];
			if (Close[0] > vidyaSmoothed + BandMult * bandAtr)      vidyaUpLatch = true;
			else if (Close[0] < vidyaSmoothed - BandMult * bandAtr) vidyaUpLatch = false;

			// ---- Signal on the RISING EDGE of latch alignment ----
			// BUY when both latches turn true together; SELL when both turn false.
			// Edge-triggered, so it fires once per alignment change - not every bar
			// the alignment happens to hold.
			bool alignedUp = ftcTrendLatch && vidyaUpLatch;
			bool alignedDn = !ftcTrendLatch && !vidyaUpLatch;

			string signal = "NONE";
			if (alignedUp && !prevAligned)        { signal = "BUY";  buyCount++; }
			else if (alignedDn && !prevAlignedDn) { signal = "SELL"; sellCount++; }

			prevAligned   = alignedUp;
			prevAlignedDn = alignedDn;

			if (writer != null)
			{
				writer.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss},{1},{2},{3},{4}",
					Time[0], Close[0], ftcTrendLatch ? 1 : 0, vidyaUpLatch ? 1 : 0, signal));
			}
		}

		/// <summary>
		/// Chande Momentum Oscillator over the given lookback, normalised to 0..1.
		/// VIDYA uses |CMO| to scale the EMA alpha: strong momentum makes the average
		/// track price closely, weak momentum makes it sluggish.
		/// </summary>
		private double CalcCmo(int period)
		{
			double up = 0, down = 0;
			for (int i = 0; i < period && i < CurrentBar; i++)
			{
				double diff = Close[i] - Close[i + 1];
				if (diff > 0) up += diff; else down += -diff;
			}
			double total = up + down;
			return total == 0 ? 0 : (up - down) / total;
		}
	}
}
