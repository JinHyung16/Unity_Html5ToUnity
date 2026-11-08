using System;
using System.Collections.Generic;
using JinHyung.Data;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 네이션 AI 실행 `[소스] Cq3gr8XO.js:gp=class e{`. 스폰 단계에도 돈다.
    ///
    /// <para>
    /// 난수 소비 순서가 원본과 같아야 스폰 칸 · 공격 주기 · 행동이 같다:
    /// 생성 ① 발동 비율 ② 보유 비율 ③ 확장 비율 ④ 성격 → init ⑤ 공격 주기 ⑥ 공격 틱 → 도우미 생성 ⑦ 핵 도우미 「수소만」 → 이후 틱마다 도우미들이 같은 난수를 쓴다.
    /// </para>
    ///
    /// <list type="number">
    /// <item>(쉬움 아님 · 전함 허용 · 항구를 지은 적 있음 · 살아 있음) 이고 틱이 공격 틱과 짝홀이 같으면 전함 도우미가 배를 추적 · 보복한다 — 스폰 단계 판정보다 먼저.</item>
    /// <item>스폰 단계: 공격 틱마다 자리를 골라 스폰한다.</item>
    /// <item>플레이어를 찾고, 죽었으면 멈춘다. 도우미가 없으면 7개를 만들고 「첫 공격 대기」 를 켠 뒤 이번 틱은 끝.</item>
    /// <item>공격 틱이 아니면: 전함 틱 · 핵 틱 · MIRV 틱 · 건물 틱(주기의 1/3 · 2/3) 에 각 도우미.</item>
    /// <item>공격 틱: 첫 공격이면 빈 땅에 병력 절반. 아니면 이모지 → 금수 관계 → 동맹 요청 → 연장 → 건물 → 금수 정책 → 공격.</item>
    /// </list>
    /// </summary>
    public sealed class FrontWarsNationExecution : IFrontWarsExecution
    {
        /// <summary>스폰 기준 칸 둘레 ±25 에서 뽑는다 `[소스] nextInt(t.x-25,t.x+25)`.</summary>
        private const int SpawnJitter = 25;

        private const int RandomLandAttempts = 50;

        /// <summary>산이면 1/2 확률로 다시 뽑는다 `[소스] ae.Mountain&&this.random.chance(2)`.</summary>
        private const int MountainRerollChance = 2;

        private const int MinAttackRate = 10;
        private const double CompactMapAttackRateMul = 0.9;
        private const double RatioMin = 0.05;
        private const double RatioMax = 0.95;

        /// <summary>전함 추적은 2틱마다 (공격 틱과 짝홀이 같을 때) `[소스] e%2==this.attackTick%2`.</summary>
        private const int WarshipTrackPeriod = 2;

        /// <summary>건물 틱 = 주기의 1/3 · 2/3 지점 `[소스] Math.floor(this.attackRate/3) · Math.floor(this.attackRate*2/3)`.</summary>
        private const int StructurePhaseDivisor = 3;
        private const int StructureSecondPhaseNumerator = 2;

        /// <summary>남이 나에게 금수를 걸면 그 상대 관계 −20, 풀면 +20 되돌림 `[소스] e.updateRelation(t,-20) · e.updateRelation(t,20)`.</summary>
        private const double EmbargoRelationPenalty = -20;
        private const double EmbargoRelationRestore = 20;

        private readonly FrontWarsNation _nation;
        private readonly FrontWarsRandom _random;
        private readonly FrontWarsPersonalityData _personality;
        private readonly FrontWarsNationSharedState _shared;
        private readonly HashSet<string> _embargoMalusApplied = new HashSet<string>();
        private FrontWarsGame _game;
        private FrontWarsPlayer _player;
        private EFrontWarsRelation? _embargoForgiveAt;
        private bool _firstAttackPending;

        private FrontWarsNationEmojiBehavior _emojiBehavior;
        private FrontWarsNationAllianceBehavior _allianceBehavior;
        private FrontWarsNationAttackBehavior _attackBehavior;
        private FrontWarsNationStructureBehavior _structureBehavior;
        private FrontWarsNationMirvBehavior _mirvBehavior;
        private FrontWarsNationNukeBehavior _nukeBehavior;
        private FrontWarsNationWarshipBehavior _warshipBehavior;

        public double TriggerRatio { get; private set; }
        public double ReserveRatio { get; private set; }
        public double ExpandRatio { get; private set; }
        public int AttackRate { get; private set; }
        public int AttackTick { get; private set; }
        public int WarshipTick { get; private set; }
        public int NukeTick { get; private set; }
        public int MirvTick { get; private set; }

        public string PersonalityCode
        {
            get { return _personality.Code; }
        }

        public bool IsActive { get; private set; } = true;

        public bool ActiveDuringSpawnPhase
        {
            get { return true; }
        }

        /// <summary>
        /// 시드 = 해시(네이션 id) + 해시(게임 id) — 숫자로 더한 뒤 문자열 시드 `[소스] new W(P(t.playerInfo.id)+P(e))`.
        /// </summary>
        /// <param name="sharedState">
        /// 한 판의 모든 네이션이 같이 쓰는 상태 `[소스] fakeHumanExecutions(){let e=[],t=new yp;for(let n of this.mg.nations())e.push(new gp(this.gameID,n,t))`.
        /// null 이면 이 네이션 전용을 만든다 — 원본과 같은 판을 내려면 차리는 쪽이 하나를 만들어 모두에게 넘겨야 한다.
        /// </param>
        public FrontWarsNationExecution(string gameId, FrontWarsNation nation, FrontWarsGameSettings settings, FrontWarsNationSharedState sharedState = null)
        {
            _nation = nation;
            _shared = sharedState ?? new FrontWarsNationSharedState();
            _random = new FrontWarsRandom(FrontWarsHash.Of(nation.Info.Id) + FrontWarsHash.Of(gameId));

            TriggerRatio = _random.NextInt(50, 60) / 100.0;
            ReserveRatio = _random.NextInt(30, 40) / 100.0;
            ExpandRatio = _random.NextInt(10, 20) / 100.0;
            _personality = PickPersonality(_random, settings);

            TriggerRatio = Clamp(TriggerRatio + _personality.TriggerRatioDelta);
            ReserveRatio = Clamp(ReserveRatio + _personality.ReserveRatioDelta);
            ExpandRatio = Clamp(ExpandRatio + _personality.ExpandRatioDelta);
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _game = game;
            FrontWarsDifficultyData difficulty = game.Settings.DifficultyRow;

            // 공격 주기 `[소스] function tp(` — 난이도 범위 × 성격 배율, 축소 맵 ×0.9, 반올림, 최소 10
            double rate = _random.NextInt(difficulty.NationAttackRateMin, difficulty.NationAttackRateMaxExclusive) * _personality.AttackRateMul;
            if (game.Settings.CompactMap)
                rate *= CompactMapAttackRateMul;

            AttackRate = Math.Max(MinAttackRate, JsRound(rate));
            AttackTick = _random.NextInt(0, AttackRate);
            WarshipTick = (AttackTick + AttackRate / 4) % AttackRate;
            NukeTick = (AttackTick + AttackRate / 2) % AttackRate;
            MirvTick = (AttackTick + (int)Math.Floor(3 * AttackRate / 4.0)) % AttackRate;
            TriggerRatio = Clamp(TriggerRatio + difficulty.NationTriggerRatioDelta);
            _embargoForgiveAt = ParseRelation(difficulty.EmbargoForgiveAt);
        }

        public void Tick(int tick)
        {
            if (tick % WarshipTrackPeriod == AttackTick % WarshipTrackPeriod
                && _warshipBehavior != null
                && _player != null
                && _player.IsAlive
                && _game.Settings.Difficulty != EFrontWarsDifficulty.Easy
                && _game.Rules.IsUnitDisabled(EFrontWarsUnit.Warship) == false
                && _player.UnitsConstructed(EFrontWarsUnit.Port) > 0)
                _warshipBehavior.TrackShipsAndRetaliate();

            if (_game.InSpawnPhase)
            {
                if (tick % AttackRate != AttackTick)
                    return;

                int tile = RandomLand();
                if (tile >= 0)
                    _game.AddExecution(new FrontWarsSpawnExecution(_nation.Info, tile));

                return;
            }

            if (_player == null)
            {
                _player = _game.Players().Find(p => p.Id == _nation.Info.Id);
                if (_player == null)
                    return;
            }

            if (_player.IsAlive == false)
            {
                IsActive = false;
                return;
            }

            if (_allianceBehavior == null || _attackBehavior == null || _structureBehavior == null
                || _mirvBehavior == null || _nukeBehavior == null || _warshipBehavior == null)
            {
                CreateBehaviors();
                _firstAttackPending = true;
                return;
            }

            int phase = tick % AttackRate;

            if (phase != AttackTick)
            {
                if (phase == WarshipTick)
                {
                    _warshipBehavior.MaybeSpawnWarship();
                    _warshipBehavior.CounterWarshipInfestation();
                }

                if (phase == NukeTick)
                    _nukeBehavior.MaybeSendNuke();

                if (phase == MirvTick)
                    _mirvBehavior.ConsiderMirv();

                int firstStructureTick = (AttackTick + AttackRate / StructurePhaseDivisor) % AttackRate;
                int secondStructureTick = (AttackTick + AttackRate * StructureSecondPhaseNumerator / StructurePhaseDivisor) % AttackRate;

                if (phase == firstStructureTick || phase == secondStructureTick)
                    _structureBehavior.HandleStructures();

                return;
            }

            if (_firstAttackPending)
            {
                _firstAttackPending = false;
                _attackBehavior.ForceSendAttack(null);
                return;
            }

            _emojiBehavior?.MaybeSendCasualEmoji();
            UpdateRelationsFromEmbargoes();
            _allianceBehavior.HandleAllianceRequests();
            _allianceBehavior.HandleAllianceExtensionRequests();
            _structureBehavior.HandleStructures();
            HandleEmbargoesToHostileNations();
            _attackBehavior.MaybeAttack();
        }

        /// <summary>도우미 7개 — 만드는 순서가 난수 순서다 (핵 도우미만 생성 때 난수를 쓴다).</summary>
        private void CreateBehaviors()
        {
            _emojiBehavior = new FrontWarsNationEmojiBehavior(_random, _game, _player);
            _allianceBehavior = new FrontWarsNationAllianceBehavior(_random, _game, _player, _emojiBehavior);
            _attackBehavior = new FrontWarsNationAttackBehavior(_random, _game, _player, TriggerRatio, ReserveRatio, ExpandRatio, _allianceBehavior, _emojiBehavior);
            _structureBehavior = new FrontWarsNationStructureBehavior(_random, _game, _player, _personality);
            _mirvBehavior = new FrontWarsNationMirvBehavior(_random, _game, _player, _shared, _emojiBehavior);
            _nukeBehavior = new FrontWarsNationNukeBehavior(_random, _game, _player, _attackBehavior, _emojiBehavior);
            _warshipBehavior = new FrontWarsNationWarshipBehavior(_random, _game, _player, _shared, _emojiBehavior);
        }

        /// <summary>남이 나에게 금수를 걸면 한 번 −20, 풀면 +20 `[소스] updateRelationsFromEmbargos(){`.</summary>
        private void UpdateRelationsFromEmbargoes()
        {
            foreach (FrontWarsPlayer other in _game.Players())
            {
                if (other.Id == _player.Id)
                    continue;

                bool embargoed = other.HasEmbargoAgainst(_player);
                bool applied = _embargoMalusApplied.Contains(other.Id);

                if (embargoed && applied == false)
                {
                    _player.UpdateRelation(other, EmbargoRelationPenalty);
                    _embargoMalusApplied.Add(other.Id);
                }
                else if (embargoed == false && applied)
                {
                    _player.UpdateRelation(other, EmbargoRelationRestore);
                    _embargoMalusApplied.Remove(other.Id);
                }
            }
        }

        /// <summary>
        /// 금수 중인 상대 관계가 용서 문턱(난이도 표 EmbargoForgiveAt) 이상이면 풀고, 금수 없는 상대가 적대 이하면 건다 `[소스] handleEmbargoesToHostileNations(){`.
        /// <para>팀전 · 어려움/불가능이면 봇 아닌 다른 팀은 관계와 상관없이 전부 금수하고 풀지 않는다.</para>
        /// </summary>
        private void HandleEmbargoesToHostileNations()
        {
            EFrontWarsDifficulty difficulty = _game.Settings.Difficulty;
            bool hardOrImpossible = difficulty == EFrontWarsDifficulty.Hard || difficulty == EFrontWarsDifficulty.Impossible;
            bool team = _game.Settings.IsTeamMode;

            foreach (FrontWarsPlayer other in _game.Players())
            {
                if (other.Id == _player.Id)
                    continue;

                // `[소스] if(r===b.Team&&i&&e.type()!==E.Bot&&!t.isOnSameTeam(e)){n||t.addEmbargo(e,!1);continue}`
                if (team && hardOrImpossible && other.Type != EFrontWarsPlayerType.Bot && _player.IsOnSameTeam(other) == false)
                {
                    if (_player.HasEmbargoAgainst(other) == false)
                        _player.AddEmbargo(other, false);

                    continue;
                }

                if (_player.HasEmbargoAgainst(other))
                {
                    if (_embargoForgiveAt.HasValue && _player.Relation(other) >= _embargoForgiveAt.Value)
                        _player.StopEmbargo(other);
                }
                else if (_player.Relation(other) <= EFrontWarsRelation.Hostile && _player.IsOnSameTeam(other) == false)
                {
                    _player.AddEmbargo(other, false);
                }
            }
        }

        /// <summary>표의 관계 이름. 빈칸 = 없음 (풀어 주지 않음).</summary>
        private static EFrontWarsRelation? ParseRelation(string code)
        {
            if (string.IsNullOrEmpty(code))
                return null;

            if (Enum.TryParse(code, out EFrontWarsRelation relation) == false)
                throw new InvalidOperationException($"관계 이름을 모른다: {code}");

            return relation;
        }

        /// <summary>스폰 기준 칸 둘레(없으면 맵 전체)에서 빈 땅을 50번까지 뽑는다 `[소스] randomLand(){`.</summary>
        private int RandomLand()
        {
            FrontWarsTerrainMap map = _game.Map;

            for (int attempt = 0; attempt < RandomLandAttempts; attempt++)
            {
                int x = _nation.SpawnX.HasValue ? _random.NextInt(_nation.SpawnX.Value - SpawnJitter, _nation.SpawnX.Value + SpawnJitter) : _random.NextInt(0, map.Width);
                int y = _nation.SpawnY.HasValue ? _random.NextInt(_nation.SpawnY.Value - SpawnJitter, _nation.SpawnY.Value + SpawnJitter) : _random.NextInt(0, map.Height);

                if (map.IsValidCoord(x, y) == false)
                    continue;

                int tile = map.Ref(x, y);
                if (map.IsLand(tile) == false || _game.HasOwner(tile) || map.IsImpassable(tile))
                    continue;

                if (map.TerrainType(tile) == EFrontWarsTerrain.Mountain && _random.Chance(MountainRerollChance))
                    continue;

                return tile;
            }

            return -1;
        }

        /// <summary>난수 0~99 가 문턱보다 작은 첫 성격 `[소스] function ep(e){`.</summary>
        private static FrontWarsPersonalityData PickPersonality(FrontWarsRandom random, FrontWarsGameSettings settings)
        {
            int roll = random.NextInt(0, 100);

            foreach (FrontWarsPersonalityData p in settings.Personalities)
            {
                if (roll < p.PickBelow)
                    return p;
            }

            return settings.Personalities[settings.Personalities.Count - 1];
        }

        private static double Clamp(double v)
        {
            return Math.Min(RatioMax, Math.Max(RatioMin, v));
        }

        /// <summary>JS Math.round — .5 는 위로 (C# 기본은 짝수 쪽).</summary>
        private static int JsRound(double v)
        {
            return (int)Math.Floor(v + 0.5);
        }
    }
}
