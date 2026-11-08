using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JinHyung.FrontWarsIo
{
    /// <summary>플레이어 종류 `[소스] Cq3gr8XO.js:E.Bot · E.Human · E.FakeHuman`. FakeHuman = 네이션.</summary>
    public enum EFrontWarsPlayerType
    {
        Bot,
        Human,
        FakeHuman
    }

    /// <summary>관계 이름 `[소스] relationFromValue(e){` — 값 &lt; −50 적대 · &lt; 0 불신 · &lt; 50 중립 · 그 이상 우호. 크기 비교가 원본 숫자 순서와 같다.</summary>
    public enum EFrontWarsRelation
    {
        Hostile,
        Distrustful,
        Neutral,
        Friendly
    }

    /// <summary>
    /// 플레이어가 누구인가 — 이름 · 종류 · id `[소스] oe=class{`.
    /// <para>id 는 게임 시드 난수의 8자리 36진수다. 해시와 플레이어 난수 시드가 이 id 에서 나온다.</para>
    /// </summary>
    public sealed class FrontWarsPlayerInfo
    {
        public string Name { get; }
        public EFrontWarsPlayerType Type { get; }

        /// <summary>사람만 있다. 봇 · 네이션은 null.</summary>
        public string ClientId { get; }

        public string Id { get; }

        /// <summary>시작 병력 강도 배율 — 원본 싱글에서는 늘 1 (<see cref="FrontWarsGameSetup"/>).</summary>
        public int NationStrength { get; }

        public FrontWarsPlayerInfo(string name, EFrontWarsPlayerType type, string clientId, string id, int nationStrength = 1)
        {
            Name = name;
            Type = type;
            ClientId = clientId;
            Id = id;
            NationStrength = nationStrength;
        }
    }

    /// <summary>금수 한 건 `[소스] embargoes.set(e.id(),{createdAt,isTemporary,target})`.</summary>
    public sealed class FrontWarsEmbargo
    {
        public int CreatedAt { get; set; }
        public bool IsTemporary { get; set; }
        public FrontWarsPlayer Target { get; set; }
    }

    /// <summary>보낸 이모지 한 건. 받는 쪽 −1 = 모두.</summary>
    public sealed class FrontWarsEmojiMessage
    {
        public string Emoji { get; }
        public int SenderSmallId { get; }
        public int RecipientSmallId { get; }
        public int CreatedAt { get; }

        public FrontWarsEmojiMessage(string emoji, int senderSmallId, int recipientSmallId, int createdAt)
        {
            Emoji = emoji;
            SenderSmallId = senderSmallId;
            RecipientSmallId = recipientSmallId;
            CreatedAt = createdAt;
        }
    }

    /// <summary>
    /// 플레이어 한 명의 시뮬 상태 `[소스] Cq3gr8XO.js:Nm=class{`.
    ///
    /// <para>
    /// ⚠ 병력은 원본이 BigInt(버림)로 담고 숫자로 꺼낸다 — 그래서 <c>long</c> 에 버림으로 담는다.
    /// 칸 집합 · 국경 집합 · 관계 · 금수 목록은 원본 순회 순서를 그대로 내야 같은 판이 나온다.
    /// </para>
    /// </summary>
    public sealed partial class FrontWarsPlayer
    {
        /// <summary>이름에 남기는 글자 `[소스] hm=/^[a-zA-Z0-9_[\] 🐈🍀üÜ.\-+,*^]+$/u`.</summary>
        private const string AllowedNameSymbols = "_[] .-+,*^üÜ";
        private const int CatEmoji = 0x1F408;
        private const int CloverEmoji = 0x1F340;
        private const int MaxNameLength = 27;
        private const int MinNameLength = 3;

        /// <summary>관계 감소 폭 · 0 으로 자르는 문턱은 2배 `[소스] decayRelations(){let e=.05`.</summary>
        private const double RelationDecay = 0.05;

        private const double RelationMin = -100;
        private const double RelationMax = 100;
        private const double HostileBelow = -50;
        private const double NeutralFrom = 0;
        private const double FriendlyFrom = 50;

        /// <summary>모두에게 보내는 이모지의 받는 쪽 id.</summary>
        public const int AllPlayersId = -1;

        private readonly FrontWarsGame _game;
        private readonly List<FrontWarsPlayer> _relationOrder = new List<FrontWarsPlayer>();
        private readonly Dictionary<FrontWarsPlayer, double> _relations = new Dictionary<FrontWarsPlayer, double>();
        private readonly List<FrontWarsEmbargo> _embargoes = new List<FrontWarsEmbargo>();
        private readonly List<FrontWarsAlliance> _alliances = new List<FrontWarsAlliance>();
        private List<FrontWarsAllianceRequest> _pastOutgoingAllianceRequests = new List<FrontWarsAllianceRequest>();
        private List<(int Tick, FrontWarsPlayer Target)> _targets = new List<(int, FrontWarsPlayer)>();
        private List<FrontWarsEmojiMessage> _outgoingEmojis = new List<FrontWarsEmojiMessage>();
        private List<(FrontWarsPlayer Recipient, int Tick)> _sentDonations = new List<(FrontWarsPlayer, int)>();
        private readonly List<FrontWarsUnit> _units = new List<FrontWarsUnit>();
        private readonly Dictionary<EFrontWarsUnit, int> _unitsConstructed = new Dictionary<EFrontWarsUnit, int>();

        public int SmallId { get; }
        public FrontWarsPlayerInfo Info { get; }
        public string Name { get; }

        public string Id
        {
            get { return Info.Id; }
        }

        public EFrontWarsPlayerType Type
        {
            get { return Info.Type; }
        }

        public FrontWarsGame Game
        {
            get { return _game; }
        }

        /// <summary>넣은 순서의 칸 집합 — 원본 <c>_tiles</c> (JS Set).</summary>
        public FrontWarsOrderedTileSet Tiles { get; } = new FrontWarsOrderedTileSet();

        /// <summary>국경 칸 — 원본 <c>_borderTiles</c>.</summary>
        public FrontWarsIndexedTileSet BorderTiles { get; }

        /// <summary>플레이어 전용 난수 — 시드 = 해시(id) `[소스] _pseudo_random=new W(P(this.playerInfo.id))`.</summary>
        public FrontWarsRandom PseudoRandom { get; }

        public long Troops { get; private set; }
        public long Gold { get; private set; }

        /// <summary>마지막으로 칸이 바뀐 틱 — 고립 조각 검사가 이걸 보고 건너뛴다.</summary>
        public int LastTileChange { get; internal set; }

        public bool HasSpawned { get; set; }
        public bool IsDisconnected { get; private set; }

        public int TraitorUntilTick { get; private set; }

        public int LastDeleteUnitTick { get; set; } = -1;
        public int LastUpgradeTick { get; set; } = -1;

        /// <summary>가장 큰 조각의 사각 범위 — 이름 표시 위치에 쓴다. 아직 계산 전이면 null.</summary>
        public FrontWarsTileBounds? LargestClusterBounds { get; set; }

        public List<FrontWarsAttack> OutgoingAttacks { get; } = new List<FrontWarsAttack>();
        public List<FrontWarsAttack> IncomingAttacks { get; } = new List<FrontWarsAttack>();

        /// <summary>팀 이름 — 개인전이면 null. 팀전 봇은 <see cref="FrontWarsGame.BotTeam"/> `[소스] team(){return this._team`.</summary>
        public string Team { get; }

        public FrontWarsPlayer(FrontWarsGame game, int smallId, FrontWarsPlayerInfo info, double startTroops, string team = null)
        {
            _game = game;
            SmallId = smallId;
            Info = info;
            Team = team;
            Name = CleanName(info.Name);
            Troops = ToStoredTroops(startTroops);
            PseudoRandom = new FrontWarsRandom(FrontWarsHash.Of(info.Id));
            BorderTiles = new FrontWarsIndexedTileSet(game.BorderPositionIndex);
        }

        public int NumTilesOwned
        {
            get { return Tiles.Count; }
        }

        public bool IsAlive
        {
            get { return Tiles.Count > 0; }
        }

        private int Ticks
        {
            get { return _game.Ticks; }
        }

        private JinHyung.Data.FrontWarsConfigData Config
        {
            get { return _game.Settings.Config; }
        }

        public void MarkDisconnected(bool value)
        {
            IsDisconnected = value;
        }

        // ── 병력 · 골드

        /// <summary>음수면 빼기로 넘긴다 `[소스] addTroops(e){`.</summary>
        public void AddTroops(double amount)
        {
            if (amount < 0)
            {
                RemoveTroops(-amount);
                return;
            }

            Troops += ToStoredTroops(amount);
        }

        /// <summary>가진 만큼만 뺀다. 실제로 뺀 양을 돌려준다 `[소스] removeTroops(e){`.</summary>
        public long RemoveTroops(double amount)
        {
            if (amount <= 0)
                return 0;

            long take = Math.Min(Troops, ToStoredTroops(amount));
            Troops -= take;
            return take;
        }

        public void SetTroops(double amount)
        {
            Troops = ToStoredTroops(amount);
        }

        /// <summary>골드 더하기. 칸을 주면 떠오르는 숫자 알림을 낸다 `[소스] addGold(e,t){`.</summary>
        public void AddGold(long amount, int tile = -1)
        {
            Gold += amount;

            if (tile >= 0)
                _game.RaiseGoldGain(this, amount, tile);
        }

        /// <summary>가진 만큼만 뺀다.</summary>
        public long RemoveGold(long amount)
        {
            if (amount <= 0)
                return 0;

            long take = Math.Min(Gold, amount);
            Gold -= take;
            return take;
        }

        // ── 칸

        public void Conquer(int tile)
        {
            _game.Conquer(this, tile);
        }

        public void Relinquish(int tile)
        {
            if (_game.OwnerSmallId(tile) != SmallId)
                throw new InvalidOperationException("Cannot relinquish tile not owned by this player");

            _game.Relinquish(tile);
        }

        /// <summary>내 국경에 그 주인(0 = 주인 없는 땅) 칸이 닿는가 `[소스] sharesBorderWith(e){`.</summary>
        public bool SharesBorderWith(int otherSmallId)
        {
            FrontWarsTerrainMap map = _game.Map;
            int[] scratch = _game.NeighborScratch;
            IReadOnlyList<int> border = BorderTiles.Items;

            for (int i = 0; i < border.Count; i++)
            {
                int n = map.NeighborsInto(border[i], scratch);
                for (int k = 0; k < n; k++)
                {
                    if (_game.OwnerSmallId(scratch[k]) == otherSmallId)
                        return true;
                }
            }

            return false;
        }

        public bool SharesBorderWith(FrontWarsPlayer other)
        {
            return SharesBorderWith(other?.SmallId ?? 0);
        }

        /// <summary>
        /// 국경에 닿은 다른 주인들 — 처음 만난 순서 `[소스] neighbors(){`.
        /// <para>물 칸은 건너뛴다. 주인 없는 땅은 <c>null</c> 한 칸으로 그 자리에 들어간다 (원본 terra nullius — 봇이 이 목록을 섞으므로 길이 · 순서가 같아야 한다).</para>
        /// </summary>
        public List<FrontWarsPlayer> Neighbors()
        {
            var seen = new HashSet<int>();
            var result = new List<FrontWarsPlayer>();
            FrontWarsTerrainMap map = _game.Map;
            int[] scratch = new int[4];
            IReadOnlyList<int> border = BorderTiles.Items;

            for (int i = 0; i < border.Count; i++)
            {
                int n = map.NeighborsInto(border[i], scratch);
                for (int k = 0; k < n; k++)
                {
                    int t = scratch[k];
                    if (map.IsLand(t) == false)
                        continue;

                    int owner = _game.OwnerSmallId(t);
                    if (owner == SmallId || seen.Add(owner) == false)
                        continue;

                    result.Add(_game.PlayerBySmallId(owner));
                }
            }

            return result;
        }

        /// <summary>
        /// 그 칸을 칠 수 있나 `[소스] canAttack(e){`.
        /// <para>스폰 보호 중 남의 칸 · 내 칸 · 우호 · 물이면 아니다. 남의 칸이면 국경이 닿아야 하고, 빈 땅이면 빈 땅으로 200칸 안에 내 땅이 닿아야 한다.</para>
        /// </summary>
        public bool CanAttack(int tile)
        {
            bool owned = _game.HasOwner(tile);

            if ((owned && _game.Settings.SpawnPhaseTurns + Config.SpawnImmunityTicks > Ticks) || _game.OwnerSmallId(tile) == SmallId)
                return false;

            FrontWarsPlayer owner = _game.Owner(tile);

            if ((owner != null && IsFriendly(owner)) || _game.Map.IsLand(tile) == false)
                return false;

            if (owned)
                return SharesBorderWith(owner);

            int[] scratch = new int[4];
            return _game.AttackReachSearch.Search(_game.Map, tile, FrontWarsGame.AttackReachRadius,
                t => _game.HasOwner(t) == false && _game.Map.IsLand(t),
                t =>
                {
                    int n = _game.Map.NeighborsInto(t, scratch);
                    for (int k = 0; k < n; k++)
                    {
                        if (_game.OwnerSmallId(scratch[k]) == SmallId)
                            return true;
                    }

                    return false;
                });
        }

        // ── 공격

        /// <summary>공격 만들기 — id 는 플레이어 난수 `[소스] createAttack(e,t,n,r){`.</summary>
        public FrontWarsAttack CreateAttack(FrontWarsPlayer target, double troops, int sourceTile)
        {
            var attack = new FrontWarsAttack(PseudoRandom.NextId(), target, this, troops, sourceTile);
            OutgoingAttacks.Add(attack);
            target?.IncomingAttacks.Add(attack);
            return attack;
        }

        public void OrderRetreat(string attackId)
        {
            OutgoingAttacks.FirstOrDefault(a => a.Id == attackId)?.OrderRetreat();
        }

        public void ExecuteRetreat(string attackId)
        {
            OutgoingAttacks.FirstOrDefault(a => a.Id == attackId)?.ExecuteRetreat();
        }

        // ── 관계

        public EFrontWarsRelation Relation(FrontWarsPlayer other)
        {
            if (other == this)
                throw new InvalidOperationException("cannot get relation with self");

            return RelationFromValue(RelationValue(other));
        }

        public double RelationValue(FrontWarsPlayer other)
        {
            return _relations.TryGetValue(other, out double v) ? v : 0;
        }

        public static EFrontWarsRelation RelationFromValue(double value)
        {
            if (value < HostileBelow)
                return EFrontWarsRelation.Hostile;
            if (value < NeutralFrom)
                return EFrontWarsRelation.Distrustful;

            return value < FriendlyFrom ? EFrontWarsRelation.Neutral : EFrontWarsRelation.Friendly;
        }

        /// <summary>값 오름차순 (같으면 넣은 순서) `[소스] allRelationsSorted(){`.</summary>
        public List<(FrontWarsPlayer Player, EFrontWarsRelation Relation)> AllRelationsSorted()
        {
            return _relationOrder.OrderBy(p => _relations[p]).Select(p => (p, RelationFromValue(_relations[p]))).ToList();
        }

        /// <summary>−100~100 으로 자른다 `[소스] updateRelation(e,t){`.</summary>
        public void UpdateRelation(FrontWarsPlayer other, double delta)
        {
            if (other == this)
                throw new InvalidOperationException("cannot update relation with self");

            double v = Math.Min(Math.Max(RelationValue(other) + delta, RelationMin), RelationMax);

            if (_relations.ContainsKey(other) == false)
                _relationOrder.Add(other);

            _relations[other] = v;
        }

        /// <summary>매 틱 0 쪽으로 0.05. 절댓값이 0.1 아래면 0 `[소스] decayRelations(){`.</summary>
        public void DecayRelations()
        {
            for (int i = 0; i < _relationOrder.Count; i++)
            {
                FrontWarsPlayer p = _relationOrder[i];
                double v = _relations[p];

                if (v == 0)
                    continue;

                double r = v - Math.Sign(v) * RelationDecay;
                if (Math.Abs(r) < RelationDecay * 2)
                    r = 0;

                _relations[p] = r;
            }
        }

        // ── 배신 · 팀 · 우호

        public bool IsTraitor
        {
            get { return Ticks < TraitorUntilTick; }
        }

        public int TraitorRemainingTicks
        {
            get { return Math.Max(0, TraitorUntilTick - Ticks); }
        }

        public void MarkTraitor()
        {
            TraitorUntilTick = Ticks + Config.TraitorDurationTicks;
        }

        /// <summary>
        /// 같은 팀인가 — 자기 자신 · 팀 없음(개인전) · 봇 팀이면 거짓 `[소스] isOnSameTeam(e){return e===this||this.team()===null||e.team()===null||this.team()===_.Bot||e.team()===_.Bot?!1:this._team===e.team()}`.
        /// </summary>
        public bool IsOnSameTeam(FrontWarsPlayer other)
        {
            if (other == this || Team == null || other.Team == null || Team == FrontWarsGame.BotTeam || other.Team == FrontWarsGame.BotTeam)
                return false;

            return Team == other.Team;
        }

        /// <summary>끊긴 상대는 우호가 아니다. 같은 팀이거나 동맹 `[소스] isFriendly(e){`.</summary>
        public bool IsFriendly(FrontWarsPlayer other)
        {
            if (other.IsDisconnected)
                return false;

            return IsOnSameTeam(other) || IsAlliedWith(other);
        }

        // ── 동맹

        public void AddAllianceRef(FrontWarsAlliance alliance)
        {
            _alliances.Add(alliance);
        }

        public void RemoveAllianceRef(FrontWarsAlliance alliance)
        {
            _alliances.Remove(alliance);
        }

        public List<FrontWarsAlliance> Alliances()
        {
            return new List<FrontWarsAlliance>(_alliances);
        }

        public List<FrontWarsPlayer> Allies()
        {
            return _alliances.Select(a => a.Other(this)).ToList();
        }

        public bool IsAlliedWith(FrontWarsPlayer other)
        {
            return other != this && AllianceWith(other) != null;
        }

        public FrontWarsAlliance AllianceWith(FrontWarsPlayer other)
        {
            if (other == this)
                return null;

            foreach (FrontWarsAlliance a in _alliances)
            {
                if (a.Recipient == other || a.Requestor == other)
                    return a;
            }

            return null;
        }

        public List<FrontWarsAllianceRequest> IncomingAllianceRequests()
        {
            return _game.Diplomacy.Requests.Where(r => r.Recipient == this).ToList();
        }

        public List<FrontWarsAllianceRequest> OutgoingAllianceRequests()
        {
            return _game.Diplomacy.Requests.Where(r => r.Requestor == this).ToList();
        }

        public bool HasOutgoingAllianceRequest
        {
            get { return _game.Diplomacy.Requests.Any(r => r.Requestor == this); }
        }

        /// <summary>동맹 끔 · 자기 · 끊김 · 이미 우호 · 죽음 · 이미 보냄이면 아니고, 쿨다운이 0 이어야 한다 `[소스] canSendAllianceRequest(e){`.</summary>
        public bool CanSendAllianceRequest(FrontWarsPlayer other)
        {
            if (_game.Settings.DisableAlliances || other == this || IsDisconnected || other.IsDisconnected || IsFriendly(other) || IsAlive == false
                || OutgoingAllianceRequests().Any(r => r.Recipient == other))
                return false;

            return AllianceRequestCooldownTicks(other) == 0;
        }

        public int AllianceRequestCooldownTicks(FrontWarsPlayer other)
        {
            int last = -1;

            foreach (FrontWarsAllianceRequest r in _pastOutgoingAllianceRequests)
            {
                if (r.Recipient == other && r.CreatedAt > last)
                    last = r.CreatedAt;
            }

            if (last < 0)
                return 0;

            return Math.Max(0, Config.AllianceRequestCooldownTicks - (Ticks - last));
        }

        public void RecordPastOutgoingAllianceRequest(FrontWarsAllianceRequest request)
        {
            _pastOutgoingAllianceRequests.Add(request);
            int cooldown = Config.AllianceRequestCooldownTicks;
            _pastOutgoingAllianceRequests = _pastOutgoingAllianceRequests.FindAll(r => Ticks - r.CreatedAt < cooldown);
        }

        public FrontWarsAllianceRequest CreateAllianceRequest(FrontWarsPlayer recipient)
        {
            if (IsAlliedWith(recipient))
                throw new InvalidOperationException("cannot create alliance request, already allies");

            return _game.Diplomacy.CreateAllianceRequest(this, recipient);
        }

        public void BreakAlliance(FrontWarsAlliance alliance)
        {
            _game.Diplomacy.BreakAlliance(this, alliance);
        }

        // ── 금수

        public IReadOnlyList<FrontWarsEmbargo> Embargoes
        {
            get { return _embargoes; }
        }

        public bool HasEmbargoAgainst(FrontWarsPlayer other)
        {
            return _embargoes.Exists(e => e.Target.Id == other.Id);
        }

        /// <summary>영구 금수가 이미 있으면 그대로 두고, 아니면 (임시를 덮어) 새로 건다 `[소스] addEmbargo(e,t){`.</summary>
        public void AddEmbargo(FrontWarsPlayer other, bool temporary)
        {
            FrontWarsEmbargo existing = _embargoes.Find(e => e.Target.Id == other.Id);

            if (existing != null && existing.IsTemporary == false)
                return;

            if (existing != null)
            {
                existing.CreatedAt = Ticks;
                existing.IsTemporary = temporary;
                existing.Target = other;
            }
            else
            {
                _embargoes.Add(new FrontWarsEmbargo { CreatedAt = Ticks, IsTemporary = temporary, Target = other });
            }

            _game.RaiseEmbargo(this, other, true);
        }

        public void StopEmbargo(FrontWarsPlayer other)
        {
            _embargoes.RemoveAll(e => e.Target.Id == other.Id);
            _game.RaiseEmbargo(this, other, false);
        }

/// <summary>영구 금수가 아니면 푼다 (없어도 푸는 알림을 낸다) `[소스] endTemporaryEmbargo(e){…t!==void 0&&!t.isTemporary||this.stopEmbargo(e)}`.</summary>
        public void EndTemporaryEmbargo(FrontWarsPlayer other)
        {
            FrontWarsEmbargo existing = _embargoes.Find(e => e.Target.Id == other.Id);

            if (existing != null && existing.IsTemporary == false)
                return;

            StopEmbargo(other);
        }

        public bool CanTrade(FrontWarsPlayer other)
        {
            return other.HasEmbargoAgainst(this) == false && HasEmbargoAgainst(other) == false && other.Id != Id;
        }

        // ── 타겟

        /// <summary>자기 · 우호면 아니고, 쿨다운 안에 찍은 게 있으면 아니다 `[소스] canTarget(e){`.</summary>
        public bool CanTarget(FrontWarsPlayer other)
        {
            if (this == other || IsFriendly(other))
                return false;

            foreach (var t in _targets)
            {
                if (Ticks - t.Tick < Config.TargetCooldownTicks)
                    return false;
            }

            return true;
        }

        public void Target(FrontWarsPlayer other)
        {
            _targets.Add((Ticks, other));
            int keep = Math.Max(Config.TargetCooldownTicks, Config.TargetDurationTicks);
            _targets = _targets.FindAll(t => Ticks - t.Tick < keep);
            _game.RaiseTarget(this, other);
        }

        /// <summary>지속 시간 안의 타겟 `[소스] targets(){`.</summary>
        public List<FrontWarsPlayer> Targets()
        {
            return _targets.Where(t => Ticks - t.Tick < Config.TargetDurationTicks).Select(t => t.Target).ToList();
        }

        // ── 이모지

        /// <summary>받는 쪽(null = 모두)에게 쿨다운 안에 보낸 게 없어야 한다 `[소스] canSendEmoji(e){`.</summary>
        public bool CanSendEmoji(FrontWarsPlayer recipient)
        {
            if (recipient == this)
                return false;

            int id = recipient == null ? AllPlayersId : recipient.SmallId;

            foreach (FrontWarsEmojiMessage m in _outgoingEmojis)
            {
                if (m.RecipientSmallId == id && Ticks - m.CreatedAt < Config.EmojiCooldownTicks)
                    return false;
            }

            return true;
        }

        public void SendEmoji(FrontWarsPlayer recipient, string emoji)
        {
            if (recipient == this)
                throw new InvalidOperationException("Cannot send emoji to oneself");

            var message = new FrontWarsEmojiMessage(emoji, SmallId, recipient == null ? AllPlayersId : recipient.SmallId, Ticks);
            _outgoingEmojis.Add(message);
            int keep = Math.Max(Config.EmojiDurationTicks, Config.EmojiCooldownTicks);
            _outgoingEmojis = _outgoingEmojis.FindAll(m => Ticks - m.CreatedAt < keep);
            _game.RaiseEmoji(message);
        }

        /// <summary>표시 중인 이모지 — 최근 것부터 `[소스] outgoingEmojis(){`.</summary>
        public List<FrontWarsEmojiMessage> OutgoingEmojis()
        {
            return _outgoingEmojis.Where(m => Ticks - m.CreatedAt < Config.EmojiDurationTicks).OrderByDescending(m => m.CreatedAt).ToList();
        }

        // ── 기부

        public bool CanDonateGold(FrontWarsPlayer other)
        {
            if (IsAlive == false || other.IsAlive == false || IsFriendly(other) == false || _game.Settings.DonateGold == false)
                return false;

            return DonationCooldownTicks(other) == 0;
        }

        public bool CanDonateTroops(FrontWarsPlayer other)
        {
            if (IsAlive == false || other.IsAlive == false || IsFriendly(other) == false || _game.Settings.DonateTroops == false)
                return false;

            return DonationCooldownTicks(other) == 0;
        }

        public int DonationCooldownTicks(FrontWarsPlayer other)
        {
            int last = -1;

            foreach (var d in _sentDonations)
            {
                if (d.Recipient == other && d.Tick > last)
                    last = d.Tick;
            }

            if (last < 0)
                return 0;

            return Math.Max(0, Config.DonateCooldownTicks - (Ticks - last));
        }

        private void RecordDonation(FrontWarsPlayer other)
        {
            _sentDonations.Add((other, Ticks));
            _sentDonations = _sentDonations.FindAll(d => Ticks - d.Tick < Config.DonateCooldownTicks);
        }

        public bool DonateTroops(FrontWarsPlayer other, double troops)
        {
            if (troops <= 0)
                return false;

            long sent = RemoveTroops(troops);
            if (sent == 0)
                return false;

            other.AddTroops(sent);
            RecordDonation(other);
            _game.DisplayMessage(EFrontWarsMessage.SentTroopsToPlayer, this, troops, other.Name);
            _game.DisplayMessage(EFrontWarsMessage.ReceivedTroopsFromPlayer, other, troops, Name);
            return true;
        }

        public bool DonateGold(FrontWarsPlayer other, long gold)
        {
            if (gold <= 0)
                return false;

            long sent = RemoveGold(gold);
            if (sent == 0)
                return false;

            other.AddGold(sent);
            RecordDonation(other);
            _game.DisplayMessage(EFrontWarsMessage.SentGoldToPlayer, this, gold, other.Name);
            _game.DisplayMessage(EFrontWarsMessage.ReceivedGoldFromPlayer, other, gold, Name);
            return true;
        }

        // ── 유닛

        public IReadOnlyList<FrontWarsUnit> AllUnits
        {
            get { return _units; }
        }

        public List<FrontWarsUnit> Units(params EFrontWarsUnit[] types)
        {
            if (types == null || types.Length == 0)
                return new List<FrontWarsUnit>(_units);

            return _units.FindAll(u => Array.IndexOf(types, u.Type) >= 0);
        }

        internal void AddUnitRef(FrontWarsUnit unit)
        {
            _units.Add(unit);
        }

        internal void RemoveUnitRef(FrontWarsUnit unit)
        {
            _units.Remove(unit);
        }

        /// <summary>지은 수 + 지금 짓는 중인 수 `[소스] unitsConstructed(e){`.</summary>
        public int UnitsConstructed(EFrontWarsUnit type)
        {
            int n = _unitsConstructed.TryGetValue(type, out int built) ? built : 0;

            foreach (FrontWarsUnit u in _units)
            {
                if (u.Type == EFrontWarsUnit.Construction && u.ConstructionTypeValue == type)
                    n++;
            }

            return n;
        }

        /// <summary>그 종류 레벨 합 `[소스] unitCount(e){`.</summary>
        public int UnitCount(EFrontWarsUnit type)
        {
            int n = 0;

            foreach (FrontWarsUnit u in _units)
            {
                if (u.Type == type)
                    n += u.Level;
            }

            return n;
        }

        /// <summary>레벨 합 + 짓는 중인 수 `[소스] unitsOwned(e){`.</summary>
        public int UnitsOwned(EFrontWarsUnit type)
        {
            int n = 0;

            foreach (FrontWarsUnit u in _units)
            {
                if (u.Type == type)
                {
                    n += u.Level;
                    continue;
                }

                if (u.Type == EFrontWarsUnit.Construction && u.ConstructionTypeValue == type)
                    n++;
            }

            return n;
        }

        public void CaptureUnit(FrontWarsUnit unit)
        {
            if (unit.Owner == this)
                throw new InvalidOperationException($"Cannot capture unit, {Name} already owns {unit.Type}");

            unit.SetOwner(this);
        }

        internal void RecordUnitConstructed(EFrontWarsUnit type)
        {
            _unitsConstructed[type] = UnitsConstructed(type) + 1;
        }

        public bool CanDeleteUnit()
        {
            return Ticks - LastDeleteUnitTick >= Config.DeleteUnitCooldownTicks;
        }

        public void RecordDeleteUnit()
        {
            LastDeleteUnitTick = Ticks;
        }

        // ── 해시

        /// <summary>
        /// 해시(id) × (병력 + 칸 수) + 유닛 해시 합 `[소스] hash(){return P(this.id())*(`.
        /// <para>원본이 JS 숫자(double)로 더하므로 double 로 계산한다 — 큰 값에서 반올림까지 같게.</para>
        /// </summary>
        public double Hash()
        {
            double h = (double)FrontWarsHash.Of(Id) * ((double)Troops + NumTilesOwned);
            double units = 0;

            foreach (FrontWarsUnit u in _units)
                units += u.Hash();

            return h + units;
        }

        // ── 도우미

        /// <summary>BigInt(버림) — 무한대는 ±(2^53−1) `[소스] function Lt(e){`.</summary>
        public static long ToStoredTroops(double value)
        {
            const long maxSafe = (1L << 53) - 1;

            if (double.IsPositiveInfinity(value))
                return maxSafe;
            if (double.IsNegativeInfinity(value))
                return -maxSafe;

            return (long)Math.Floor(value);
        }

        /// <summary>
        /// 허용 글자만 남기고 27자(UTF-16 단위)로 자른 뒤 3자가 안 되면 x 로 채운다 `[소스] function ym(e){`.
        /// </summary>
        public static string CleanName(string name)
        {
            var sb = new StringBuilder(name?.Length ?? 0);

            if (name != null)
            {
                for (int i = 0; i < name.Length; i++)
                {
                    // 원본 Array.from 은 글자(코드 포인트) 단위로 돈다 — 짝 맞은 대리쌍은 한 글자
                    bool pair = char.IsHighSurrogate(name[i]) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]);
                    int cp = pair ? char.ConvertToUtf32(name[i], name[i + 1]) : name[i];
                    int units = pair ? 2 : 1;

                    if (IsAllowedNameCodePoint(cp))
                        sb.Append(name, i, units);

                    i += units - 1;
                }
            }

            string s = sb.ToString();
            if (s.Length > MaxNameLength)
                s = s.Substring(0, MaxNameLength);

            return s.PadRight(MinNameLength, 'x');
        }

        private static bool IsAllowedNameCodePoint(int cp)
        {
            if (cp == CatEmoji || cp == CloverEmoji)
                return true;

            if (cp > 0xFFFF)
                return false;

            char c = (char)cp;
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
                   || AllowedNameSymbols.IndexOf(c) >= 0;
        }
    }

    /// <summary>칸 묶음의 사각 범위 `[소스] function Mt(e,t){`.</summary>
    public readonly struct FrontWarsTileBounds
    {
        public readonly int MinX, MinY, MaxX, MaxY;

        public FrontWarsTileBounds(int minX, int minY, int maxX, int maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        /// <summary>이 범위가 other 를 완전히 품는가 `[소스] function Pt(e,t){`.</summary>
        public bool Contains(FrontWarsTileBounds other)
        {
            return MinX <= other.MinX && MinY <= other.MinY && MaxX >= other.MaxX && MaxY >= other.MaxY;
        }

        public static FrontWarsTileBounds Of(FrontWarsTerrainMap map, IEnumerable<int> tiles)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;

            foreach (int t in tiles)
            {
                int x = map.X(t), y = map.Y(t);
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }

            return new FrontWarsTileBounds(minX, minY, maxX, maxY);
        }
    }
}
