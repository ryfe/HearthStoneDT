using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HearthStoneDT.UI.Logs
{
    public sealed class LogWatchManager
    {
        private readonly LogFileTailer _powerTailer;
        private readonly LogFileTailer _loadingTailer;

        private DateTime? _lastPowerTime;
        private DateTime? _lastLoadingTime;

        public event Action<LogLine>? OnLine;

        public LogWatchManager(string powerPath, string loadingPath, bool startFromEnd = true)
        {
            _powerTailer = new LogFileTailer(powerPath);
            _loadingTailer = new LogFileTailer(loadingPath);

            StartingPointFinder.Apply(_powerTailer, startFromEnd);
            StartingPointFinder.Apply(_loadingTailer, startFromEnd);
        }

        public async Task RunAsync(CancellationToken ct, int pollMs = 250)
        {
            while (!ct.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var batch = new List<LogLine>(256);

                foreach (var (line, reset) in _powerTailer.ReadNewLines(detectTruncate: true))
                {
                    if (reset)
                    {
                        batch.Add(new LogLine { Source = "Power", Raw = "", Time = now, IsReset = true });
                        continue;
                    }

                    var t = LogTimeParser.ParseOrNow(line, now, ref _lastPowerTime);
                    batch.Add(new LogLine { Source = "Power", Raw = line, Time = t });
                }

                foreach (var (line, reset) in _loadingTailer.ReadNewLines(detectTruncate: true))
                {
                    if (reset)
                    {
                        batch.Add(new LogLine { Source = "LoadingScreen", Raw = "", Time = now, IsReset = true });
                        continue;
                    }

                    var t = LogTimeParser.ParseOrNow(line, now, ref _lastLoadingTime);
                    batch.Add(new LogLine { Source = "LoadingScreen", Raw = line, Time = t });
                }

                foreach (var l in batch.OrderBy(x => x.Time))
                    OnLine?.Invoke(l);

                await Task.Delay(pollMs, ct);
            }
        }
    }
}
