using System;
using System.Collections.Generic;
using JinHyung.Data;
using UnityEngine;

namespace JinHyung.FrontWarsIo
{
    /// <summary>이벤트 분류 — 거르기 버튼 순서와 같다 `[소스] QPQdQ6Sy.js:H.ATTACK · H.NUKE · H.TRADE · H.ALLIANCE · H.CHAT`.</summary>
    public enum EFrontWarsEventCategory
    {
        Attack,
        Nuke,
        Trade,
        Alliance,
        Chat
    }

    /// <summary>이벤트 한 줄.</summary>
    public sealed class FrontWarsEvent
    {
        public int Id;
        public string Text;
        public EFrontWarsEventCategory Category;
        public int Tick;
        public int Duration;
        public int Priority;
        public string FocusPlayerId;
        public FrontWarsAllianceRequest AllianceRequest;

        /// <summary>동맹이 곧 끝난다는 알림이면 그 상대 이름 — 「연장」 버튼이 이 이름으로 상대를 찾는다.</summary>
        public string RenewWithName;
    }

    /// <summary>
    /// 이벤트 패널 목록 — 시뮬 알림을 받아 내 것만 모은다 `[소스] QPQdQ6Sy.js:Di=class{processGameTick(e){ … cleanupEvents(e){`.
    /// <para>기본 600틱 동안 남고, 30개를 넘으면 오래된 것부터 버린다. 동맹 요청은 요청 지속 −20 틱 동안 남는다.</para>
    /// </summary>
    public sealed class FrontWarsEventLog
    {
        private const int DefaultDuration = 600;
        private const int MaxEvents = 30;
        private const int DefaultPriority = 100000;
        private const int AllianceRequestDurationMargin = 20;

        private static readonly Color AttackColor = new Color32(251, 146, 60, 255);
        private static readonly Color NukeColor = new Color32(239, 68, 68, 255);
        private static readonly Color TradeColor = new Color32(250, 204, 21, 255);
        private static readonly Color AllianceColor = new Color32(74, 222, 128, 255);
        private static readonly Color ChatColor = new Color32(156, 163, 175, 255);

        private readonly FrontWarsGame _game;
        private readonly Func<FrontWarsPlayer> _me;
        private readonly FrontWarsTextDataContainer _texts;
        private readonly List<FrontWarsEvent> _events = new List<FrontWarsEvent>();
        private int _nextId;

        public FrontWarsEventLog(FrontWarsGame game, Func<FrontWarsPlayer> me, FrontWarsTextDataContainer texts)
        {
            _game = game;
            _me = me;
            _texts = texts;

            _game.OnMessage += HandleMessage;
            _game.OnIncomingUnit += HandleIncomingUnit;
            _game.OnEmoji += HandleEmoji;
            _game.OnChat += HandleChat;
            _game.OnTarget += HandleTarget;
            _game.Diplomacy.OnRequest += HandleAllianceRequest;
            _game.Diplomacy.OnRequestReply += HandleAllianceReply;
            _game.Diplomacy.OnBroken += HandleBroken;
            _game.Diplomacy.OnExpired += HandleExpired;
        }

        public void Dispose()
        {
            _game.OnMessage -= HandleMessage;
            _game.OnIncomingUnit -= HandleIncomingUnit;
            _game.OnEmoji -= HandleEmoji;
            _game.OnChat -= HandleChat;
            _game.OnTarget -= HandleTarget;
            _game.Diplomacy.OnRequest -= HandleAllianceRequest;
            _game.Diplomacy.OnRequestReply -= HandleAllianceReply;
            _game.Diplomacy.OnBroken -= HandleBroken;
            _game.Diplomacy.OnExpired -= HandleExpired;
        }

        public IReadOnlyList<FrontWarsEvent> Events => _events;

        public static Color ColorOf(EFrontWarsEventCategory category)
        {
            switch (category)
            {
                case EFrontWarsEventCategory.Attack: return AttackColor;
                case EFrontWarsEventCategory.Nuke: return NukeColor;
                case EFrontWarsEventCategory.Trade: return TradeColor;
                case EFrontWarsEventCategory.Alliance: return AllianceColor;
                default: return ChatColor;
            }
        }

        /// <summary>메시지 종류 → 분류 `[소스] Object.fromEntries({ATTACK:[0,1,2,3,7,8,9,10,11,12],NUKE:[4,5,6],ALLIANCE:[13,14,15,16,17,24],TRADE:[18,19,20,21,22],CHAT:[23]})`.</summary>
        public static EFrontWarsEventCategory CategoryOf(EFrontWarsMessage type)
        {
            switch (type)
            {
                case EFrontWarsMessage.MirvInbound:
                case EFrontWarsMessage.NukeInbound:
                case EFrontWarsMessage.HydrogenBombInbound:
                    return EFrontWarsEventCategory.Nuke;
                case EFrontWarsMessage.AllianceAccepted:
                case EFrontWarsMessage.AllianceRejected:
                case EFrontWarsMessage.AllianceRequest:
                case EFrontWarsMessage.AllianceBroken:
                case EFrontWarsMessage.AllianceExpired:
                case EFrontWarsMessage.RenewAlliance:
                    return EFrontWarsEventCategory.Alliance;
                case EFrontWarsMessage.SentGoldToPlayer:
                case EFrontWarsMessage.ReceivedGoldFromPlayer:
                case EFrontWarsMessage.ReceivedGoldFromTrade:
                case EFrontWarsMessage.SentTroopsToPlayer:
                case EFrontWarsMessage.ReceivedTroopsFromPlayer:
                    return EFrontWarsEventCategory.Trade;
                case EFrontWarsMessage.Chat:
                    return EFrontWarsEventCategory.Chat;
                default:
                    return EFrontWarsEventCategory.Attack;
            }
        }

        /// <summary>틱마다 — 오래된 것 정리, 30개 넘으면 앞을 버린다.</summary>
        public void Tick()
        {
            int ticks = _game.Ticks;
            _events.RemoveAll(e => ticks - e.Tick >= e.Duration || (e.AllianceRequest != null && e.AllianceRequest.Status != EFrontWarsRequestStatus.Pending));

            if (_events.Count > MaxEvents)
                _events.RemoveRange(0, _events.Count - MaxEvents);
        }

        public void Remove(FrontWarsEvent e)
        {
            _events.Remove(e);
        }

        private FrontWarsEvent Add(string text, EFrontWarsEventCategory category, string focusId = null, int duration = DefaultDuration, FrontWarsAllianceRequest request = null)
        {
            var added = new FrontWarsEvent
            {
                Id = _nextId++,
                Text = text,
                Category = category,
                Tick = _game.Ticks,
                Duration = duration,
                Priority = DefaultPriority,
                FocusPlayerId = focusId,
                AllianceRequest = request
            };

            _events.Add(added);
            return added;
        }

        private string Amount(EFrontWarsMessage type, double amount)
        {
            switch (type)
            {
                case EFrontWarsMessage.AttackCancelled:
                case EFrontWarsMessage.SentTroopsToPlayer:
                case EFrontWarsMessage.ReceivedTroopsFromPlayer:
                    return FrontWarsNumberFormat.Troops(amount);
                default:
                    return FrontWarsNumberFormat.Number(amount);
            }
        }

        private void HandleMessage(FrontWarsMessage m)
        {
            FrontWarsPlayer me = _me();

            if (me == null || (m.Recipient != null && m.Recipient != me))
                return;

            string text;

            switch (m.Type)
            {
                case EFrontWarsMessage.SentGoldToPlayer:
                case EFrontWarsMessage.ReceivedGoldFromPlayer:
                case EFrontWarsMessage.ReceivedGoldFromTrade:
                case EFrontWarsMessage.SentTroopsToPlayer:
                case EFrontWarsMessage.ReceivedTroopsFromPlayer:
                    text = _texts.Format($"msg.{m.Type}", Amount(m.Type, m.Amount), m.OtherName);
                    break;
                case EFrontWarsMessage.AttackFailed:
                case EFrontWarsMessage.AttackCancelled:
                    text = _texts.Format($"msg.{m.Type}", Amount(m.Type, m.Amount));
                    break;
                default:
                    text = _texts.Format($"msg.{m.Type}", m.OtherName ?? string.Empty, Amount(m.Type, m.Amount));
                    break;
            }

            FrontWarsEvent added = Add(text, CategoryOf(m.Type));

            if (m.Type == EFrontWarsMessage.RenewAlliance && added != null)
                added.RenewWithName = m.OtherName;
        }

        private void HandleIncomingUnit(int unitId, EFrontWarsMessage type, FrontWarsPlayer target)
        {
            if (target != _me())
                return;

            Add(_texts.Format($"msg.{type}", string.Empty), CategoryOf(type));
        }

        private void HandleEmoji(FrontWarsEmojiMessage e)
        {
            FrontWarsPlayer me = _me();
            FrontWarsPlayer sender = _game.PlayerBySmallId(e.SenderSmallId);

            if (me == null || sender == null)
                return;

            if (e.RecipientSmallId == me.SmallId || e.RecipientSmallId == FrontWarsPlayer.AllPlayersId)
            {
                if (sender != me)
                    Add($"{sender.Name}: {e.Emoji}", EFrontWarsEventCategory.Chat, sender.Id);
            }
            else if (sender == me)
            {
                FrontWarsPlayer to = _game.PlayerBySmallId(e.RecipientSmallId);
                Add($"→ {to?.Name}: {e.Emoji}", EFrontWarsEventCategory.Chat, to?.Id);
            }
        }

        private void HandleChat(FrontWarsChatEvent chat)
        {
            FrontWarsPlayer me = _me();

            if (me == null || chat.PlayerId != me.Id)
                return;

            // 「{상대}로부터: {문구}」 · 「{상대}에게: {문구}」 — 문구 안 「[P1]」 자리에는 고른 플레이어 이름이 들어간다 `[소스] chat.from|…`
            FrontWarsPlayer other = _game.HasPlayer(chat.OtherId) ? _game.Player(chat.OtherId) : null;
            string phrase = _texts.Ko($"quick_chat.{chat.Category}.{chat.Key}");

            if (phrase.Contains("{0}"))
            {
                FrontWarsPlayer target = string.IsNullOrEmpty(chat.TargetId) == false && _game.HasPlayer(chat.TargetId) ? _game.Player(chat.TargetId) : null;
                phrase = phrase.Replace("{0}", target?.Name ?? string.Empty);
            }

            Add(_texts.Format(chat.IsFrom ? "chat.from" : "chat.to", other?.Name, phrase), EFrontWarsEventCategory.Chat, chat.OtherId);
        }

        private void HandleTarget(FrontWarsPlayer from, FrontWarsPlayer to)
        {
            FrontWarsPlayer me = _me();

            if (me == null || me.IsFriendly(from) == false)
                return;

            Add($"{from.Name} → {to.Name}", EFrontWarsEventCategory.Attack, to.Id);
        }

        private void HandleAllianceRequest(FrontWarsAllianceRequest request)
        {
            if (request.Recipient != _me())
                return;

            Add(_texts.Format("msg.AllianceRequest", request.Requestor.Name), EFrontWarsEventCategory.Alliance, request.Requestor.Id,
                _game.Settings.Config.AllianceRequestDurationTicks - AllianceRequestDurationMargin, request);
        }

        private void HandleAllianceReply(FrontWarsAllianceRequest request, bool accepted)
        {
            if (request.Requestor != _me())
                return;

            string key = accepted ? "msg.AllianceAccepted" : "msg.AllianceRejected";
            Add(_texts.Format(key, request.Recipient.Name), EFrontWarsEventCategory.Alliance, request.Recipient.Id);
        }

        private void HandleBroken(FrontWarsPlayer traitor, FrontWarsPlayer betrayed)
        {
            FrontWarsPlayer me = _me();

            if (betrayed == me)
                Add(_texts.Format("msg.AllianceBroken", traitor.Name), EFrontWarsEventCategory.Attack, traitor.Id);
        }

        private void HandleExpired(FrontWarsAlliance alliance)
        {
            FrontWarsPlayer me = _me();

            if (me == null || (alliance.Requestor != me && alliance.Recipient != me))
                return;

            FrontWarsPlayer other = alliance.Other(me);
            Add(_texts.Format("msg.AllianceExpired", other.Name), EFrontWarsEventCategory.Alliance, other.Id);
        }
    }
}
