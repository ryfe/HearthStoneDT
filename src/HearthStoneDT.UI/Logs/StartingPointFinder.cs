using System;
using System.IO;
using System.Text;

namespace HearthStoneDT.UI.Logs
{
    /// <summary>
    /// HDT 방식: 두 로그에서 "마지막 시작점"을 찾아 startingPoint를 결정한다.
    /// - LoadingScreen: "Gameplay.Start" (세션 시작)
    /// - Power: "tag=STATE value=COMPLETE" 또는 "End Spectator" (게임 초기화 완료/관전 종료)
    /// </summary>
    public static class StartingPointFinder
    {
        // 너무 큰 로그는 뒤쪽만 스캔(충분히 안정적)
        private const int MaxScanBytes = 5 * 1024 * 1024; // 5MB

        public static DateTime FindStartingPoint(string powerLogPath, string loadingScreenLogPath)
        {
            var baseDate = DateTime.Today;

            var loading = FindLastMatchTime(
                loadingScreenLogPath,
                baseDate,
                lastTime: null,
                contains: "Gameplay.Start");

            var power = FindLastMatchTime(
                powerLogPath,
                baseDate,
                lastTime: null,
                contains: "tag=STATE value=COMPLETE");

            // 대체 패턴
            if (power == null)
                power = FindLastMatchTime(powerLogPath, baseDate, null, contains: "End Spectator");

            if (loading == null && power == null)
                return DateTime.MinValue; // 시작점 못 찾으면 전부 읽게(하지만 아래 필터에서 최소화 가능)

            if (loading == null) return power!.Value;
            if (power == null) return loading!.Value;
            return loading.Value > power.Value ? loading.Value : power.Value;
        }

        private static DateTime? FindLastMatchTime(string path, DateTime baseDate, DateTime? lastTime, string contains)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

                long startPos = Math.Max(0, fs.Length - MaxScanBytes);
                fs.Seek(startPos, SeekOrigin.Begin);

                using var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 64 * 1024);

                DateTime? found = null;
                DateTime? prevTs = lastTime;
                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (!line.Contains(contains, StringComparison.Ordinal))
                        continue;

                    if (!LogTimeParser.TryParseTimestamp(line, baseDate, prevTs, out var ts))
                        continue;

                    prevTs = ts;
                    found = ts;
                }

                return found;
            }
            catch
            {
                return null;
            }
        }
    }
}
