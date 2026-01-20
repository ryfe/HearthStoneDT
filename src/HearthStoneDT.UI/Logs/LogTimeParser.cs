using System;
using System.Globalization;

namespace HearthStoneDT.UI.Logs
{
    /// <summary>
    /// Hearthstone 로그 라인 앞부분의 시간을 파싱한다.
    /// 예: "D 12:34:56.789 ..."
    /// 날짜 정보가 없어서, 기준 날짜(baseDate)에 시간을 얹어 만든다.
    /// 자정 롤오버 보정을 위해 lastTime을 넣으면 더 안정적이다.
    /// </summary>
    public static class LogTimeParser
    {
        public static bool TryParseTimestamp(string rawLine, DateTime baseDate, DateTime? lastTime, out DateTime timestamp)
        {
            timestamp = default;

            if (string.IsNullOrWhiteSpace(rawLine))
                return false;

            // 대부분 "D "로 시작
            if (rawLine.Length < 14)
                return false;

            // "D 12:34:56.789" 또는 "D 12:34:56"
            // 인덱스 2부터가 시간
            var span = rawLine.AsSpan();
            if (span[0] != 'D' && span[0] != 'W' && span[0] != 'E')
                return false;
            if (span[1] != ' ')
                return false;

            // 최대 12자리 정도 잘라서 TryParse
            // HH:mm:ss.fff (12) / HH:mm:ss (8)
            var timePart = span.Slice(2, Math.Min(12, span.Length - 2)).ToString();

            TimeSpan time;
            if (!TimeSpan.TryParseExact(timePart, "hh\\:mm\\:ss\\.fff", CultureInfo.InvariantCulture, out time)
                && !TimeSpan.TryParseExact(timePart, "hh\\:mm\\:ss", CultureInfo.InvariantCulture, out time))
            {
                return false;
            }

            var candidate = baseDate.Date + time;

            // 자정 롤오버 보정: 시간이 과거로 크게 점프하면 다음날로 간주
            if (lastTime.HasValue)
            {
                // 6시간 이상 과거로 튀면 롤오버로 판단(너무 타이트하면 오탐)
                if (candidate < lastTime.Value.AddHours(-6))
                    candidate = candidate.AddDays(1);
            }

            timestamp = candidate;
            return true;
        }
    }
}
