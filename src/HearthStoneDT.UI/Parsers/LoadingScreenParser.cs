using System;

namespace HearthStoneDT.UI.Logs.Parsers
{
    public sealed class LoadingScreenParser
    {
        public event Action? SessionStart;

        public void Feed(string line)
        {
            if (line.Contains("Gameplay.Start", StringComparison.OrdinalIgnoreCase))
                SessionStart?.Invoke();
        }
    }
}
