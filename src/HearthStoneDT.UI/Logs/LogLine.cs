using System;

namespace HearthStoneDT.UI.Logs
{
    /// <summary>
    /// 로그 한 줄 (Power/LoadingScreen merge용)
    /// </summary>
    public sealed class LogLine
    {
        public string Source { get; }
        public DateTime Time { get; }
        public string RawLine { get; }

        public LogLine(string source, DateTime time, string rawLine)
        {
            Source = source;
            Time = time;
            RawLine = rawLine;
        }
    }
}
