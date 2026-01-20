using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace HearthStoneDT.UI.GameEvents
{
    public sealed class PowerLogParser
    {
        // TAG_CHANGE Entity=... tag=ZONE value=...
        private static readonly Regex TagChangeZone =
            new(@"TAG_CHANGE\s+Entity=(?<entity>.+?)\s+tag=ZONE\s+value=(?<zone>\w+)", RegexOptions.Compiled);

        // TAG_CHANGE Entity=... tag=CONTROLLER value=1
        private static readonly Regex TagChangeController =
            new(@"TAG_CHANGE\s+Entity=(?<entity>.+?)\s+tag=CONTROLLER\s+value=(?<ctrl>\d+)", RegexOptions.Compiled);

        // SHOW_ENTITY - Updating Entity=[... ] CardID=CS2_004
        private static readonly Regex ShowEntityCardId =
            new(@"SHOW_ENTITY.*?Entity=(?<entity>.+?)\s+CardID=(?<cardid>[A-Z0-9_]+)", RegexOptions.Compiled);

        // FULL_ENTITY - Creating ID=... CardID=...
        private static readonly Regex FullEntityCardId =
            new(@"FULL_ENTITY.*?ID=(?<id>\d+).*?CardID=(?<cardid>[A-Z0-9_]+)", RegexOptions.Compiled);

        // Entity=... 에서 entityId 뽑기 (가장 흔한 형태: [entityName id=64 zone=...])
        private static readonly Regex EntityIdInBracket =
            new(@"\bid=(?<id>\d+)\b", RegexOptions.Compiled);

        private readonly IGameEventSink _sink;

        private readonly Dictionary<int, string> _zoneByEntity = new();
        private readonly Dictionary<int, int> _controllerByEntity = new();
        private readonly Dictionary<int, string> _cardIdByEntity = new();

        private readonly HashSet<int> _pendingExit = new();
        private readonly HashSet<int> _pendingEnter = new();

        // HDT처럼 "내 플레이어" 컨트롤러를 결정한 뒤 그 값으로 필터링할 수 있게 둔다.
        // null이면(아직 모르면) 모든 controller를 처리해서 초기 디버깅이 가능하게 한다.
        public int? MyControllerId { get; private set; }

        public PowerLogParser(IGameEventSink sink)
        {
            _sink = sink;
        }

        public void Reset()
        {
            _zoneByEntity.Clear();
            _controllerByEntity.Clear();
            _cardIdByEntity.Clear();
            _pendingExit.Clear();
            _pendingEnter.Clear();
            MyControllerId = null;
        }

        public void SetMyControllerId(int controllerId)
        {
            MyControllerId = controllerId;
        }

        public void FeedLine(string line)
        {
            // 1) controller 업데이트
            var mc = TagChangeController.Match(line);
            if (mc.Success)
            {
                if (TryGetEntityId(mc.Groups["entity"].Value, out var id))
                    _controllerByEntity[id] = int.Parse(mc.Groups["ctrl"].Value);
                return;
            }

            // 2) cardId 업데이트 (SHOW_ENTITY / FULL_ENTITY)
            var ms = ShowEntityCardId.Match(line);
            if (ms.Success)
            {
                if (TryGetEntityId(ms.Groups["entity"].Value, out var id))
                    SetCardId(id, ms.Groups["cardid"].Value);
                return;
            }

            var mf = FullEntityCardId.Match(line);
            if (mf.Success)
            {
                var id = int.Parse(mf.Groups["id"].Value);
                SetCardId(id, mf.Groups["cardid"].Value);
                return;
            }

            // 3) zone 변경 감지
            var mz = TagChangeZone.Match(line);
            if (!mz.Success)
                return;

            if (!TryGetEntityId(mz.Groups["entity"].Value, out var entityId))
                return;

            var newZone = mz.Groups["zone"].Value;

            _zoneByEntity.TryGetValue(entityId, out var oldZone);
            _zoneByEntity[entityId] = newZone;

            // MyControllerId가 정해지면 그 컨트롤러만, 아니면 전부 처리
            if (!_controllerByEntity.TryGetValue(entityId, out var ctrl))
                return;
            if (MyControllerId.HasValue && ctrl != MyControllerId.Value)
                return;

            // oldZone이 비어있으면 첫 값이라 비교 불가
            if (string.IsNullOrWhiteSpace(oldZone))
                return;

            if (oldZone == "DECK" && newZone != "DECK")
            {
                if (_cardIdByEntity.TryGetValue(entityId, out var cardId) && !string.IsNullOrWhiteSpace(cardId))
                    _sink.OnCardRemovedFromDeck(cardId);
                else
                    _pendingExit.Add(entityId);

                return;
            }

            if (oldZone != "DECK" && newZone == "DECK")
            {
                if (_cardIdByEntity.TryGetValue(entityId, out var cardId) && !string.IsNullOrWhiteSpace(cardId))
                    _sink.OnCardAddedToDeck(cardId);
                else
                    _pendingEnter.Add(entityId);

                return;
            }
        }

        private void SetCardId(int entityId, string cardId)
        {
            _cardIdByEntity[entityId] = cardId;

            // pending 처리
            if (_pendingExit.Remove(entityId))
                _sink.OnCardRemovedFromDeck(cardId);

            if (_pendingEnter.Remove(entityId))
                _sink.OnCardAddedToDeck(cardId);
        }

        private static bool TryGetEntityId(string entityToken, out int id)
        {
            id = 0;

            // 케이스1) FULL_ENTITY처럼 그냥 숫자 ID가 들어올 수도 있음
            if (int.TryParse(entityToken.Trim(), out id))
                return true;

            // 케이스2) [.. id=64 ..] 에서 id 파싱
            var m = EntityIdInBracket.Match(entityToken);
            if (!m.Success)
                return false;

            return int.TryParse(m.Groups["id"].Value, out id);
        }
    }
}
