namespace JinHyung.FrontWarsIo
{
    /// <summary>빠른 채팅 한 줄 — 분류 · 문구 키 · 대상 · 받는 사람 · 보낸 쪽인가 · 상대 id.</summary>
    public sealed class FrontWarsChatEvent
    {
        public string Category { get; }
        public string Key { get; }
        public string TargetId { get; }
        public string PlayerId { get; }
        public bool IsFrom { get; }
        public string OtherId { get; }

        public FrontWarsChatEvent(string category, string key, string targetId, string playerId, bool isFrom, string otherId)
        {
            Category = category;
            Key = key;
            TargetId = targetId;
            PlayerId = playerId;
            IsFrom = isFrom;
            OtherId = otherId;
        }
    }

    /// <summary>
    /// 빠른 채팅 보내기 — 키 «분류.문구» 를 받는 쪽 · 보낸 쪽 둘 다에게 알린다 `[소스] Cq3gr8XO.js:yu=class{`.
    /// </summary>
    public sealed class FrontWarsQuickChatExecution : IFrontWarsExecution
    {
        private readonly FrontWarsPlayer _sender;
        private readonly string _recipientId;
        private readonly string _key;
        private readonly string _targetId;
        private FrontWarsGame _game;
        private FrontWarsPlayer _recipient;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsQuickChatExecution(FrontWarsPlayer sender, string recipientId, string quickChatKey, string targetId)
        {
            _sender = sender;
            _recipientId = recipientId;
            _key = quickChatKey;
            _targetId = targetId;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _game = game;

            if (game.HasPlayer(_recipientId) == false)
            {
                IsActive = false;
                return;
            }

            _recipient = game.Player(_recipientId);
        }

        public void Tick(int tick)
        {
            string[] parts = _key.Split('.');
            string category = parts.Length > 0 ? parts[0] : string.Empty;
            string phrase = parts.Length > 1 ? parts[1] : null;

            _game.RaiseChat(new FrontWarsChatEvent(category, phrase, _targetId, _recipient.Id, true, _sender.Id));
            _game.RaiseChat(new FrontWarsChatEvent(category, phrase, _targetId, _sender.Id, false, _recipient.Id));
            IsActive = false;
        }
    }
}
