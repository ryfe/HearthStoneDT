using System;
using System.IO;
using System.Threading.Tasks;
using HearthStoneDT.UI.Decks;
using HearthStoneDT.UI.Overlay;
using HearthStoneDT.UI.GameEvents;
using HearthStoneDT.UI.Logs;
using HearthStoneDT.UI.Logs.Parsers;

namespace HearthStoneDT.UI.Services
{
    /// <summary>
    /// 앱에서 1번만 띄우는 로그 서비스.
    /// - Power + LoadingScreen 동시 Tail
    /// - StartingPoint 이후 라인만 처리
    /// - LoadingScreen Gameplay.Start에서 세션 리셋
    /// </summary>
    public sealed class GameLogService : IGameEventSink, IDisposable
    {
        private LogWatchManager? _watch;
        private LoadingScreenParser? _loadingParser;
        private PowerLogParser? _powerParser;
        private string? _logDirectory;
        // 하이브리드: 세션 시작 시(LoadingScreen Gameplay.Start) 선택된 덱을 오버레이에 다시 세팅
        private OverlayController? _autoResetOverlays;
        private Func<DeckDefinition?>? _autoResetGetDeck;

        public event Action<string>? CardRemovedFromDeck;
        public event Action<string>? CardAddedToDeck;
        public event Action<DateTime>? SessionStarted;

        public bool IsRunning => _watch != null;

        public void BindAutoDeckReset(OverlayController overlays, Func<DeckDefinition?> getSelectedDeck)
        {
            _autoResetOverlays = overlays;
            _autoResetGetDeck = getSelectedDeck;
        }

        public void Start(TimeSpan? pollInterval = null)
        {
            if (_watch != null)
                return;

            var logDir = _logDirectory ?? HearthstoneLogPathResolver.ResolveLogDirectory();
            if (logDir == null)
                throw new DirectoryNotFoundException("Hearthstone Logs 폴더를 찾지 못했습니다.");

            var power = Path.Combine(logDir, "Power.log");
            var loading = Path.Combine(logDir, "LoadingScreen.log");

            _loadingParser = new LoadingScreenParser();
            _powerParser = new PowerLogParser(this);

            _loadingParser.OnGameplayStart += t =>
            {
                _powerParser.Reset();
                SessionStarted?.Invoke(t);

                // 선택된 덱이 있으면 오버레이를 해당 덱으로 재설정(사용자 버전 기능)
                try
                {
                    if (_autoResetOverlays != null && _autoResetGetDeck != null)
                    {
                        var deck = _autoResetGetDeck();
                        if (deck != null)
                        {
                            // fire-and-forget: UI 스레드에서 실행되도록 Overlays 내부에서 Dispatcher 사용 가능
                            _ = _autoResetOverlays.ShowDeckAsync(deck);
                        }
                    }
                }
                catch
                {
                    // 세션 리셋은 로그 처리의 핵심이라 예외는 삼킨다
                }
            };

            _watch = new LogWatchManager(power, loading);
            _watch.OnNewLines += OnNewLines;
            _watch.Start(pollInterval ?? TimeSpan.FromMilliseconds(200));
        }

        public void SetLogDirectory(string logDirectory)
        {
            _logDirectory = logDirectory;
        }
        private void OnNewLines(System.Collections.Generic.IReadOnlyList<LogLine> lines)
        {
            if (_loadingParser == null || _powerParser == null)
                return;

            foreach (var l in lines)
            {
                if (l.Source == "LoadingScreen")
                {
                    _loadingParser.FeedLine(l.Time, l.RawLine);
                }
                else if (l.Source == "Power")
                {
                    _powerParser.FeedLine(l.RawLine);
                }
            }
        }

        public async Task StopAsync()
        {
            if (_watch == null)
                return;

            _watch.OnNewLines -= OnNewLines;
            await _watch.StopAsync();
            _watch = null;
            _loadingParser = null;
            _powerParser = null;
        }

        public void Dispose()
        {
            _ = StopAsync();
        }

        // IGameEventSink
        public void OnCardRemovedFromDeck(string cardId) => CardRemovedFromDeck?.Invoke(cardId);
        public void OnCardAddedToDeck(string cardId) => CardAddedToDeck?.Invoke(cardId);

        /// <summary>
        /// Windows 기본 설치 경로 기준으로 Logs 폴더를 찾는다.
        /// </summary>
        public static string? TryFindHearthstoneLogDirectory()
        {
            // 1) 기본(대부분 여기)
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var p1 = Path.Combine(local, "Blizzard", "Hearthstone", "Logs");
            if (Directory.Exists(p1)) return p1;

            // 2) 예전/변형 케이스
            var p2 = Path.Combine(local, "Hearthstone", "Logs");
            if (Directory.Exists(p2)) return p2;

            return null;
        }
    }
}
