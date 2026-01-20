using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HearthStoneDT.UI.Logs
{
    /// <summary>
    /// Power + LoadingScreen 로그를 동시에 tail 하고, 시간순으로 merge해서 내보낸다.
    /// 안정성 핵심: StartingPoint 이후 라인만 방출.
    /// </summary>
    public sealed class LogWatchManager : IDisposable
    {
        private readonly LogFileTailer _powerTailer;
        private readonly LogFileTailer _loadingTailer;

        private CancellationTokenSource? _cts;
        private Task? _loopTask;

        private readonly object _lock = new();

        private DateTime _startingPoint;

        // 날짜 롤오버 보정용(소스별)
        private DateTime? _lastPowerTs;
        private DateTime? _lastLoadingTs;

        public event Action<IReadOnlyList<LogLine>>? OnNewLines;

        public LogWatchManager(string powerLogPath, string loadingScreenLogPath)
        {
            _powerTailer = new LogFileTailer(powerLogPath);
            _loadingTailer = new LogFileTailer(loadingScreenLogPath);
        }

        public void Start(TimeSpan pollInterval)
        {
            lock (_lock)
            {
                if (_cts != null)
                    return;

                _startingPoint = StartingPointFinder.FindStartingPoint(_powerTailer.FilePath, _loadingTailer.FilePath);

                _cts = new CancellationTokenSource();
                _loopTask = Task.Run(() => LoopAsync(pollInterval, _cts.Token));
            }
        }

        public async Task StopAsync()
        {
            CancellationTokenSource? cts;
            Task? task;
            lock (_lock)
            {
                cts = _cts;
                task = _loopTask;
                _cts = null;
                _loopTask = null;
            }

            if (cts == null)
                return;

            try
            {
                cts.Cancel();
            }
            finally
            {
                cts.Dispose();
            }

            if (task != null)
            {
                try { await task; } catch { /* ignore */ }
            }
        }

        private async Task LoopAsync(TimeSpan pollInterval, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    PollOnce();
                }
                catch
                {
                    // 상위에서 예외로그 처리
                }

                try
                {
                    await Task.Delay(pollInterval, ct);
                }
                catch
                {
                    break;
                }
            }
        }

        private void PollOnce()
        {
            var baseDate = DateTime.Today;

            var powerRaw = _powerTailer.ReadNewLines();
            var loadingRaw = _loadingTailer.ReadNewLines();

            if (powerRaw.Count == 0 && loadingRaw.Count == 0)
                return;

            var merged = new List<LogLine>(powerRaw.Count + loadingRaw.Count);

            foreach (var raw in powerRaw)
            {
                if (LogTimeParser.TryParseTimestamp(raw, baseDate, _lastPowerTs, out var ts))
                    _lastPowerTs = ts;
                else
                    continue;

                if (ts < _startingPoint)
                    continue;

                merged.Add(new LogLine("Power", ts, raw));
            }

            foreach (var raw in loadingRaw)
            {
                if (LogTimeParser.TryParseTimestamp(raw, baseDate, _lastLoadingTs, out var ts))
                    _lastLoadingTs = ts;
                else
                    continue;

                if (ts < _startingPoint)
                    continue;

                merged.Add(new LogLine("LoadingScreen", ts, raw));
            }

            if (merged.Count == 0)
                return;

            merged.Sort((a, b) => a.Time.CompareTo(b.Time));
            OnNewLines?.Invoke(merged);
        }

        public void Dispose()
        {
            _ = StopAsync();
        }
    }
}
