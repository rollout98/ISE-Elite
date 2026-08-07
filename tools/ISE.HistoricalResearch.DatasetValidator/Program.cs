using System;
using ISE.HistoricalResearch;

namespace ISE.HistoricalResearch.DatasetValidator
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.DatasetValidator -- <path-to-tsv>");
                return 2;
            }

            try
            {
                var store = new HistoricalDataFileStore();
                var bars = store.ReadContractAware(args[0]);
                var validator = new ContractAwareHistoricalDatasetValidator();
                var report = validator.BuildCoverageReport(bars, TimeSpan.FromHours(6), TimeSpan.FromHours(11));

                Console.WriteLine("ISE-DATASET-VALIDATION RESULT"
                    + " instrument=" + report.Instrument
                    + " intervalSeconds=" + report.IntervalSeconds
                    + " bars=" + report.BarCount
                    + " sessions=" + report.SessionCount
                    + " completeSessions=" + report.CompleteSessionCount
                    + " partialSessions=" + report.PartialSessionCount
                    + " firstSession=" + (report.FirstSessionDateCentral.HasValue ? report.FirstSessionDateCentral.Value.ToString("yyyy-MM-dd") : "none")
                    + " lastSession=" + (report.LastSessionDateCentral.HasValue ? report.LastSessionDateCentral.Value.ToString("yyyy-MM-dd") : "none"));

                for (var i = 0; i < report.ContractSegments.Count; i++)
                {
                    var segment = report.ContractSegments[i];
                    Console.WriteLine("ISE-DATASET-VALIDATION SEGMENT"
                        + " index=" + i
                        + " contract=" + segment.Contract
                        + " bars=" + segment.BarCount
                        + " firstUtc=" + segment.FirstTimestampUtc.ToString("O")
                        + " lastUtc=" + segment.LastTimestampUtc.ToString("O"));
                }

                foreach (var date in report.PartialSessionDatesCentral)
                    Console.WriteLine("ISE-DATASET-VALIDATION PARTIAL date=" + date.ToString("yyyy-MM-dd"));

                Console.WriteLine("ISE-DATASET-VALIDATION COMPLETE");
                return report.PartialSessionCount == 0 ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ISE-DATASET-VALIDATION ERROR " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }
    }
}
