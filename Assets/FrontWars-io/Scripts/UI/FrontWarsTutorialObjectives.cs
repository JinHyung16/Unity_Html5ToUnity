using System;
using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>튜토리얼 목표 단계 `[소스] phase: playing · victory · stretch · dead · done`.</summary>
    public enum EFrontWarsTutorialStage
    {
        /// <summary>본 목표 10개를 차례로.</summary>
        Objectives,

        /// <summary>본 목록을 다 끝냈다 — 튜토리얼 승리 화면 (게임 승리와 별개).</summary>
        Victory,

        /// <summary>「정복 계속하기」 뒤 추가 목표.</summary>
        Extra,

        /// <summary>추가 목표까지 끝났거나 판이 끝났다.</summary>
        Finished,

        /// <summary>스폰 단계 뒤 내가 죽었다.</summary>
        Dead
    }

    /// <summary>지도 위 표시 종류 `[소스] marker: spawn · expand · outpost · attacker`.</summary>
    public enum EFrontWarsTutorialMarker
    {
        None,
        Spawn,
        Expand,
        Outpost,
        Attacker
    }

    /// <summary>목표 하나 `[소스] var In=[{id,icon,marker,hasTip,briefing,buildGuide,spotlightId,autoCompleteTicks,progress,isComplete}`.</summary>
    public sealed class FrontWarsTutorialObjective
    {
        public string Id;
        public string Icon;
        public EFrontWarsTutorialMarker Marker;

        /// <summary>목표가 시작되면 10초 동안 도움말 카드를 띄운다.</summary>
        public bool HasTip;

        /// <summary>브리핑이 가리키는 HUD 요소 이름 (null = 브리핑 없음). 브리핑이 닫힐 때까지 완료 검사 · 자동 완료 시계가 멈춘다.</summary>
        public string BriefingTarget;

        /// <summary>건설 안내가 붙는 목표 — 고를 조각 (도시 · 방어 초소).</summary>
        public EFrontWarsUnit? BuildGuide;

        /// <summary>스포트라이트가 가리키는 HUD 요소 이름 (null = 없음).</summary>
        public string SpotlightTarget;

        /// <summary>목표가 시작된 뒤 이만큼 지나면 조건과 무관하게 완료. null = 없음.</summary>
        public int? AutoCompleteTicks;

        public Func<bool> IsDone;

        /// <summary>진행 표시 (지금 · 목표). null = 없음.</summary>
        public Func<(int Value, int Max)?> Progress;
    }

    /// <summary>
    /// 튜토리얼 HUD 목표 엔진 `[소스] QPQdQ6Sy.js:var Un=class e{`.
    /// <list type="bullet">
    /// <item>틱마다 지금 목표의 완료를 보고, 완료되면 40틱 뒤 다음으로 넘긴다 (그 사이는 「넘어가는 중」).</item>
    /// <item>브리핑이 떠 있는 동안은 완료 검사도 자동 완료 시계도 멈춘다 (시작 틱을 계속 미룬다).</item>
    /// <item>「전선을 사수하라」 가 시작될 때 공격 받은 기록 · 공격자를 지운다 — 그 뒤 처음 쳐들어온 플레이어가 공격자다.</item>
    /// <item>확장 목표는 내 이름 자리에서 나선으로 돌며 «내 땅에 붙은 빈 땅» 을 찾아 표시한다.</item>
    /// </list>
    /// </summary>
    public sealed class FrontWarsTutorialObjectives : IDisposable
    {
        /// <summary>목표 칩에 쓰는 아이콘 전부 — 이모지를 TMP 스프라이트로 구울 때 이 목록을 쓴다.</summary>
        public static readonly string[] AllIcons =
        {
            "🚩", "🗺️", "🏙️", "🪖", "💥", "🛡️", "🏰", "⚔️", "🏆", "👑", "🌏", "✅", "💪"
        };

        /// <summary>HUD 요소 이름 — 브리핑 · 스포트라이트가 가리킨다 `[소스] tutorial-control-panel · tutorial-leaderboard · tutorial-game-control · tutorial-attack-slider`.</summary>
        public const string TargetControlPanel = "ControlPanel";
        public const string TargetLeaderboard = "Leaderboard";
        public const string TargetGameControl = "TopRightPanel";
        public const string TargetAttackSlider = "AttackSlider";

        /// <summary>반군 전초기지 이름 `[소스] OUTPOST_NAME=`Rebel Outpost``.</summary>
        public const string OutpostName = "Rebel Outpost";

        private const int AdvanceDelayTicks = 40;

        private const int ExpandTiles = 300;
        private const int ArmyCityLevels = 2;
        private const int QuarterPercent = 25;
        private const int DominationPercent = 80;
        /// <summary>한 목록의 최대 목표 수 — 칩 위 점 줄 칸 수.</summary>
        public const int MaxObjectives = 10;

        private const int TopRank = 3;
        private const int RevengeConquests = 2;

        private const int ArmyAutoTicks = 900;
        private const int DefendAutoTicks = 750;
        private const int FortifyAutoTicks = 900;
        private const int RevengeAutoTicks = 1200;

        /// <summary>확장 표시를 찾는 나선 — 반지름 2~80, 한 바퀴에 max(8, 반지름×2) 점 `[소스] refreshExpandTarget`.</summary>
        private const int ExpandSearchMinRadius = 2;
        private const int ExpandSearchMaxRadius = 80;

        private readonly FrontWarsGame _game;
        private readonly Func<FrontWarsPlayer> _me;
        private readonly List<FrontWarsTutorialObjective> _main;
        private readonly List<FrontWarsTutorialObjective> _extra;

        private int _index;
        private int _activatedAt = -1;
        private int _advanceAt = -1;
        private int _conquests;
        private bool _hadIncomingAttack;
        private bool _incomingNow;
        private FrontWarsPlayer _attacker;
        private FrontWarsPlayer _outpost;

        public FrontWarsTutorialObjectives(FrontWarsGame game, Func<FrontWarsPlayer> me)
        {
            _game = game;
            _me = me;

            _main = new List<FrontWarsTutorialObjective>
            {
                new FrontWarsTutorialObjective { Id = "spawn", Icon = "🚩", Marker = EFrontWarsTutorialMarker.Spawn, HasTip = true, IsDone = () => Me.NumTilesOwned > 0 },
                new FrontWarsTutorialObjective { Id = "expand", Icon = "🗺️", Marker = EFrontWarsTutorialMarker.Expand, HasTip = true, IsDone = () => Me.NumTilesOwned >= ExpandTiles, Progress = () => (Math.Min(Me.NumTilesOwned, ExpandTiles), ExpandTiles) },
                new FrontWarsTutorialObjective { Id = "city", Icon = "🏙️", BuildGuide = EFrontWarsUnit.City, IsDone = () => Me.Units(EFrontWarsUnit.City).Count > 0 },
                new FrontWarsTutorialObjective { Id = "army", Icon = "🪖", BriefingTarget = TargetControlPanel, BuildGuide = EFrontWarsUnit.City, AutoCompleteTicks = ArmyAutoTicks, IsDone = () => CityLevels() >= ArmyCityLevels, Progress = () => (Math.Min(CityLevels(), ArmyCityLevels), ArmyCityLevels) },
                new FrontWarsTutorialObjective { Id = "conquest", Icon = "💥", Marker = EFrontWarsTutorialMarker.Outpost, HasTip = true, IsDone = () => _conquests >= 1 },
                new FrontWarsTutorialObjective { Id = "defend", Icon = "🛡️", Marker = EFrontWarsTutorialMarker.Attacker, SpotlightTarget = TargetAttackSlider, AutoCompleteTicks = DefendAutoTicks, IsDone = () => Me.IsAlive && _hadIncomingAttack && _incomingNow == false },
                new FrontWarsTutorialObjective { Id = "fortify", Icon = "🏰", BuildGuide = EFrontWarsUnit.DefensePost, AutoCompleteTicks = FortifyAutoTicks, IsDone = () => Me.Units(EFrontWarsUnit.DefensePost).Count > 0 },
                new FrontWarsTutorialObjective { Id = "revenge", Icon = "⚔️", Marker = EFrontWarsTutorialMarker.Attacker, HasTip = true, AutoCompleteTicks = RevengeAutoTicks, IsDone = () => (_attacker != null && _attacker.IsAlive == false) || _conquests >= RevengeConquests },
                new FrontWarsTutorialObjective { Id = "top3", Icon = "🏆", BriefingTarget = TargetLeaderboard, IsDone = () => Rank() <= TopRank },
                new FrontWarsTutorialObjective { Id = "quarter", Icon = "🌏", BriefingTarget = TargetGameControl, IsDone = () => LandRatio() >= QuarterPercent / 100.0, Progress = () => (Math.Min(Percent(), QuarterPercent), QuarterPercent) }
            };

            _extra = new List<FrontWarsTutorialObjective>
            {
                new FrontWarsTutorialObjective { Id = "domination", Icon = "👑", IsDone = () => LandRatio() >= DominationPercent / 100.0, Progress = () => (Math.Min(Percent(), DominationPercent), DominationPercent) }
            };

            _game.OnConquerPlayer += HandleConquer;
            _game.OnWinner += HandleWinner;
            Activate();
        }

        public void Dispose()
        {
            _game.OnConquerPlayer -= HandleConquer;
            _game.OnWinner -= HandleWinner;
        }

        public EFrontWarsTutorialStage Stage { get; private set; } = EFrontWarsTutorialStage.Objectives;

        /// <summary>목표를 끝낼 때마다 +1 — 축하 연출을 한 번씩 튼다.</summary>
        public int CelebrateSeq { get; private set; }

        /// <summary>완료 뒤 다음 목표로 넘어가기를 기다리는 중.</summary>
        public bool Celebrating => _advanceAt >= 0;

        /// <summary>브리핑이 떠 있다 — 「알겠습니다!」 또는 12초 뒤 닫힌다.</summary>
        public bool BriefingPending { get; private set; }

        /// <summary>도움말 카드를 띄울 목표 id (null = 없음). 목표가 바뀔 때만 바뀐다.</summary>
        public string TipObjectiveId { get; private set; }

        /// <summary>확장 목표의 표시 칸 (-1 = 없음).</summary>
        public int ExpandTargetTile { get; private set; } = -1;

        /// <summary>내가 공격받는 중이고 목표가 「전선을 사수하라」 다 — 화면 가장자리가 붉게 뛴다.</summary>
        public bool UnderAttack => Current?.Id == "defend" && _incomingNow;

        public IReadOnlyList<FrontWarsTutorialObjective> Objectives => Stage == EFrontWarsTutorialStage.Extra ? _extra : _main;

        public int Index => _index;

        /// <summary>본 목표 목록에서 id 의 순번 — HUD 를 단계별로 드러낼 때 쓴다 `[소스] In.findIndex(e=>e.id===…)`.</summary>
        public int MainIndexOf(string id) => _main.FindIndex(objective => objective.Id == id);

        public FrontWarsTutorialObjective Current
        {
            get
            {
                IReadOnlyList<FrontWarsTutorialObjective> list = Objectives;
                return _index < list.Count ? list[_index] : null;
            }
        }

        /// <summary>지도에 표시할 플레이어 — 전초기지 · 공격자 (죽었으면 null) `[소스] resolveMarkerPlayer`.</summary>
        public FrontWarsPlayer MarkerPlayer
        {
            get
            {
                FrontWarsTutorialObjective current = Current;

                if (current == null)
                    return null;

                if (current.Marker == EFrontWarsTutorialMarker.Outpost)
                    return _outpost != null && _outpost.IsAlive ? _outpost : null;

                if (current.Marker == EFrontWarsTutorialMarker.Attacker)
                    return _attacker != null && _attacker.IsAlive ? _attacker : null;

                return null;
            }
        }

        private FrontWarsPlayer Me => _me();

        public void Tick()
        {
            FrontWarsPlayer me = Me;

            if (Stage == EFrontWarsTutorialStage.Finished || Stage == EFrontWarsTutorialStage.Dead)
                return;

            TrackUpdates(me);

            if (me != null && me.IsAlive == false && _game.InSpawnPhase == false)
            {
                Stage = EFrontWarsTutorialStage.Dead;
                return;
            }

            if (me == null || Stage == EFrontWarsTutorialStage.Victory)
                return;

            int ticks = _game.Ticks;

            if (_advanceAt >= 0)
            {
                if (ticks >= _advanceAt)
                {
                    _advanceAt = -1;
                    Advance();
                }

                return;
            }

            FrontWarsTutorialObjective objective = Current;

            if (_activatedAt < 0)
                _activatedAt = ticks;

            if (BriefingPending)
            {
                _activatedAt = ticks;
            }
            else
            {
                bool auto = objective.AutoCompleteTicks.HasValue && ticks - _activatedAt > objective.AutoCompleteTicks.Value;

                if (objective.IsDone() || auto)
                {
                    CelebrateSeq++;
                    _advanceAt = ticks + AdvanceDelayTicks;
                    return;
                }
            }

            if (objective.Marker == EFrontWarsTutorialMarker.Expand && BriefingPending == false)
                RefreshExpandTarget(me);
            else if (objective.Marker != EFrontWarsTutorialMarker.Expand)
                ExpandTargetTile = -1;
        }

        /// <summary>브리핑 「알겠습니다!」 · 시간 끝.</summary>
        public void DismissBriefing() => BriefingPending = false;

        /// <summary>승리 화면 「정복 계속하기」.</summary>
        public void ContinueConquest()
        {
            if (Stage != EFrontWarsTutorialStage.Victory)
                return;

            Stage = EFrontWarsTutorialStage.Extra;
            _index = 0;
            Activate();
        }

        private void Advance()
        {
            if (_index + 1 >= Objectives.Count)
            {
                Stage = Stage == EFrontWarsTutorialStage.Extra ? EFrontWarsTutorialStage.Finished : EFrontWarsTutorialStage.Victory;
                return;
            }

            _index++;
            Activate();
        }

        /// <summary>목표 시작 `[소스] activateCurrent(){`.</summary>
        private void Activate()
        {
            FrontWarsTutorialObjective objective = Current;
            _activatedAt = -1;
            ExpandTargetTile = -1;

            if (objective == null)
                return;

            if (objective.Id == "defend")
            {
                _hadIncomingAttack = false;
                _attacker = null;
            }

            // PC 에는 모바일 전용 브리핑이 없다 — 가리킬 대상이 있는 브리핑만 띄운다 `[소스] function Hn(e)`
            BriefingPending = objective.BriefingTarget != null;
            TipObjectiveId = objective.HasTip ? objective.Id : null;
        }

        /// <summary>틱마다 전초기지 · 공격 받음 · 공격자를 갱신한다 `[소스] trackUpdates(e,t){`.</summary>
        private void TrackUpdates(FrontWarsPlayer me)
        {
            if (me == null)
                return;

            _incomingNow = me.IncomingAttacks.Count > 0;

            if (_incomingNow)
            {
                _hadIncomingAttack = true;

                if (_attacker == null && me.IncomingAttacks[0].Attacker != null)
                    _attacker = me.IncomingAttacks[0].Attacker;
            }

            if (_outpost == null)
            {
                foreach (FrontWarsPlayer p in _game.Players())
                {
                    if (p.Name == OutpostName)
                    {
                        _outpost = p;
                        break;
                    }
                }
            }
        }

        /// <summary>내 땅에 붙은 빈 땅 한 칸을 찾는다 — 지금 칸이 아직 맞으면 그대로 둔다 `[소스] refreshExpandTarget(e,t){`.</summary>
        private void RefreshExpandTarget(FrontWarsPlayer me)
        {
            FrontWarsTerrainMap map = _game.Map;

            if (ExpandTargetTile >= 0 && IsExpandCandidate(map, me, map.X(ExpandTargetTile), map.Y(ExpandTargetTile)))
                return;

            FrontWarsNameLocation name = FrontWarsNamePlacement.Compute(_game, me);

            if (name.X == 0 && name.Y == 0)
            {
                ExpandTargetTile = -1;
                return;
            }

            for (int radius = ExpandSearchMinRadius; radius <= ExpandSearchMaxRadius; radius++)
            {
                int steps = Math.Max(8, radius * 2);

                for (int i = 0; i < steps; i++)
                {
                    double angle = i / (double)steps * 2 * Math.PI;
                    // JS Math.round — .5 는 언제나 위로 (음수 −2.5 → −2)
                    int x = (int)Math.Floor(name.X + Math.Cos(angle) * radius + 0.5);
                    int y = (int)Math.Floor(name.Y + Math.Sin(angle) * radius + 0.5);

                    if (IsExpandCandidate(map, me, x, y))
                    {
                        ExpandTargetTile = map.Ref(x, y);
                        return;
                    }
                }
            }

            ExpandTargetTile = -1;
        }

        private bool IsExpandCandidate(FrontWarsTerrainMap map, FrontWarsPlayer me, int x, int y)
        {
            if (x < 1 || y < 1 || x >= map.Width - 1 || y >= map.Height - 1)
                return false;

            int tile = map.Ref(x, y);

            if (map.IsLand(tile) == false || _game.HasOwner(tile))
                return false;

            return _game.OwnerSmallId(map.Ref(x - 1, y)) == me.SmallId
                   || _game.OwnerSmallId(map.Ref(x + 1, y)) == me.SmallId
                   || _game.OwnerSmallId(map.Ref(x, y - 1)) == me.SmallId
                   || _game.OwnerSmallId(map.Ref(x, y + 1)) == me.SmallId;
        }

        private void HandleConquer(FrontWarsPlayer conqueror, FrontWarsPlayer conquered, long gold)
        {
            if (conqueror == Me)
                _conquests++;
        }

        private void HandleWinner(FrontWarsPlayer winner)
        {
            if (Stage != EFrontWarsTutorialStage.Dead)
                Stage = EFrontWarsTutorialStage.Finished;
        }

        private int CityLevels()
        {
            int sum = 0;

            foreach (FrontWarsUnit city in Me.Units(EFrontWarsUnit.City))
                sum += city.Level;

            return sum;
        }

        /// <summary>나보다 땅 많은 살아 있는 플레이어 수 + 1 `[소스] function Fn(e)`.</summary>
        private int Rank()
        {
            int mine = Me.NumTilesOwned;
            int above = 0;

            foreach (FrontWarsPlayer p in _game.Players())
            {
                if (p != Me && p.IsAlive && p.NumTilesOwned > mine)
                    above++;
            }

            return above + 1;
        }

        private double LandRatio()
        {
            return _game.NumLandTiles <= 0 ? 0 : Me.NumTilesOwned / (double)_game.NumLandTiles;
        }

        /// <summary>`[소스] Math.round(Pn(e)*100)` — JS 반올림(0.5 는 위로).</summary>
        private int Percent()
        {
            return (int)Math.Floor(LandRatio() * 100 + 0.5);
        }
    }
}
