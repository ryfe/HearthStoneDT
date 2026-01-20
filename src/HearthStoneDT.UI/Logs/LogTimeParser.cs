using System;
using System.Text.RegularExpressions;

namespace HearthStoneDT.UI.Logs
{
    public static class LogTimeParser
    {
        private static readonly Regex Rx =
            new(@"^\s*D\s+(?<hh>\d{2}):(?<mm>\d{2}):(?<ss>\d{2})\.(?<ms>\d{3})\s+",
                RegexOptions.Compiled);

        public static DateTime ParseOrNow(string line, DateTime now, ref DateTime? last)
        {
            var m = Rx.Match(line);
            if (!m.Success)
                return now;

            int hh = int.Parse(m.Groups["hh"].Value);
            int mm = int.Parse(m.Groups["mm"].Value);
            int ss = int.Parse(m.Groups["ss"].Value);
            int ms = int.Parse(m.Groups["ms"].Value);

            var dt = new DateTime(now.Year, now.Month, now.Day, hh, mm, ss, ms, now.Kind);

            // 자정 rollover
            if (last.HasValue && dt < last.Value.AddHours(-6))
                dt = dt.AddDays(1);

            last = dt;
            return dt;
        }
    }
}
