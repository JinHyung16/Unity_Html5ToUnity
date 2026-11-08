using System;
using System.Collections.Generic;
using System.Linq;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 이모지 55개 — 명령은 이 목록의 번호를 보낸다 `[소스] Cq3gr8XO.js:const Vt=`😀.😊…`.split(`.`)`.
    /// <para>AI 반응 목록은 이모지를 번호로 바꾼 것이다. 목록에 없는 이모지(🙏)는 원본처럼 빠진다.</para>
    /// </summary>
    public static class FrontWarsEmojis
    {
        public static readonly IReadOnlyList<string> All = new[]
        {
            "😀", "😊", "🥰", "😇", "😎", "😞", "🥺", "😭", "😱", "😡", "😈", "🤡", "🖕", "🥱", "🤦‍♂️", "👋", "👏", "🤌", "💪", "🫡",
            "👍", "👎", "❓", "🐔", "🐀", "🤝", "🆘", "🕊️", "🏳️", "⏳", "🔥", "💥", "💀", "☢️", "⚠️", "↖️", "⬆️", "↗️", "👑", "🥇",
            "⬅️", "🎯", "➡️", "🥈", "🥉", "↙️", "⬇️", "↘️", "❤️", "💔", "💰", "⚓", "⛵", "🏡", "🛡️"
        };

        public const string MiddleFinger = "🖕";
        public const string Clown = "🤡";

        /// <summary>네이션이 호의로 받는 이모지 `[소스] Hu=new Set([`🕊️`,`🏳️`,`❤️`,`🥰`,`👏`])`.</summary>
        public static readonly HashSet<string> Friendly = new HashSet<string> { "🕊️", "🏳️", "❤️", "🥰", "👏" };

        // 봇 동맹 돕기 `[소스] Ku · qu · Ju · Yu`
        public static readonly IReadOnlyList<int> Assist = Indices("👍", "⛵", "🤝", "🎯");
        public static readonly IReadOnlyList<int> RelationTooLow = Indices("🥱", "🤦‍♂️");
        public static readonly IReadOnlyList<int> TargetMe = Indices("🥺", "💀");
        public static readonly IReadOnlyList<int> TargetAlly = Indices("🕊️", "👎");

        // 네이션 반응 · 행동 `[소스] bu · xu · Su · Cu · wu · Tu · Eu · Du · Ou · ku · Au · ju · Mu · Nu · Pu · Fu · Iu · Lu · Ru · zu · Bu`
        public static readonly IReadOnlyList<int> AssistAccept = Indices("👍", "🤝", "🎯");
        public static readonly IReadOnlyList<int> AssistRelationTooLow = Indices("🥱", "🤦‍♂️");
        public static readonly IReadOnlyList<int> AssistTargetMe = Indices("🥺", "💀");
        public static readonly IReadOnlyList<int> AssistTargetAlly = Indices("🕊️", "👎");
        public static readonly IReadOnlyList<int> Devil = Indices("😈");
        public static readonly IReadOnlyList<int> Angry = Indices("😡");
        public static readonly IReadOnlyList<int> Boat = Indices("⛵");
        public static readonly IReadOnlyList<int> Nuke = Indices("☢️", "💥");
        public static readonly IReadOnlyList<int> Insulted = Indices("🖕", "😡", "🤡", "😞", "😭");
        public static readonly IReadOnlyList<int> Love = Indices("❤️", "😊", "🥰");
        public static readonly IReadOnlyList<int> Confused = Indices("❓", "🤡");
        public static readonly IReadOnlyList<int> Crown = Indices("👑", "🥇", "💪");
        public static readonly IReadOnlyList<int> Handshake = Indices("🤝", "😇", "💪");
        public static readonly IReadOnlyList<int> Mock = Indices("🤡", "🤦‍♂️");
        public static readonly IReadOnlyList<int> Rat = Indices("🐀");
        public static readonly IReadOnlyList<int> Help = Indices("💀", "🆘", "😱", "🥺", "😭", "😞", "🫡", "👋");
        public static readonly IReadOnlyList<int> Clap = Indices("👏");
        public static readonly IReadOnlyList<int> Beg = Indices("🙏", "🥺");
        public static readonly IReadOnlyList<int> Yawn = Indices("🥱");
        public static readonly IReadOnlyList<int> HandshakeOnly = Indices("🤝");
        public static readonly IReadOnlyList<int> Wave = Indices("👋");

        public static int IndexOf(string emoji)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i] == emoji)
                    return i;
            }

            return -1;
        }

        private static IReadOnlyList<int> Indices(params string[] emojis)
        {
            return emojis.Select(IndexOf).Where(i => i >= 0).ToArray();
        }
    }

    /// <summary>
    /// 이모지 보내기 `[소스] Cq3gr8XO.js:Wu=class{`. 받는 쪽 id 가 null 이면 모두에게.
    /// <para>🖕 을 네이션에게 보내면 관계 −100. 네이션이 받으면 반응한다 (<see cref="NationReaction"/>).</para>
    /// </summary>
    public sealed class FrontWarsEmojiExecution : IFrontWarsExecution
    {
        private const int MiddleFingerRelationPenalty = -100;
        private const int ClownRelationPenalty = -10;
        private const int EasyFriendlyEmojiBonus = 15;

        private readonly FrontWarsPlayer _requestor;
        private readonly string _recipientId;
        private readonly int _emojiIndex;
        private FrontWarsGame _game;
        private FrontWarsRandom _random;
        private FrontWarsPlayer _recipient;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        /// <param name="recipientId">null = 모두.</param>
        public FrontWarsEmojiExecution(FrontWarsPlayer requestor, string recipientId, int emojiIndex)
        {
            _requestor = requestor;
            _recipientId = recipientId;
            _emojiIndex = emojiIndex;
        }

        /// <summary>난수 시드 = 틱 + 해시(보낸 id) — 숫자로 더한다.</summary>
        public void Init(FrontWarsGame game, int tick)
        {
            _game = game;
            _random = new FrontWarsRandom(game.Ticks + FrontWarsHash.Of(_requestor.Id));

            if (_recipientId != null && game.HasPlayer(_recipientId) == false)
            {
                IsActive = false;
                return;
            }

            _recipient = _recipientId == null ? null : game.Player(_recipientId);
        }

        public void Tick(int tick)
        {
            if (_emojiIndex >= 0 && _emojiIndex < FrontWarsEmojis.All.Count && _requestor.CanSendEmoji(_recipient))
            {
                string emoji = FrontWarsEmojis.All[_emojiIndex];
                _requestor.SendEmoji(_recipient, emoji);

                if (emoji == FrontWarsEmojis.MiddleFinger && _recipient != null && _recipient.Type == EFrontWarsPlayerType.FakeHuman)
                    _recipient.UpdateRelation(_requestor, MiddleFingerRelationPenalty);

                NationReaction(_game, _random, _requestor, _recipient, emoji);
            }

            IsActive = false;
        }

        /// <summary>
        /// 네이션이 받은 이모지에 답한다 `[소스] function Uu(e,t,n,r,i){`.
        /// <para>🖕 → 모욕 목록 · 🤡 → 관계 −10 후 당황 목록 · 호의 이모지 → (쉬움이면 +15) 내 관계가 중립 이상이면 사랑 목록, 아니면 당황 목록.</para>
        /// </summary>
        private static void NationReaction(FrontWarsGame game, FrontWarsRandom random, FrontWarsPlayer sender, FrontWarsPlayer recipient, string emoji)
        {
            if (recipient == null || recipient.Type != EFrontWarsPlayerType.FakeHuman || recipient.CanSendEmoji(sender) == false)
                return;

            void Reply(IReadOnlyList<int> list)
            {
                if (list.Count != 0)
                    game.AddExecution(new FrontWarsEmojiExecution(recipient, sender.Id, random.RandElement(list)));
            }

            if (emoji == FrontWarsEmojis.MiddleFinger)
            {
                Reply(FrontWarsEmojis.Insulted);
            }
            else if (emoji == FrontWarsEmojis.Clown)
            {
                recipient.UpdateRelation(sender, ClownRelationPenalty);
                Reply(FrontWarsEmojis.Confused);
            }
            else if (FrontWarsEmojis.Friendly.Contains(emoji))
            {
                if (game.Settings.Difficulty == EFrontWarsDifficulty.Easy)
                    recipient.UpdateRelation(sender, EasyFriendlyEmojiBonus);

                Reply(sender.Relation(recipient) >= EFrontWarsRelation.Neutral ? FrontWarsEmojis.Love : FrontWarsEmojis.Confused);
            }
        }
    }
}
