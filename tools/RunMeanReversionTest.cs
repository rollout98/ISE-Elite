using System;
using System.Collections.Generic;
using ISE.Systems;
using ISE.Tools;

namespace ISE.Tools
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("\n🚀 ISE-Elite Mean Reversion Backtest - AUTOMATED RUN\n");

            // Generate mock bars (5,000 bars = ~11 trading days)
            Console.WriteLine("Generating historical bar data (5,000 bars)...");
            var bars = MeanReversionBacktestRunner.GenerateMockBars(5000);
            Console.WriteLine($"✅ Generated {bars.Count} bars\n");

            // Run backtest
            var runner = new MeanReversionBacktestRunner();
            runner.Run(bars);

            Console.WriteLine("✅ Backtest complete!");
        }
    }
}
