using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HearthStoneDT.UI.Logs;

namespace HearthStoneDT.UI.GameEvents
{
    /// <summary>
    /// HDT-style parser:
    /// - Treats TAG_CHANGE as the primary change stream.
    /// - Additionally consumes creation tags inside SHOW_ENTITY / FULL_ENTITY blocks ("tag=... value=...").
    /// - Maintains per-entity previous tag values so that we can detect DECK -> (not DECK) immediately.
    /// </summary>
    public sealed class PowerLogParser
    {
        // TAG_CHANGE Entity=... tag=... value=...
        // Power.log lines usually have a timestamp/prefix like:
        // D 19:41:31.0599017 GameState.DebugPrintPower() -     TAG_CHANGE ...
        // so we must match TAG_CHANGE anywhere in the line.
        private static readonly Regex TagChangeRegex = new(
            @".*\bTAG_CHANGE\s+Entity=(?<entity>.+?)\s+tag=(?<tag>\w+)\s+value=(?<value>.+?)\s*$",
            RegexOptions.Compiled);

        // SHOW_ENTITY - Updating Entity=[... id=4 zone=DECK ...] CardID=VAC_420
        // CHANGE_ENTITY - Updating Entity=[...] CardID=...
        private static readonly Regex ShowOrChangeEntityRegex = new(
            @".*\b(?<type>(SHOW_ENTITY|CHANGE_ENTITY))\s+-\s+Updating\s+Entity=(?<entity>.+?)\s+CardID=(?<cardid>\w*)",
            RegexOptions.Compiled);

        // FULL_ENTITY - Creating ID=191 CardID=TLC_818
        // FULL_ENTITY - Updating [entityName=... id=192 zone=SETASIDE ...] CardID=VAC_408
        private static readonly Regex FullEntityRegex = new(
            @".*\bFULL_ENTITY\s+-\s+(?<action>Creating|Updating)\s+(?<rest>.+?)\s+CardID=(?<cardid>\w*)",
            RegexOptions.Compiled);

        // Creation tag lines inside SHOW_ENTITY/FULL_ENTITY blocks:
        //     tag=ZONE value=HAND
        // Creation tag lines appear like:
        // D .. DebugPrintPower() -         tag=ZONE value=HAND
        // (Note: do NOT confuse with TAG_CHANGE lines.)
        private static readonly Regex CreationTagRegex = new(
            @".*-\s+tag=(?<tag>\w+)\s+value=(?<value>.+?)\s*$",
            RegexOptions.Compiled);

        // Extract id / zone from bracket entity token: [entityName=... id=47 zone=PLAY ...]
        private static readonly Regex EntityIdInBracket = new(@"\bid=(?<id>\d+)\b", RegexOptions.Compiled);
        private static readonly Regex ZoneInBracket = new(@"\bzone=(?<zone>\w+)\b", RegexOptions.Compiled);

        // FULL_ENTITY Creating has "ID=123" form.
        private static readonly Regex FullEntityIdRegex = new(@"\bID=(?<id>\d+)\b", RegexOptions.Compiled);

        private readonly IGameEventSink _sink;

        // Current packet entity (for creation tag lines)
        private int _currentEntityId;

        // Current known values
        private readonly Dictionary<int, string> _cardIdByEntity = new();
        private readonly Dictionary<int, int> _controllerByEntity = new();

        // Tag storage (only what we need: ZONE, but keeping generic allows extension)
        private readonly Dictionary<(int entityId, string tag), string> _tagValues = new();

        // If a DECK exit/enter happens before CardID is known, we queue by entityId.
        private readonly HashSet<int> _pendingExit = new();
        private readonly HashSet<int> _pendingEnter = new();

        // Optional: filter to player's controller.
        public int? MyControllerId { get; private set; }

        public PowerLogParser(IGameEventSink sink) => _sink = sink;

        public void Reset()
        {
            _currentEntityId = 0;
            _cardIdByEntity.Clear();
            _controllerByEntity.Clear();
            _tagValues.Clear();
            _pendingExit.Clear();
            _pendingEnter.Clear();
            MyControllerId = null;
        }

        public void SetMyControllerId(int controllerId) => MyControllerId = controllerId;

        public void FeedLine(string line)
        {
            // 1) SHOW_ENTITY / CHANGE_ENTITY header (sets current entity + cardId)
            var se = ShowOrChangeEntityRegex.Match(line);
            if(se.Success)
            {
                if(TryGetEntityId(se.Groups["entity"].Value, out var entityId))
                {
                    _currentEntityId = entityId;

                    // Seed initial ZONE from the header "zone=..." so we can detect DECK->HAND when creation tags arrive.
                    var headerZone = TryGetZoneFromEntityToken(se.Groups["entity"].Value);
                    if(!string.IsNullOrWhiteSpace(headerZone))
                        SetTag(entityId, "ZONE", headerZone, suppressActions: true);

                    var cardId = se.Groups["cardid"].Value;
                    if(!string.IsNullOrWhiteSpace(cardId))
                        SetCardId(entityId, cardId);
                }
                return;
            }

            // 2) FULL_ENTITY header (sets current entity + cardId)
            var fe = FullEntityRegex.Match(line);
            if(fe.Success)
            {
                var rest = fe.Groups["rest"].Value;
                int entityId = 0;

                // Creating ID=###
                var mid = FullEntityIdRegex.Match(rest);
                if(mid.Success)
                    entityId = int.Parse(mid.Groups["id"].Value);
                else
                    TryGetEntityId(rest, out entityId);

                if(entityId != 0)
                {
                    _currentEntityId = entityId;
                    var headerZone = TryGetZoneFromEntityToken(rest);
                    if(!string.IsNullOrWhiteSpace(headerZone))
                        SetTag(entityId, "ZONE", headerZone, suppressActions: true);

                    var cardId = fe.Groups["cardid"].Value;
                    if(!string.IsNullOrWhiteSpace(cardId))
                        SetCardId(entityId, cardId);
                }
                return;
            }

            // 3) Creation tag line inside SHOW_ENTITY / FULL_ENTITY blocks
            var ct = CreationTagRegex.Match(line);
            if(ct.Success && _currentEntityId != 0)
            {
                var tag = ct.Groups["tag"].Value;
                var value = ct.Groups["value"].Value.Trim();
                HandleTagChange(_currentEntityId, tag, value);
                return;
            }

            // 4) TAG_CHANGE stream
            var tc = TagChangeRegex.Match(line);
            if(tc.Success)
            {
                if(!TryGetEntityId(tc.Groups["entity"].Value, out var entityId))
                    return;
                var tag = tc.Groups["tag"].Value;
                var value = tc.Groups["value"].Value.Trim();
                HandleTagChange(entityId, tag, value);
            }
        }

        private void HandleTagChange(int entityId, string tag, string newValue)
        {
            // Controller tracking (needed for filtering)
            if(tag == "CONTROLLER" && int.TryParse(newValue, out var controller))
                _controllerByEntity[entityId] = controller;

            // Maintain prev values
            var key = (entityId, tag);
            _tagValues.TryGetValue(key, out var prevValue);

            // Store new
            _tagValues[key] = newValue;

            // Only actions we currently care about: ZONE changes
            if(tag != "ZONE")
                return;

            // If we don't know prev zone yet, we can't detect a transition.
            if(string.IsNullOrWhiteSpace(prevValue))
                return;

            var oldZone = prevValue;
            var newZone = newValue;

            // Deck remove/add definition (HDT style): based on prev zone.
            if(oldZone == "DECK" && newZone != "DECK")
            {
                _controllerByEntity.TryGetValue(entityId, out var controller2);
                DebugLog.Write($"[DECK_EXIT_DETECTED] entity={entityId} {oldZone}->{newZone} ctrl={(controller2 == 0 ? "?" : controller2.ToString())} hasCardId={_cardIdByEntity.ContainsKey(entityId)}");

                if(MyControllerId.HasValue && controller2 != 0 && controller2 != MyControllerId.Value)
                    return;

                if(_cardIdByEntity.TryGetValue(entityId, out var cardId) && !string.IsNullOrWhiteSpace(cardId))
                {
                    DebugLog.Write($"[EVENT] RemovedFromDeck cardId={cardId} zone={newZone}");
                    _sink.OnCardRemovedFromDeck(cardId);
                }
                else
                    _pendingExit.Add(entityId);

                return;
            }

            if(oldZone != "DECK" && newZone == "DECK")
            {
                _controllerByEntity.TryGetValue(entityId, out var controller3);
                if(MyControllerId.HasValue && controller3 != 0 && controller3 != MyControllerId.Value)
                    return;

                if(_cardIdByEntity.TryGetValue(entityId, out var cardId) && !string.IsNullOrWhiteSpace(cardId))
                {
                    DebugLog.Write($"[EVENT] AddedToDeck cardId={cardId}");
                    _sink.OnCardAddedToDeck(cardId);
                }
                else
                    _pendingEnter.Add(entityId);
            }
        }

        private void SetTag(int entityId, string tag, string value, bool suppressActions)
        {
            var key = (entityId, tag);
            if(!_tagValues.ContainsKey(key))
            {
                _tagValues[key] = value;
                return;
            }

            if(suppressActions)
            {
                _tagValues[key] = value;
                return;
            }

            HandleTagChange(entityId, tag, value);
        }

        private void SetCardId(int entityId, string cardId)
        {
            _cardIdByEntity[entityId] = cardId;

            // Resolve pending events once cardId is known.
            if(_pendingExit.Contains(entityId))
            {
                _controllerByEntity.TryGetValue(entityId, out var ctrl);
                if(!MyControllerId.HasValue || ctrl == 0 || ctrl == MyControllerId.Value)
                {
                    _pendingExit.Remove(entityId);
                    DebugLog.Write($"[EVENT] RemovedFromDeck(pending) entity={entityId} ctrl={(ctrl == 0 ? "?" : ctrl.ToString())} cardId={cardId}");
                    _sink.OnCardRemovedFromDeck(cardId);
                }
            }

            if(_pendingEnter.Contains(entityId))
            {
                _controllerByEntity.TryGetValue(entityId, out var ctrl2);
                if(!MyControllerId.HasValue || ctrl2 == 0 || ctrl2 == MyControllerId.Value)
                {
                    _pendingEnter.Remove(entityId);
                    DebugLog.Write($"[EVENT] AddedToDeck(pending) entity={entityId} ctrl={(ctrl2 == 0 ? "?" : ctrl2.ToString())} cardId={cardId}");
                    _sink.OnCardAddedToDeck(cardId);
                }
            }
        }

        private static bool TryGetEntityId(string entityToken, out int id)
        {
            id = 0;
            var raw = entityToken.Trim();

            // Case: plain numeric id
            if(int.TryParse(raw, out id))
                return true;

            // Case: bracket token with id=###
            var m = EntityIdInBracket.Match(raw);
            return m.Success && int.TryParse(m.Groups["id"].Value, out id);
        }

        private static string TryGetZoneFromEntityToken(string entityToken)
        {
            var m = ZoneInBracket.Match(entityToken);
            return m.Success ? m.Groups["zone"].Value : "";
        }
    }
}
