using System;

namespace HearthStoneDT.UI.Logs
{
    public sealed class LogLine
    {
        public required string Source { get; init; }   // "Power" / "LoadingScreen"
        public required string Raw { get; init; }
        public DateTime Time { get; init; }
        public bool IsReset { get; init; }            // tailer가 truncate/교체 감지했을 때
    }
}