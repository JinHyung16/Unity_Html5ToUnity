using System;
using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 네이션 한 곳 — 맵 manifest 의 한 줄 `[소스] Cq3gr8XO.js:ie=class{constructor(e,t,n){this.spawnCell=e,this.strength=t,this.playerInfo=n}}`.
    /// </summary>
    public sealed class FrontWarsNation
    {
        public FrontWarsPlayerInfo Info { get; }

        /// <summary>스폰 기준 칸 좌표. manifest 에 없으면 null (맵 아무 곳).</summary>
        public int? SpawnX { get; }

        public int? SpawnY { get; }

        /// <summary>manifest 강도 (1~2). 시작 병력에는 안 쓰인다 — <see cref="FrontWarsGameSetup"/> 주석.</summary>
        public int Strength { get; }

        public FrontWarsNation(FrontWarsPlayerInfo info, int? spawnX, int? spawnY, int strength)
        {
            Info = info;
            SpawnX = spawnX;
            SpawnY = spawnY;
            Strength = strength;
        }
    }

    /// <summary>
    /// 결정적 시뮬 본체 — 한 틱 = 한 턴 = 100ms `[소스] Cq3gr8XO.js:ih=class{`.
    ///
    /// <para>
    /// 칸 주인 · 플레이어 목록 · 실행 목록 · 틱 번호를 가진다. 같은 시드 · 같은 명령이면 같은 판이 나와야 하므로
    /// 순회 순서를 원본과 똑같이 둔다 — 플레이어는 넣은 순서, 실행은 넣은 순서, 칸 집합은 전용 자료 구조.
    /// </para>
    ///
    /// <para>순수 C# 이다. Unity 는 <see cref="FrontWarsGameManager"/> 가 100ms 마다 <see cref="ExecuteNextTick"/> 을 부른다.</para>
    /// </summary>
    public sealed class FrontWarsGame
    {
        /// <summary>칸 상태의 주인 비트 폭 — 작은 id 최대 4095 `[소스] PLAYER_ID_MASK=4095`.</summary>
        public const int MaxSmallId = 4095;

        /// <summary>자동 스폰은 스폰 단계 끝나기 이만큼 전 `[소스] numSpawnPhaseTurns()-10`.</summary>
        private const int AutoSpawnBeforeEnd = 10;

        /// <summary>무작위 스폰일 때 자동 스폰도 2틱에 한다 `[소스] this._ticks===2&&…randomSpawn`.</summary>
        private const int RandomSpawnAutoTick = 2;

        /// <summary>자동 스폰 칸 찾기 — 시도 수 · 무작위 스폰일 때 서로 떨어질 거리.</summary>
        private const int AutoSpawnAttempts = 50;
        private const int RandomSpawnSeparation = 30;

        /// <summary>해시를 남기는 틱 간격 `[소스] this.ticks()%10==0`.</summary>
        public const int HashInterval = 10;

        /// <summary>팀전에서 봇이 드는 팀 `[소스] this.botTeam=_.Bot` · `_={Red:`Red`,…,Bot:`Bot`}`.</summary>
        public const string BotTeam = "Bot";

        /// <summary>팀 나누기 값 `[소스] const f=`Duos`,p=`Trios`,m=`Quads`,h=`Humans`,g=`Nations``.</summary>
        public const string Duos = "Duos";
        public const string Trios = "Trios";
        public const string Quads = "Quads";
        public const string HumansVsNations = "HumansVsNations";

        /// <summary>사람 대 네이션 팀 이름.</summary>
        public const string HumansTeam = "Humans";
        public const string NationsTeam = "Nations";

        /// <summary>팀이 8개 미만일 때 쓰는 색 이름 — 넣는 순서 그대로 `[소스] this.playerTeams=[_.Red,_.Blue],e>=3&&this.playerTeams.push(_.Yellow),e>=4&&…_.Green…_.Purple…_.Orange…_.Teal`.</summary>
        private static readonly string[] NamedTeams = { "Red", "Blue", "Yellow", "Green", "Purple", "Orange", "Teal" };

        /// <summary>이 수부터는 「Team 1」… 이름 `[소스] if(e<8)`.</summary>
        private const int NamedTeamLimit = 8;

        private readonly List<string> _playerTeams = new List<string>();

        private readonly ushort[] _owner;
        private readonly List<FrontWarsPlayer> _players = new List<FrontWarsPlayer>();
        private readonly Dictionary<string, FrontWarsPlayer> _playersById = new Dictionary<string, FrontWarsPlayer>();
        private readonly List<FrontWarsPlayer> _playersBySmallId = new List<FrontWarsPlayer>();
        private readonly List<FrontWarsNation> _nations;
        private readonly int[] _updateBordersScratch = new int[4];
        private readonly int[] _calcIsBorderScratch = new int[4];

        private List<IFrontWarsExecution> _execs = new List<IFrontWarsExecution>();
        private List<IFrontWarsExecution> _unInitExecs = new List<IFrontWarsExecution>();
        private int _nextPlayerId = 1;

        public FrontWarsTerrainMap Map { get; }
        public FrontWarsGameSettings Settings { get; }
        public FrontWarsRules Rules { get; }

        /// <summary>국경 집합이 같이 쓰는 위치 배열 `[소스] borderPosIndex=new Int32Array(`.</summary>
        public int[] BorderPositionIndex { get; }

        /// <summary>4칸 임시 배열 — 호출 안에서만 쓰고 넘기지 않는다.</summary>
        internal int[] NeighborScratch { get; } = new int[4];

        public int Ticks { get; private set; }

        /// <summary>승리 판정 분모 (해안 아닌 안쪽 산 제외) `[소스] function ag(e){`. 물 핵으로 줄어든다.</summary>
        public int NumLandTiles { get; private set; }

        /// <summary>축소 맵 (길 찾기 · 축소 맵 설정) `[소스] miniMap(){return this.miniGameMap}`.</summary>
        public FrontWarsTerrainMap MiniMap { get; }

        /// <summary>길 찾기 묶음 — <c>Sim/Path/</c> 구현을 붙인다.</summary>
        public IFrontWarsPathFactory Paths { get; set; }

        /// <summary>배 · 핵이 오고 있다 — 유닛 id · 알림 종류 · 받는 플레이어.</summary>
        public event Action<int, EFrontWarsMessage, FrontWarsPlayer> OnIncomingUnit;

        public void DisplayIncomingUnit(int unitId, EFrontWarsMessage type, FrontWarsPlayer target)
        {
            OnIncomingUnit?.Invoke(unitId, type, target);
        }

        /// <summary>
        /// 땅을 물로 바꾼다 (물 핵) `[소스] convertToWater(e){`.
        /// <para>주인이 있으면 내놓고 낙진을 지운 뒤, 축소 맵 칸도 물로 · 길 찾기에 알림 · 4방향 이웃을 해안으로 표시한다.</para>
        /// </summary>
        public void ConvertToWater(int tile)
        {
            if (Map.IsLand(tile) == false || Map.IsImpassable(tile))
                return;

            if (HasOwner(tile))
                Relinquish(tile);

            SetFalloutRaw(tile, false);
            Map.ConvertToWater(tile);
            NumLandTiles--;

            if (MiniMap != null && MiniMap != Map)
            {
                int mx = Map.X(tile) >> 1, my = Map.Y(tile) >> 1;

                if (MiniMap.IsValidCoord(mx, my))
                {
                    int mini = MiniMap.Ref(mx, my);
                    if (MiniMap.IsWater(mini) == false)
                        MiniMap.ConvertToWater(mini);
                }
            }

            Paths?.ApplyWaterConversion(tile);
            TileUpdates.Add(tile);

            int n = Map.NeighborsInto(tile, NeighborScratch);
            for (int i = 0; i < n; i++)
            {
                if (Map.MarkShoreline(NeighborScratch[i]))
                    TileUpdates.Add(NeighborScratch[i]);
            }
        }

        /// <summary>낙진 칸 수 — 핵은 단위 12.</summary>
        public int NumTilesWithFallout { get; private set; }

        /// <summary>개인전 승자. 팀전이면 null 이고 <see cref="WinnerTeam"/> 에 팀 이름이 들어간다.</summary>
        public FrontWarsPlayer Winner { get; private set; }

        /// <summary>팀전 승리 팀 이름 `[소스] setWinner(e,t){this._winner=e` (e 가 문자열).</summary>
        public string WinnerTeam { get; private set; }

        /// <summary>승자가 나왔나 (개인 · 팀) `[소스] this.game.winner()!==null`.</summary>
        public bool HasWinner
        {
            get { return Winner != null || WinnerTeam != null; }
        }

        /// <summary>플레이어 팀 목록 (봇 팀 제외 · 만든 순서). 개인전이면 비어 있다 `[소스] populateTeams(){`.</summary>
        public IReadOnlyList<string> PlayerTeams
        {
            get { return _playerTeams; }
        }

        /// <summary>마지막으로 계산한 해시와 그 틱.</summary>
        public double LastHash { get; private set; }

        public int LastHashTick { get; private set; } = -1;

        /// <summary>이번 틱에 주인이 바뀐 칸 — 그리기가 가져가고 비운다.</summary>
        public List<int> TileUpdates { get; } = new List<int>(1024);

        /// <summary>화면 이벤트 알림 (공격 취소 · 정복 · 동맹 …) — HUD 이벤트 패널이 받는다.</summary>
        public event Action<FrontWarsMessage> OnMessage;

        public event Action<FrontWarsEmojiMessage> OnEmoji;
        public event Action<FrontWarsPlayer, FrontWarsPlayer> OnTarget;
        public event Action<FrontWarsPlayer, FrontWarsPlayer, bool> OnEmbargo;

        /// <summary>떠오르는 골드 숫자 — 받은 플레이어 · 양 · 칸.</summary>
        public event Action<FrontWarsPlayer, long, int> OnGoldGain;

        public FrontWarsDiplomacy Diplomacy { get; }
        public FrontWarsUnitGrid Units { get; }

        /// <summary>빈 땅 공격 도달 검사 반경 `[소스] this.attackReachBFS=new nh(200)`.</summary>
        public const int AttackReachRadius = 200;

        public FrontWarsBoundedBfs AttackReachSearch { get; } = new FrontWarsBoundedBfs(AttackReachRadius);

        internal void RaiseEmoji(FrontWarsEmojiMessage message) => OnEmoji?.Invoke(message);
        internal void RaiseTarget(FrontWarsPlayer from, FrontWarsPlayer to) => OnTarget?.Invoke(from, to);
        internal void RaiseEmbargo(FrontWarsPlayer from, FrontWarsPlayer to, bool start) => OnEmbargo?.Invoke(from, to, start);
        internal void RaiseGoldGain(FrontWarsPlayer player, long gold, int tile) => OnGoldGain?.Invoke(player, gold, tile);

        /// <summary>핵 폭발 — 칸 · 종류 · 바깥 반경. 연출만 받는다 (시뮬 상태 · 해시와 무관).</summary>
        public event Action<int, EFrontWarsUnit, int> OnNukeDetonated;

        internal void RaiseNukeDetonated(int tile, EFrontWarsUnit type, int outerRadius) => OnNukeDetonated?.Invoke(tile, type, outerRadius);

        public event Action<int, double> OnHash;
        public event Action<FrontWarsPlayer> OnWinner;

        /// <summary>팀전에서 팀이 이겼다 — 팀 이름. 이때는 <see cref="OnWinner"/> 가 오지 않는다.</summary>
        public event Action<string> OnTeamWinner;
        public event Action<FrontWarsPlayer, FrontWarsPlayer, long> OnConquerPlayer;

        public FrontWarsGame(FrontWarsTerrainMap map, FrontWarsTerrainMap miniMap, FrontWarsGameSettings settings, IReadOnlyList<FrontWarsPlayerInfo> humans, IReadOnlyList<FrontWarsNation> nations)
        {
            Map = map;
            MiniMap = miniMap;
            Settings = settings;
            Rules = new FrontWarsRules(settings);
            _owner = new ushort[map.TileCount];
            BorderPositionIndex = new int[map.TileCount];
            NumLandTiles = map.CountLandTilesForWin();
            _nations = new List<FrontWarsNation>(nations);
            Diplomacy = new FrontWarsDiplomacy(this);
            Units = new FrontWarsUnitGrid(this);
            RailNetwork = new FrontWarsRailNetwork(this);

            // 원본은 플레이어보다 먼저 철도망 재계산 실행을 넣는다 `[소스] this.addExecution(new Pm(this._railNetwork))`
            AddExecution(new FrontWarsRailRecomputeExecution(RailNetwork));

            if (settings.IsTeamMode)
                PopulateTeams(humans.Count);

            AddPlayers(humans);
        }

        /// <summary>
        /// 팀 목록을 만든다 `[소스] populateTeams(){`.
        /// <list type="bullet">
        /// <item>사람 대 네이션 → [Humans, Nations]</item>
        /// <item>Duos · Trios · Quads → max(2, ⌈(사람 + 네이션) ÷ 2 · 3 · 4⌉) 팀</item>
        /// <item>8팀 미만 → Red · Blue · Yellow · Green · Purple · Orange · Teal 앞에서부터, 8팀 이상 → 「Team 1」~「Team N」</item>
        /// </list>
        /// </summary>
        private void PopulateTeams(int humanCount)
        {
            string config = Settings.PlayerTeams;

            if (config == HumansVsNations)
            {
                _playerTeams.Add(HumansTeam);
                _playerTeams.Add(NationsTeam);
                return;
            }

            int total = humanCount + _nations.Count;
            int count;

            switch (config)
            {
                case Duos:
                    count = Math.Max(2, (total + 1) / 2);
                    break;
                case Trios:
                    count = Math.Max(2, (total + 2) / 3);
                    break;
                case Quads:
                    count = Math.Max(2, (total + 3) / 4);
                    break;
                default:
                    // 원본 설정값이 없으면 0 이다 `[소스] playerTeams(){return this._gameConfig.playerTeams??0}`
                    if (config == null)
                        count = 0;
                    else if (int.TryParse(config, out count) == false)
                        throw new InvalidOperationException($"Unknown TeamCountConfig {config}");
                    break;
            }

            if (count < 2)
                throw new InvalidOperationException($"Too few teams: {count}");

            if (count < NamedTeamLimit)
            {
                for (int i = 0; i < count; i++)
                    _playerTeams.Add(NamedTeams[i]);
            }
            else
            {
                for (int i = 1; i <= count; i++)
                    _playerTeams.Add($"Team {i}");
            }
        }

        /// <summary>
        /// 사람 · 네이션을 넣는다 `[소스] addPlayers(){`.
        /// <para>개인전 · 사람 대 네이션은 사람 → 네이션 순서. 그 밖의 팀전은 팀 고르기 결과 순서(네이션이 섞인 순서)라 작은 id 가 달라진다.</para>
        /// </summary>
        private void AddPlayers(IReadOnlyList<FrontWarsPlayerInfo> humans)
        {
            if (Settings.IsTeamMode == false)
            {
                for (int i = 0; i < humans.Count; i++)
                    AddPlayer(humans[i]);

                for (int i = 0; i < _nations.Count; i++)
                    AddPlayer(_nations[i].Info);

                return;
            }

            if (Settings.PlayerTeams == HumansVsNations)
            {
                for (int i = 0; i < humans.Count; i++)
                    AddPlayer(humans[i], HumansTeam);

                for (int i = 0; i < _nations.Count; i++)
                    AddPlayer(_nations[i].Info, NationsTeam);

                return;
            }

            var all = new List<FrontWarsPlayerInfo>(humans.Count + _nations.Count);
            all.AddRange(humans);

            for (int i = 0; i < _nations.Count; i++)
                all.Add(_nations[i].Info);

            foreach ((FrontWarsPlayerInfo info, string team) in AssignTeams(all, _playerTeams))
                AddPlayer(info, team);
        }

        /// <summary>
        /// 팀 고르기 `[소스] function $m(e,t,n){`.
        /// <para>
        /// 네이션이 아닌 쪽을 먼저, 그다음 네이션을 «첫 네이션 id 해시» 시드로 섞은 순서로 — 한 명씩 인원이 가장 적은 팀에 넣는다.
        /// 인원이 같으면 앞 팀이다 (더 적을 때만 바꾼다).
        /// </para>
        /// </summary>
        private static List<(FrontWarsPlayerInfo Info, string Team)> AssignTeams(List<FrontWarsPlayerInfo> players, IReadOnlyList<string> teams)
        {
            var nations = players.FindAll(p => p.Type == EFrontWarsPlayerType.FakeHuman);
            IReadOnlyList<FrontWarsPlayerInfo> shuffled = nations;

            if (nations.Count > 0)
                shuffled = new FrontWarsRandom(FrontWarsHash.Of(nations[0].Id)).Shuffle(nations);

            var order = players.FindAll(p => p.Type != EFrontWarsPlayerType.FakeHuman);
            order.AddRange(shuffled);

            var counts = new Dictionary<string, int>();
            var result = new List<(FrontWarsPlayerInfo, string)>(order.Count);

            foreach (FrontWarsPlayerInfo p in order)
            {
                string best = null;
                int bestCount = 0;

                foreach (string team in teams)
                {
                    counts.TryGetValue(team, out int n);

                    if (best == null || bestCount > n)
                    {
                        bestCount = n;
                        best = team;
                    }
                }

                if (best == null)
                    continue;

                counts[best] = bestCount + 1;
                result.Add((p, best));
            }

            return result;
        }

        /// <summary>
        /// 넣을 때 팀을 따로 안 주면 고른다 `[소스] maybeAssignTeam(e){`.
        /// <para>개인전 null · 봇은 봇 팀 · 사람 대 네이션은 종류로 · 나머지는 id 해시 % 팀 수.</para>
        /// </summary>
        private string MaybeAssignTeam(FrontWarsPlayerInfo info)
        {
            if (Settings.IsTeamMode == false)
                return null;

            if (info.Type == EFrontWarsPlayerType.Bot)
                return BotTeam;

            if (Settings.PlayerTeams == HumansVsNations)
                return info.Type == EFrontWarsPlayerType.FakeHuman ? NationsTeam : HumansTeam;

            return _playerTeams[(int)(FrontWarsHash.Of(info.Id) % _playerTeams.Count)];
        }

        /// <summary>팀전이면 [봇 팀, 플레이어 팀…], 개인전이면 빈 목록 `[소스] teams(){`.</summary>
        public List<string> Teams()
        {
            var result = new List<string>();

            if (Settings.IsTeamMode)
            {
                result.Add(BotTeam);
                result.AddRange(_playerTeams);
            }

            return result;
        }

        public IReadOnlyList<FrontWarsNation> Nations
        {
            get { return _nations; }
        }

        /// <summary>
        /// 스폰 단계인가 — 틱 ≤ 스폰 턴 수 `[소스] inSpawnPhase(){return this._ticks<=`.
        /// <para>⚠ «이하» 다. 스폰 턴 수가 100 이면 0~100 틱 101번이 스폰 단계다.</para>
        /// </summary>
        public bool InSpawnPhase
        {
            get { return Ticks <= Settings.SpawnPhaseTurns; }
        }

        // ── 틱

        /// <summary>
        /// 한 틱 `[소스] executeNextTick(){`.
        /// <list type="number">
        /// <item>자동 스폰 (스폰 끝 10틱 전 · 무작위 스폰이면 2틱에 사람만)</item>
        /// <item>시작한 실행들 tick — 스폰 단계면 스폰 단계용만</item>
        /// <item>시작 안 한 실행들 init — 스폰 단계면 스폰 단계용만, 나머지는 대기로 남긴다</item>
        /// <item>끝난 실행 정리 → 방금 init 한 것을 뒤에 붙인다</item>
        /// <item>10틱마다 해시 → 틱 +1</item>
        /// </list>
        /// </summary>
        public void ExecuteNextTick()
        {
            TileUpdates.Clear();

            if (Ticks == Settings.SpawnPhaseTurns - AutoSpawnBeforeEnd)
                ScheduleAutoSpawns(null);
            else if (Ticks == RandomSpawnAutoTick && Settings.RandomSpawn)
                ScheduleAutoSpawns(EFrontWarsPlayerType.Human);

            // 원본 forEach 는 시작 때 길이까지만 돈다. 도중에 목록이 통째로 바뀌어도(removeExecution) 옛 목록을 끝까지 돈다
            List<IFrontWarsExecution> running = _execs;
            int runningCount = running.Count;
            bool spawnPhase = InSpawnPhase;

            for (int i = 0; i < runningCount; i++)
            {
                IFrontWarsExecution e = running[i];
                if ((spawnPhase == false || e.ActiveDuringSpawnPhase) && e.IsActive)
                    e.Tick(Ticks);
            }

            var initialized = new List<IFrontWarsExecution>();
            var waiting = new List<IFrontWarsExecution>();
            List<IFrontWarsExecution> pending = _unInitExecs;
            int pendingCount = pending.Count;
            spawnPhase = InSpawnPhase;

            // ⚠ init 도중 새로 넣은 실행은 원본에서 사라진다 (forEach 범위 밖 + 목록 교체). 같은 판을 내려고 그대로 둔다
            for (int i = 0; i < pendingCount; i++)
            {
                IFrontWarsExecution e = pending[i];
                if (spawnPhase == false || e.ActiveDuringSpawnPhase)
                {
                    e.Init(this, Ticks);
                    initialized.Add(e);
                }
                else
                {
                    waiting.Add(e);
                }
            }

            RemoveInactiveExecutions();
            _execs.AddRange(initialized);
            _unInitExecs = waiting;

            if (Ticks % HashInterval == 0)
            {
                LastHash = Hash();
                LastHashTick = Ticks;
                OnHash?.Invoke(Ticks, LastHash);
            }

            Ticks++;
        }

        /// <summary>스폰 단계엔 스폰 단계용 실행만 활성 여부로 거르고 나머지는 둔다 `[소스] removeInactiveExecutions(){`.</summary>
        private void RemoveInactiveExecutions()
        {
            var kept = new List<IFrontWarsExecution>(_execs.Count);
            bool spawnPhase = InSpawnPhase;

            foreach (IFrontWarsExecution e in _execs)
            {
                if (spawnPhase && e.ActiveDuringSpawnPhase == false)
                    kept.Add(e);
                else if (e.IsActive)
                    kept.Add(e);
            }

            _execs = kept;
        }

        /// <summary>1 + 플레이어 해시 합 (죽은 플레이어 포함 · 넣은 순서) `[소스] hash(){let e=1`.</summary>
        public double Hash()
        {
            double h = 1;

            for (int i = 0; i < _players.Count; i++)
                h += _players[i].Hash();

            return h;
        }

        // ── 실행

        /// <summary>시작 안 한 목록 끝에 넣는다. 다음 틱 끝에 init 된다.</summary>
        public void AddExecution(IFrontWarsExecution execution)
        {
            _unInitExecs.Add(execution);
        }

        public void AddExecutions(IEnumerable<IFrontWarsExecution> executions)
        {
            _unInitExecs.AddRange(executions);
        }

        public void RemoveExecution(IFrontWarsExecution execution)
        {
            _execs = _execs.FindAll(e => e != execution);
            _unInitExecs = _unInitExecs.FindAll(e => e != execution);
        }

        public int ExecutionCount
        {
            get { return _execs.Count + _unInitExecs.Count; }
        }

        private bool HasPendingSpawnExecution(string playerId)
        {
            foreach (IFrontWarsExecution e in _execs)
            {
                if (e is FrontWarsSpawnExecution s && s.PlayerId == playerId)
                    return true;
            }

            foreach (IFrontWarsExecution e in _unInitExecs)
            {
                if (e is FrontWarsSpawnExecution s && s.PlayerId == playerId)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 아직 안 나온 사람 · 네이션을 자동으로 내려놓는다 `[소스] scheduleAutoSpawns(e){`.
        /// <para>실행을 목록에 넣지 않고 그 자리에서 init · tick 한다.</para>
        /// </summary>
        private void ScheduleAutoSpawns(EFrontWarsPlayerType? onlyType)
        {
            var targets = new List<FrontWarsPlayer>();

            foreach (FrontWarsPlayer p in _players)
            {
                if (p.Type == EFrontWarsPlayerType.Bot)
                    continue;
                if (onlyType.HasValue && p.Type != onlyType.Value)
                    continue;
                if (p.HasSpawned || HasPendingSpawnExecution(p.Id))
                    continue;

                targets.Add(p);
            }

            if (targets.Count == 0)
                return;

            int separation = Settings.RandomSpawn ? RandomSpawnSeparation : 0;
            var taken = new List<int>();

            foreach (FrontWarsPlayer p in targets)
            {
                int tile = FindAutoSpawnTile(p, taken, separation);
                if (tile < 0)
                    continue;

                taken.Add(tile);
                var spawn = new FrontWarsSpawnExecution(p.Info, tile);
                spawn.Init(this, Ticks);
                spawn.Tick(Ticks);
            }
        }

        /// <summary>
        /// 플레이어 id 해시 시드로 칸을 뽑는다 — 거리 조건이 있으면 그 조건으로 50번, 없이 50번 `[소스] findAutoSpawnTile(e,t,n){`.
        /// </summary>
        private int FindAutoSpawnTile(FrontWarsPlayer player, List<int> taken, int separation)
        {
            var random = new FrontWarsRandom(FrontWarsHash.Of(player.Id));
            int[] passes = separation > 0 ? new[] { separation, 0 } : new[] { 0 };

            foreach (int minDistance in passes)
            {
                for (int attempt = 0; attempt < AutoSpawnAttempts; attempt++)
                {
                    int x = random.NextInt(0, Map.Width);
                    int y = random.NextInt(0, Map.Height);

                    if (Map.IsValidCoord(x, y) == false)
                        continue;

                    int tile = Map.Ref(x, y);
                    if (Map.IsLand(tile) == false || Map.IsImpassable(tile) || HasOwner(tile))
                        continue;

                    if (minDistance > 0 && taken.Exists(t => Map.ManhattanDistance(t, tile) < minDistance))
                        continue;

                    return tile;
                }
            }

            return -1;
        }

        // ── 플레이어

        /// <summary>작은 id 는 1 부터 넣은 순서 `[소스] addPlayer(e,t=null){`.</summary>
        public FrontWarsPlayer AddPlayer(FrontWarsPlayerInfo info, string team = null)
        {
            if (_nextPlayerId > MaxSmallId)
                throw new InvalidOperationException($"Player ID {_nextPlayerId} exceeds maximum value {MaxSmallId}");

            var player = new FrontWarsPlayer(this, _nextPlayerId, info, Rules.StartManpower(info), team ?? MaybeAssignTeam(info));
            long gold = Rules.StartingGold();

            if (gold > 0 && info.Type != EFrontWarsPlayerType.Bot)
                player.AddGold(gold);

            _playersBySmallId.Add(player);
            _nextPlayerId++;
            _playersById[info.Id] = player;

            // 원본 Map.set — 같은 id 가 다시 오면 자리를 유지하고 값만 바꾼다
            int existing = _players.FindIndex(p => p.Id == info.Id);
            if (existing >= 0)
                _players[existing] = player;
            else
                _players.Add(player);

            return player;
        }

        public bool HasPlayer(string id)
        {
            return _playersById.ContainsKey(id);
        }

        public FrontWarsPlayer Player(string id)
        {
            if (_playersById.TryGetValue(id, out FrontWarsPlayer p) == false)
                throw new KeyNotFoundException($"Player with id {id} not found");

            return p;
        }

        /// <summary>0 = 주인 없는 땅 → null.</summary>
        public FrontWarsPlayer PlayerBySmallId(int smallId)
        {
            return smallId == 0 ? null : _playersBySmallId[smallId - 1];
        }

        public FrontWarsPlayer PlayerByClientId(string clientId)
        {
            foreach (FrontWarsPlayer p in _players)
            {
                if (p.Info.ClientId == clientId)
                    return p;
            }

            return null;
        }

        /// <summary>살아 있는 플레이어 (넣은 순서).</summary>
        public List<FrontWarsPlayer> Players()
        {
            return _players.FindAll(p => p.IsAlive);
        }

        public IReadOnlyList<FrontWarsPlayer> AllPlayers
        {
            get { return _players; }
        }

        /// <summary>진 쪽 골드를 이긴 쪽에 넘긴다 `[소스] conquerPlayer(e,t){`.</summary>
        public void ConquerPlayer(FrontWarsPlayer conqueror, FrontWarsPlayer conquered)
        {
            long gold = conquered.Gold;
            conqueror.AddGold(gold);
            conquered.RemoveGold(gold);
            OnConquerPlayer?.Invoke(conqueror, conquered, gold);
        }

        public void SetWinner(FrontWarsPlayer winner)
        {
            Winner = winner;
            OnWinner?.Invoke(winner);
        }

        /// <summary>팀이 이겼다 `[소스] setWinner(e,t){…winnerDisplayName:typeof e==`string`?e:e.name()`.</summary>
        public void SetWinnerTeam(string team)
        {
            WinnerTeam = team;
            OnTeamWinner?.Invoke(team);
        }

        // ── 칸

        public int OwnerSmallId(int tile)
        {
            return _owner[tile];
        }

        // ── 알림 · 낙진 · 유닛 근처

        public void DisplayMessage(EFrontWarsMessage type, FrontWarsPlayer recipient, double amount = 0, string otherName = null)
        {
            OnMessage?.Invoke(new FrontWarsMessage(type, recipient, amount, otherName, Ticks));
        }

        private bool[] _fallout;

        public bool HasFallout(int tile)
        {
            return _fallout != null && _fallout[tile];
        }

        /// <summary>낙진 켜고 끄기 — 주인 있는 칸에는 켤 수 없다 `[소스] setFallout(e,t){if(t&&this.hasOwner(e))throw`.</summary>
        public void SetFallout(int tile, bool value)
        {
            if (value && HasOwner(tile))
                throw new InvalidOperationException($"cannot set fallout, tile {tile} has owner");

            SetFalloutRaw(tile, value);
        }

        /// <summary>주인 검사 없이 — 원본 map.setFallout.</summary>
        private void SetFalloutRaw(int tile, bool value)
        {
            _fallout ??= new bool[Map.TileCount];

            if (_fallout[tile] == value)
                return;

            _fallout[tile] = value;
            NumTilesWithFallout += value ? 1 : -1;
        }

        private int _nextUnitId = 1;

        public event Action<FrontWarsUnit> OnUnitUpdate;

        internal void RaiseUnitUpdate(FrontWarsUnit unit) => OnUnitUpdate?.Invoke(unit);

        /// <summary>유닛 id 는 1 부터 `[source] nextUnitID(){`.</summary>
        public int NextUnitId()
        {
            return _nextUnitId++;
        }

private int _unitCountCacheTick = -1;
        private readonly Dictionary<EFrontWarsUnit, int> _unitCountCache = new Dictionary<EFrontWarsUnit, int>();

        /// <summary>
        /// 모든 플레이어의 그 종류 레벨 합 (죽은 플레이어 포함) `[소스] unitCount(e){this.unitCountCacheTick`.
        /// <para>⚠ 원본은 틱마다 한 번 센 값을 그 틱 동안 캐시한다 — 같은 틱 안에서 유닛이 늘어도 처음 센 값을 준다. 같게 한다.</para>
        /// </summary>
        public int UnitCount(EFrontWarsUnit type)
        {
            if (_unitCountCacheTick != Ticks)
            {
                _unitCountCacheTick = Ticks;
                _unitCountCache.Clear();
            }

            if (_unitCountCache.TryGetValue(type, out int cached))
                return cached;

            int n = 0;

            for (int i = 0; i < _players.Count; i++)
                n += _players[i].UnitCount(type);

            _unitCountCache[type] = n;
            return n;
        }

        /// <summary>모든 플레이어의 그 종류 유닛 (플레이어 넣은 순서) `[소스] units(...e){`.</summary>
        public List<FrontWarsUnit> AllUnits(params EFrontWarsUnit[] types)
        {
            var result = new List<FrontWarsUnit>();

            for (int i = 0; i < _players.Count; i++)
                result.AddRange(_players[i].Units(types));

            return result;
        }

        /// <summary>새 유닛을 격자에 올리고 알린다 — 만들기는 <see cref="FrontWarsPlayer.BuildUnit"/>.</summary>
        internal void RegisterUnit(FrontWarsUnit unit)
        {
            RaiseUnitUpdate(unit);
            Units.AddUnit(unit);
        }

        /// <summary>격자에서 뺀다. 기차역이 있으면 철도망에서도 뺀다 `[소스] removeUnit(e){`.</summary>
        internal void RemoveUnit(FrontWarsUnit unit)
        {
            Units.RemoveUnit(unit);

            if (unit.HasTrainStation)
                RailNetwork.RemoveStation(unit);
        }

        public FrontWarsRailNetwork RailNetwork { get; }

        public event Action<int, List<FrontWarsRailTile>> OnRailAdded;
        public event Action<int, List<int>> OnRailRemoved;
        public event Action<int, FrontWarsRailroad, FrontWarsRailroad> OnRailSplit;
        public event Action<FrontWarsChatEvent> OnChat;

        internal void RaiseRailAdded(int id, List<FrontWarsRailTile> tiles) => OnRailAdded?.Invoke(id, tiles);
        internal void RaiseRailRemoved(int id, List<int> tiles) => OnRailRemoved?.Invoke(id, tiles);
        internal void RaiseRailSplit(int id, FrontWarsRailroad head, FrontWarsRailroad tail) => OnRailSplit?.Invoke(id, head, tail);
        internal void RaiseChat(FrontWarsChatEvent chat) => OnChat?.Invoke(chat);

        /// <summary>그 플레이어의 그 종류 유닛이 반경 안에 있나 (유클리드 거리 제곱) `[소스] hasUnitNearby(e,t,n,r){`.</summary>
        public bool HasUnitNearby(int tile, int range, EFrontWarsUnit type, FrontWarsPlayer owner)
        {
            return Units.HasUnitNearby(tile, range, type, owner);
        }

        public bool HasOwner(int tile)
        {
            return _owner[tile] != 0;
        }

        public FrontWarsPlayer Owner(int tile)
        {
            return PlayerBySmallId(_owner[tile]);
        }

        /// <summary>
        /// 칸을 가져간다 `[소스] conquer(e,t){`. 물이면 멈추고, 지나갈 수 없는 산이면 조용히 무시한다.
        /// <para>순서: 옛 주인 칸 · 국경에서 빼기 → 주인 바꾸기 → 새 주인 칸에 넣기 → 자기와 4방향 국경 다시 계산.</para>
        /// </summary>
        public void Conquer(FrontWarsPlayer player, int tile)
        {
            if (Map.IsLand(tile) == false)
                throw new InvalidOperationException("cannot conquer water");

            if (Map.IsImpassable(tile))
                return;

            FrontWarsPlayer previous = Owner(tile);
            if (previous != null)
            {
                previous.LastTileChange = Ticks;
                previous.Tiles.Remove(tile);
                previous.BorderTiles.Remove(tile);
            }

            _owner[tile] = (ushort)player.SmallId;
            player.Tiles.Add(tile);
            player.LastTileChange = Ticks;
            UpdateBorders(tile);
            SetFalloutRaw(tile, false);
            TileUpdates.Add(tile);
        }

        /// <summary>칸을 주인 없게 만든다 `[소스] relinquish(e){`.</summary>
        public void Relinquish(int tile)
        {
            if (HasOwner(tile) == false)
                throw new InvalidOperationException("Cannot relinquish tile because it is unowned");

            if (Map.IsWater(tile))
                throw new InvalidOperationException("Cannot relinquish water");

            FrontWarsPlayer owner = Owner(tile);
            owner.LastTileChange = Ticks;
            owner.Tiles.Remove(tile);
            owner.BorderTiles.Remove(tile);
            _owner[tile] = 0;
            UpdateBorders(tile);
            TileUpdates.Add(tile);
        }

        private void UpdateBorders(int tile)
        {
            UpdateBorderForTile(tile);

            int n = Map.NeighborsInto(tile, _updateBordersScratch);
            for (int i = 0; i < n; i++)
                UpdateBorderForTile(_updateBordersScratch[i]);
        }

        private void UpdateBorderForTile(int tile)
        {
            int id = _owner[tile];
            if (id == 0)
                return;

            FrontWarsPlayer p = _playersBySmallId[id - 1];
            if (CalcIsBorder(tile, id))
                p.BorderTiles.Add(tile);
            else
                p.BorderTiles.Remove(tile);
        }

        /// <summary>4방향에 주인이 다른 칸(물 포함 — 물은 주인 0)이 있으면 국경.</summary>
        private bool CalcIsBorder(int tile, int ownerId)
        {
            if (ownerId == 0)
                return false;

            int n = Map.NeighborsInto(tile, _calcIsBorderScratch);
            for (int i = 0; i < n; i++)
            {
                if (_owner[_calcIsBorderScratch[i]] != ownerId)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 조건에 맞는 칸을 4방향으로 넓힌다 `[소스] bfs(e,t){`. 스택이라 마지막에 넣은 칸부터 편다.
        /// <para>결과는 찾은 순서다 (원본 Set 순서).</para>
        /// </summary>
        public List<int> Bfs(int start, Func<int, bool> include)
        {
            var found = new List<int>();
            var seen = new HashSet<int>();
            var stack = new List<int>();
            var scratch = new int[4];

            if (include(start))
            {
                seen.Add(start);
                found.Add(start);
                stack.Add(start);
            }

            while (stack.Count > 0)
            {
                int tile = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);

                int n = Map.NeighborsInto(tile, scratch);
                for (int i = 0; i < n; i++)
                {
                    int t = scratch[i];
                    if (seen.Contains(t) == false && include(t))
                    {
                        seen.Add(t);
                        found.Add(t);
                        stack.Add(t);
                    }
                }
            }

            return found;
        }
    }
}
