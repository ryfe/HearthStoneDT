using System;
using System.Threading;
using System.Threading.Tasks;
using HearthStoneDT.UI.Decks;
using HearthStoneDT.UI.Logs;
using HearthStoneDT.UI.Logs.Parsers;
using HearthStoneDT.UI.Views;
using HearthStoneDT.UI.Parser;

namespace HearthStoneDT.UI.Services
{
    public sealed class GameLogService
    {
        private readonly LogWatchManager _watcher;
        private readonly LoadingScreenParser _loading;
        private readonly PowerLogParser _power;

        private readonly DeckWindow _deckWindow;
        private readonly CardDb _cardDb;
        private readonly Func<DeckDefinition?> _getSelectedDeck;

        private CancellationTokenSource? _cts;

        public GameLogService(
            LogWatchManager watcher,
            LoadingScreenParser loading,
            PowerLogParser power,
            DeckWindow deckWindow,
            CardDb cardDb,
            Func<DeckDefinition?> getSelectedDeck)
        {
            _watcher = watcher;
            _loading = loading;
            _power = power;

            _deckWindow = deckWindow;
            _cardDb = cardDb;
            _getSelectedDeck = getSelectedDeck;

            _watcher.OnLine += OnLine;
            _loading.SessionStart += async () => await OnSessionStartAsync();
        }

        public void Start()
        {
            if (_cts != null) return;

            _cts = new CancellationTokenSource();
            _ = _watcher.RunAsync(_cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts = null;
        }

        private void OnLine(LogLine line)
        {
            // reset(truncate/교체) 처리 정책:
            // - 지금은 무시. (원하면 SessionStart처럼 Reset 호출해도 됨)
            if (line.IsReset) return;

            if (line.Source == "LoadingScreen")
            {
                _loading.Feed(line.Raw);
                return;
            }

            if (line.Source == "Power")
            {
                _power.FeedLine(line.Raw);
                return;
            }
        }

        private async Task OnSessionStartAsync()
        {
            // 1) 파서 리셋
            _power.Reset();

            // 2) 덱 UI 상태를 선택 덱으로 다시 초기화
            var deck = _getSelectedDeck();
            if (deck == null) return;

            await _cardDb.EnsureLoadedAsync();
            await _deckWindow.SetDeckAsync(deck, _cardDb);
        }
    }
}
