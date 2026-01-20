using System;

namespace HearthStoneDT.UI.Logs.Parsers
{
    /// <summary>
    /// LoadingScreen.log 파서 (안정성 목적: 세션 시작점)
    /// </summary>
    public sealed class LoadingScreenParser
    {
        public event Action<DateTime>? OnGameplayStart;

        public void FeedLine(DateTime time, string rawLine)
        {
            // HDT와 동일한 키워드
            if (rawLine.Contains("Gameplay.Start", StringComparison.Ordinal))
            {
                OnGameplayStart?.Invoke(time);
            }
        }
    }
}
