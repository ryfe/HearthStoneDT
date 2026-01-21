using System;
using System.IO;
using System.Threading;
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
        private string? _currentLogDirectory;
        private string? _logRoot;
        private FileSystemWatcher? _rootWatcher;
        private Timer? _rootPollTimer;
        private readonly object _switchLock = new();
        // 하이브리드: 세션 시작 시(LoadingScreen Gameplay.Start) 선택된 덱을 오버레이에 다시 세팅
        private OverlayController? _autoResetOverlays;
        private Func<DeckDefinition?>? _autoResetGetDeck;

        public event Action<string, int>? CardRemovedFromDeck;
        public event Action<string, int>? CardAddedToDeck;
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

            // 1) 로그 디렉터리 결정
            var resolved = _logDirectory != null
                ? (Root: Path.GetDirectoryName(_logDirectory), LogDir: _logDirectory)
                : HearthstoneLogPathResolver.ResolveRootAndDirectory();

            var logDir = resolved.LogDir;
            _logRoot = resolved.Root;

            if (logDir == null)
                throw new DirectoryNotFoundException("Hearthstone Logs 폴더를 찾지 못했습니다.");

            _currentLogDirectory = logDir;

            var power = Path.Combine(logDir, "Power.log");
            var loading = Path.Combine(logDir, "LoadingScreen.log");

            DebugLog.Write($"[START] logDir={logDir}");
            DebugLog.Write($"[START] power={power}");
            DebugLog.Write($"[START] loading={loading}");

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


            StartWatchersForDirectory(logDir, pollInterval ?? TimeSpan.FromMilliseconds(200));

            // 2) HS가 실행/재실행 될 때 Logs\Hearthstone_yyyy... 폴더가 새로 생기는 구조를 지원
            // - 새 폴더 생성 이벤트를 감시해서 최신 폴더로 자동 전환
            // - 이벤트를 못 받는 환경 대비, 주기적으로도 확인
            TryStartRootWatcher();
        }

        public void SetLogDirectory(string logDirectory)
        {
            _logDirectory = logDirectory;
        }

        private void StartWatchersForDirectory(string logDir, TimeSpan pollInterval)
        {
            var power = Path.Combine(logDir, "Power.log");
            var loading = Path.Combine(logDir, "LoadingScreen.log");

            DebugLog.Write($"[WATCH] switchTo={logDir}");
            DebugLog.Write($"[WATCH] power={power}");
            DebugLog.Write($"[WATCH] loading={loading}");

            _watch = new LogWatchManager(power, loading);
            _watch.OnNewLines += OnNewLines;
            _watch.Start(pollInterval);
        }

        private void TryStartRootWatcher()
        {
            if (_logRoot == null)
                return;
            if (!Directory.Exists(_logRoot))
                return;
            if (_rootWatcher != null)
                return;

            try
            {
                _rootWatcher = new FileSystemWatcher(_logRoot)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.CreationTime,
                    Filter = "Hearthstone_*",
                    EnableRaisingEvents = true
                };
                _rootWatcher.Created += (_, __) => TrySwitchToLatestLogDirectory();
                _rootWatcher.Renamed += (_, __) => TrySwitchToLatestLogDirectory();

                // 폴링 백업(2초)
                _rootPollTimer = new Timer(_ => TrySwitchToLatestLogDirectory(), null, 2000, 2000);

                DebugLog.Write($"[ROOT_WATCH] root={_logRoot}");
            }
            catch (Exception ex)
            {
                DebugLog.Write($"[ROOT_WATCH_ERR] {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void TrySwitchToLatestLogDirectory()
        {
            // 이미 Start가 끝나고 Watcher가 살아있는 상태에서만 전환
            if (_watch == null || _logRoot == null)
                return;

            string? latest = null;
            try
            {
                latest = HearthstoneLogPathResolver.TryGetLatestChildWithLogs(_logRoot);
            }
            catch
            {
                return;
            }

            if (latest == null)
                return;

            lock (_switchLock)
            {
                if (_currentLogDirectory != null && string.Equals(_currentLogDirectory, latest, StringComparison.OrdinalIgnoreCase))
                    return;

                // 아직 파일이 생성 중일 수 있어서 잠깐 기다리며 확인
                for (int i = 0; i < 10; i++)
                {
                    if (HearthstoneLogPathResolver.HasLogs(latest))
                        break;
                    Thread.Sleep(100);
                }
                if (!HearthstoneLogPathResolver.HasLogs(latest))
                    return;

                DebugLog.Write($"[SWITCH] {_currentLogDirectory} -> {latest}");

                // 기존 watcher 중지
                try
                {
                    _watch.OnNewLines -= OnNewLines;
                    _watch.StopAsync().GetAwaiter().GetResult();
                }
                catch
                {
                    // best-effort
                }
                _watch = null;

                _currentLogDirectory = latest;
                StartWatchersForDirectory(latest, TimeSpan.FromMilliseconds(200));
            }
        }
        private void OnNewLines(System.Collections.Generic.IReadOnlyList<LogLine> lines)
        {
            if (_loadingParser == null || _powerParser == null)
                return;

            if (lines.Count > 0)
            {
                DebugLog.Write($"[LINES] count={lines.Count} first={lines[0].Source}@{lines[0].Time:HH:mm:ss.fff} last={lines[^1].Source}@{lines[^1].Time:HH:mm:ss.fff}");
            }

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

            try
            {
                _rootWatcher?.Dispose();
                _rootWatcher = null;
                _rootPollTimer?.Dispose();
                _rootPollTimer = null;
            }
            catch { }
        }

        public void Dispose()
        {
            _ = StopAsync();
        }

        // IGameEventSink
        public void OnCardRemovedFromDeck(string cardId, int entityId)
        {
            DebugLog.Write($"[EVENT] RemovedFromDeck cardId={cardId} entity={entityId}");
            CardRemovedFromDeck?.Invoke(cardId, entityId);
        }

        public void OnCardAddedToDeck(string cardId, int entityId)
        {
            DebugLog.Write($"[EVENT] AddedToDeck cardId={cardId} entity={entityId}");
            CardAddedToDeck?.Invoke(cardId, entityId);
        }

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
