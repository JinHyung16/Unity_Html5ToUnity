using System;
using System.Collections.Generic;
using System.Linq;
using JinHyung.Data;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 네이션 핵 도우미 `[소스] Cq3gr8XO.js:Jf=class e{`.
    ///
    /// <list type="number">
    /// <item>목표: 결투 상대 → 나를 가장 크게 치는 쪽 → 밀집 사냥 → 압도적 1위 → 동맹의 적 → 가장 미운 상대 → 독주 1위 (처음 나오는 쪽).</item>
    /// <item>탄두: 수소(실효 비용) → 원자(수소만 성격이 아니거나 심하게 공격받는 중).</item>
    /// <item>조준: 목표 땅 표본 + 목표 구조물 칸 중 폭발이 목표에 맞는 칸의 값이 높은 순. 어려움 · 불가능은 적 SAM 이 요격 못 하는 앞 후보.</item>
    /// <item>쏠 때마다 그 탄두의 「인지 비용」 이 오른다 — 다음 판단에서 더 비싸게 본다.</item>
    /// </list>
    /// <para>만들 때 난수 한 번(수소만 1/3)을 쓴다. 팀 모드 분기(선두 팀 · 팀원 핵 겹침)는 싱글 FFA 라 옮기지 않았다.</para>
    /// </summary>
    public sealed class FrontWarsNationNukeBehavior
    {
        /// <summary>수소만 쓰는 성격 1/3 `[소스] this.hydroOnly=this.random.chance(3)`.</summary>
        private const int HydrogenOnlyChance = 3;

        /// <summary>밀집 사냥 — 1/2 · 구조물 레벨 합 ≥ 5 · 레벨/땅 최소 1/75 `[소스] densityHuntTarget(){…t=.013333333333333334…i<5`.</summary>
        private const int DensityHuntChance = 2;
        private const int DensityMinStructureLevels = 5;
        private const double DensityMinRatio = 0.013333333333333334;

        /// <summary>압도적 1위 = (땅 − 낙진)의 50% 초과 `[소스] dominantCrown(){…>.5`.</summary>
        private const double DominantLandShare = 0.5;

        /// <summary>내 최대 병력이 그 최대 병력×2 이상이면 미워도 안 쏜다 `[소스] mostHatedPeer(){…e>=this.game.config().maxTroops(n)*2`.</summary>
        private const double MostHatedMaxTroopsMul = 2;

        /// <summary>최근 핵 기록 유지 틱 `[소스] pruneRecentNukes(){let e=this.game.ticks()-600`.</summary>
        private const int RecentNukeTicks = 600;

        /// <summary>방패 사일로 업그레이드 상한 레벨 `[소스] static{this.SILO_LEVEL_CAP=5}`.</summary>
        private const int SiloLevelCap = 5;

        /// <summary>조준 값 `[소스] aimValue(e,t,n,r){`.</summary>
        private const int MediumSamAvoidRangeSquared = 2500;
        private const double ShoreBonus = 15000;
        private const double ShoreProbeBonus = 5000;
        private const double WeakSamBonusPerLevel = 100000;
        private const int WeakSamMaxLevel = 5;
        private const double SiloDistanceFloorShare = 0.2;
        private const double SiloDistancePenalty = 30;
        private const double RecentNukePenalty = 1e6;

        /// <summary>
        /// 쏠 때마다 인지 비용에 곱하는 % `[소스] Kf={[S.AtomBomb]:150n,[S.HydrogenBomb]:125n}`.
        /// <para>표 FrontWarsNukeTable.AiCostMarkupPercent 와 같은 값 — 시뮬 설정이 그 표를 들고 있지 않아 상수로 둔다.</para>
        /// </summary>
        private const long AtomCostMarkupPercent = 150;
        private const long HydrogenCostMarkupPercent = 125;
        private const long PercentDivisor = 100;

        /// <summary>
        /// 구조물 조준 가중치 — 순서도 원본 키 순서 `[소스] Wf={[S.MissileSilo]:5e4,[S.City]:25e3,[S.SAMLauncher]:2e4,[S.Port]:15e3,[S.Factory]:15e3,[S.DefensePost]:5e3},Gf=Object.keys(Wf)`.
        /// </summary>
        private static readonly EFrontWarsUnit[] AimStructureTypes =
        {
            EFrontWarsUnit.MissileSilo, EFrontWarsUnit.City, EFrontWarsUnit.SAMLauncher, EFrontWarsUnit.Port, EFrontWarsUnit.Factory, EFrontWarsUnit.DefensePost
        };

        private static readonly double[] AimStructureWeights = { 50000, 25000, 20000, 15000, 15000, 5000 };

        /// <summary>8방향 탐침 — 순서도 원본 그대로 `[소스] qf=[[-1,-1],[0,-1],[1,-1],[-1,0],[1,0],[-1,1],[0,1],[1,1]]`.</summary>
        private static readonly int[] ProbeDx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        private static readonly int[] ProbeDy = { -1, -1, -1, 0, 0, 1, 1, 1 };

        private readonly FrontWarsRandom _random;
        private readonly FrontWarsGame _game;
        private readonly FrontWarsPlayer _player;
        private readonly FrontWarsNationAttackBehavior _attack;
        private readonly FrontWarsNationEmojiBehavior _emoji;
        private readonly FrontWarsDifficultyData _row;
        private readonly List<(int Tick, int Tile, int InnerRadius)> _recentNukes = new List<(int, int, int)>();
        private readonly bool _hydrogenOnly;
        private long _perceivedAtomCost;
        private long _perceivedHydrogenCost;

        public FrontWarsNationNukeBehavior(FrontWarsRandom random, FrontWarsGame game, FrontWarsPlayer player, FrontWarsNationAttackBehavior attack, FrontWarsNationEmojiBehavior emoji)
        {
            _random = random;
            _game = game;
            _player = player;
            _attack = attack;
            _emoji = emoji;
            _row = game.Settings.DifficultyRow;
            _perceivedAtomCost = Cost(EFrontWarsUnit.AtomBomb);
            _perceivedHydrogenCost = Cost(EFrontWarsUnit.HydrogenBomb);
            _hydrogenOnly = _random.Chance(HydrogenOnlyChance);
        }

        private EFrontWarsDifficulty Difficulty
        {
            get { return _game.Settings.Difficulty; }
        }

        private bool IsHardOrImpossible
        {
            get { return Difficulty == EFrontWarsDifficulty.Hard || Difficulty == EFrontWarsDifficulty.Impossible; }
        }

        /// <summary>핵 틱마다 `[소스] maybeSendNuke(){`.</summary>
        public void MaybeSendNuke()
        {
            List<FrontWarsUnit> silos = _player.Units(EFrontWarsUnit.MissileSilo);

            if (silos.Count == 0
                || _game.Rules.IsUnitDisabled(EFrontWarsUnit.MissileSilo)
                || (_game.Rules.IsUnitDisabled(EFrontWarsUnit.AtomBomb) && _game.Rules.IsUnitDisabled(EFrontWarsUnit.HydrogenBomb)))
                return;

            FrontWarsPlayer target = FindBestNukeTarget();

            if (target == null || target.Type == EFrontWarsPlayerType.Bot || _player.IsOnSameTeam(target) || _attack.ShouldAttack(target) == false)
                return;

            EFrontWarsUnit? warhead = ChooseWarhead();
            if (warhead.HasValue == false)
                return;

            PruneRecentNukes();
            (int Tile, int Spawn, double Value)? aim = ChooseAim(target, warhead.Value, silos);

            if (aim.HasValue && (aim.Value.Value > 0 || Difficulty != EFrontWarsDifficulty.Impossible))
                Launch(aim.Value.Tile, warhead.Value, target, 0);
            else if (Difficulty == EFrontWarsDifficulty.Impossible)
                OverwhelmSamCover(target);
        }

        // ── 목표

        /// <summary>앞에서부터 처음 나오는 대상 — 뒤 단계는 앞이 없을 때만 계산한다 (난수 포함) `[소스] findBestNukeTarget(){`.</summary>
        private FrontWarsPlayer FindBestNukeTarget()
        {
            return DuelOpponent()
                   ?? _attack.FindIncomingAttackPlayer()
                   ?? DensityHuntTarget()
                   ?? DominantCrown()
                   ?? AlliesEnemy()
                   ?? MostHatedPeer()
                   ?? RunawayCrown()
                   ?? LeadingTeamTarget();
        }

        /// <summary>
        /// 팀전 — 내 팀 말고 칸 합이 가장 큰 팀(봇 팀도 셈 · 같으면 먼저 나온 팀)의 비우호 플레이어 `[소스] leadingTeamTarget(){`.
        /// <para>1/2 로 그중 최대 병력이 가장 큰 사람(같으면 먼저), 아니면 그중 아무나.</para>
        /// </summary>
        private FrontWarsPlayer LeadingTeamTarget()
        {
            if (_game.Settings.IsTeamMode == false)
                return null;

            var order = new List<string>();
            var tiles = new Dictionary<string, int>();

            foreach (FrontWarsPlayer p in _game.Players())
            {
                if (p.Team == null)
                    continue;

                if (tiles.TryGetValue(p.Team, out int sum) == false)
                    order.Add(p.Team);

                tiles[p.Team] = sum + p.NumTilesOwned;
            }

            string leading = null;
            int most = -1;

            foreach (string team in order)
            {
                if (team != _player.Team && tiles[team] > most)
                {
                    most = tiles[team];
                    leading = team;
                }
            }

            if (leading == null)
                return null;

            FrontWarsPlayer strongest = null;
            List<FrontWarsPlayer> members = null;

            foreach (FrontWarsPlayer p in _game.Players())
            {
                if (p.Team != leading || _player.IsFriendly(p))
                    continue;

                (members ??= new List<FrontWarsPlayer>()).Add(p);

                if (strongest == null || _game.Rules.MaxTroops(p) > _game.Rules.MaxTroops(strongest))
                    strongest = p;
            }

            if (members == null)
                return null;

            return _random.Chance(LeadingTeamStrongestChance) ? strongest : _random.RandElement(members);
        }

        /// <summary>선두 팀에서 가장 센 사람을 고를 확률 1/2 `[소스] this.random.chance(2)?i:this.random.randElement(a)`.</summary>
        private const int LeadingTeamStrongestChance = 2;

        private FrontWarsPlayer DuelOpponent()
        {
            if (IsHardOrImpossible == false)
                return null;

            List<FrontWarsPlayer> players = _game.Players();

            if (players.Count != 2)
                return null;

            return players[0] == _player ? players[1] : players[0];
        }

        private FrontWarsPlayer DensityHuntTarget()
        {
            if (Difficulty != EFrontWarsDifficulty.Impossible || _random.Chance(DensityHuntChance) == false || IsRichestNation() == false)
                return null;

            FrontWarsPlayer best = null;
            double bestRatio = DensityMinRatio;

            foreach (FrontWarsPlayer p in _game.Players())
            {
                if (p == _player || p.Type == EFrontWarsPlayerType.Bot || _player.IsFriendly(p))
                    continue;

                int tiles = p.NumTilesOwned;
                if (tiles == 0)
                    continue;

                int levels = 0;
                foreach (FrontWarsUnit unit in p.Units(AimStructureTypes))
                    levels += unit.Level;

                if (levels < DensityMinStructureLevels)
                    continue;

                double ratio = levels / (double)tiles;
                if (ratio > bestRatio)
                {
                    bestRatio = ratio;
                    best = p;
                }
            }

            return best;
        }

        private bool IsRichestNation()
        {
            long gold = _player.Gold;

            foreach (FrontWarsPlayer p in _game.Players())
            {
                if (p != _player && p.Type == EFrontWarsPlayerType.FakeHuman && p.Gold > gold)
                    return false;
            }

            return true;
        }

        private FrontWarsPlayer DominantCrown()
        {
            // 개인전만 `[소스] if(e!==d.Impossible||t!==b.FFA)return null`
            if (Difficulty != EFrontWarsDifficulty.Impossible || _game.Settings.IsTeamMode)
                return null;

            double land = _game.NumLandTiles - _game.NumTilesWithFallout;
            if (land <= 0)
                return null;

            FrontWarsPlayer largest = LargestPlayer(null);

            if (largest == null || largest == _player || _player.IsFriendly(largest))
                return null;

            return largest.NumTilesOwned / land > DominantLandShare ? largest : null;
        }

        private FrontWarsPlayer AlliesEnemy()
        {
            foreach (FrontWarsPlayer ally in _player.Allies())
            {
                if (_player.Relation(ally) < EFrontWarsRelation.Friendly)
                    continue;

                foreach (FrontWarsPlayer target in ally.Targets())
                {
                    if (target == _player || _player.IsFriendly(target))
                        continue;

                    return target;
                }
            }

            return null;
        }

        private FrontWarsPlayer MostHatedPeer()
        {
            double myMax = _game.Rules.MaxTroops(_player);

            foreach (var (other, relation) in _player.AllRelationsSorted())
            {
                if (relation != EFrontWarsRelation.Hostile)
                    continue;

                if (_player.IsFriendly(other) == false && (myMax >= _game.Rules.MaxTroops(other) * MostHatedMaxTroopsMul) == false)
                    return other;
            }

            return null;
        }

        /// <summary>
        /// 독주 1위 `[소스] runawayCrown(){`. 불가능이고 내가 1위면 2위(우호 아니면). 아니면 (1위 땅 − 내 땅)/(땅 − 낙진) &gt; 난이도 표 RunawayCrownShare.
        /// </summary>
        private FrontWarsPlayer RunawayCrown()
        {
            // 개인전만 `[소스] if(t!==b.FFA||this.game.players().length<=1)return null`
            if (_game.Settings.IsTeamMode || _game.Players().Count <= 1)
                return null;

            FrontWarsPlayer largest = LargestPlayer(null);
            if (largest == null)
                return null;

            if (Difficulty == EFrontWarsDifficulty.Impossible && largest == _player)
            {
                FrontWarsPlayer second = LargestPlayer(_player);
                return second != null && _player.IsFriendly(second) == false ? second : null;
            }

            if (largest == _player || _player.IsFriendly(largest))
                return null;

            double land = _game.NumLandTiles - _game.NumTilesWithFallout;
            if (land <= 0)
                return null;

            return (largest.NumTilesOwned - _player.NumTilesOwned) / land > _row.RunawayCrownShare ? largest : null;
        }

        /// <summary>땅이 가장 큰 살아 있는 플레이어 (같으면 먼저) — exclude 는 뺀다 `[소스] largestPlayer(e){`.</summary>
        private FrontWarsPlayer LargestPlayer(FrontWarsPlayer exclude)
        {
            FrontWarsPlayer largest = null;

            foreach (FrontWarsPlayer p in _game.Players())
            {
                if (p != exclude && (largest == null || p.NumTilesOwned > largest.NumTilesOwned))
                    largest = p;
            }

            return largest;
        }

        // ── 탄두 · 비용

        private EFrontWarsUnit? ChooseWarhead()
        {
            long gold = _player.Gold;

            if (_game.Rules.IsUnitDisabled(EFrontWarsUnit.HydrogenBomb) == false && gold >= EffectiveCost(EFrontWarsUnit.HydrogenBomb))
                return EFrontWarsUnit.HydrogenBomb;

            if (_game.Rules.IsUnitDisabled(EFrontWarsUnit.AtomBomb) == false
                && (_hydrogenOnly == false || IsUnderHeavyAttack())
                && gold >= EffectiveCost(EFrontWarsUnit.AtomBomb))
                return EFrontWarsUnit.AtomBomb;

            return null;
        }

        /// <summary>
        /// 둘만 남음 · MIRV 금지 · 골드 &gt; MIRV + 수소 이면 지금 비용. 어려움 · 불가능이고 심하게 공격받는 중이면 지금 비용. 아니면 인지 비용 `[소스] effectiveCost(e){`.
        /// </summary>
        private long EffectiveCost(EFrontWarsUnit type)
        {
            long cost = Cost(type);

            // 팀전은 수소 값보다 많으면 지금 비용 `[소스] t.gameConfig().gameMode===b.Team&&this.player.gold()>this.cost(S.HydrogenBomb)`
            if (_game.Players().Count == 2
                || _game.Rules.IsUnitDisabled(EFrontWarsUnit.MIRV)
                || (_game.Settings.IsTeamMode && _player.Gold > Cost(EFrontWarsUnit.HydrogenBomb))
                || _player.Gold > Cost(EFrontWarsUnit.MIRV) + Cost(EFrontWarsUnit.HydrogenBomb))
                return cost;

            if (IsHardOrImpossible && IsUnderHeavyAttack())
                return cost;

            return type == EFrontWarsUnit.AtomBomb ? _perceivedAtomCost : _perceivedHydrogenCost;
        }

        private bool IsUnderHeavyAttack()
        {
            double incoming = 0;

            foreach (FrontWarsAttack attack in _player.IncomingAttacks)
                incoming += attack.Troops;

            return incoming >= _player.Troops;
        }

        // ── 조준

        /// <summary>
        /// 후보 = 목표 땅 표본(난이도 표 NukeAimSamples) + 목표 구조물 칸 (중복 제거 · 넣은 순서) `[소스] chooseAim(e,t,n){`.
        /// <para>값 내림차순(안정). 쉬움 · 보통은 1등. 어려움 · 불가능은 앞 NukeSamAvoidTop 개 중 적 SAM 이 요격 못 하는 첫 후보.</para>
        /// </summary>
        private (int Tile, int Spawn, double Value)? ChooseAim(FrontWarsPlayer target, EFrontWarsUnit warhead, List<FrontWarsUnit> silos)
        {
            int outer = FrontWarsNationTiles.NukeOuterRadius(warhead);
            List<FrontWarsUnit> structures = target.Units(AimStructureTypes);
            List<int> sampled = FrontWarsNationTiles.SampleOwnedTiles(_random, _game, target, _row.NukeAimSamples);

            var candidates = new List<int>(sampled.Count + structures.Count);
            var seen = new HashSet<int>();

            foreach (int tile in sampled)
            {
                if (seen.Add(tile))
                    candidates.Add(tile);
            }

            foreach (FrontWarsUnit unit in structures)
            {
                if (seen.Add(unit.Tile))
                    candidates.Add(unit.Tile);
            }

            var aims = new List<(int Tile, int Spawn, double Value)>();

            foreach (int tile in candidates)
            {
                if (BlastFitsTarget(tile, outer, target) == false)
                    continue;

                // 원본 `c!==!1` — 엄격 비교
                int spawn = _player.CanBuild(warhead, tile);
                if (spawn < 0)
                    continue;

                // 팀전(쉬움 제외)은 팀원 · 동맹 핵과 겹치는 칸을 뺀다 `[소스] o&&i!==d.Easy&&this.overlapsTeammateStrike(r,t)||f.push(`
                if (_game.Settings.IsTeamMode && Difficulty != EFrontWarsDifficulty.Easy && OverlapsTeammateStrike(tile, warhead))
                    continue;

                aims.Add((tile, spawn, AimValue(tile, warhead, silos, structures)));
            }

            if (aims.Count == 0)
                return null;

            List<(int Tile, int Spawn, double Value)> sorted = aims.OrderByDescending(a => a.Value).ToList();

            if (IsHardOrImpossible == false)
                return sorted[0];

            List<(int Tile, int Range2)> sams = EnemySams();
            int top = Math.Min(sorted.Count, _row.NukeSamAvoidTop);

            for (int i = 0; i < top; i++)
            {
                var aim = sorted[i];

                if (sams.Count == 0 || SamsCanIntercept(aim.Spawn, aim.Tile, sams) == false)
                    return aim;
            }

            return null;
        }

        /// <summary>
        /// 8방향 × (outer, outer/2) 16점이 전부 목표 땅이어야 한다. 어려움 · 불가능은 주인 없는 칸도, 거기에 팀전이면 비우호 플레이어 칸도 허용 `[소스] blastFitsTarget(e,t,n){`.
        /// </summary>
        private bool BlastFitsTarget(int tile, int outer, FrontWarsPlayer target)
        {
            FrontWarsTerrainMap map = _game.Map;
            bool lenient = IsHardOrImpossible;
            bool team = _game.Settings.IsTeamMode;
            int x = map.X(tile), y = map.Y(tile);

            foreach (int radius in new[] { outer, outer >> 1 })
            {
                for (int d = 0; d < ProbeDx.Length; d++)
                {
                    int px = x + ProbeDx[d] * radius, py = y + ProbeDy[d] * radius;
                    if (map.IsValidCoord(px, py) == false)
                        continue;

                    FrontWarsPlayer owner = _game.Owner(map.Ref(px, py));

                    if (owner != target
                        && (lenient && owner == null) == false
                        && (lenient && team && owner != null && _player.IsFriendly(owner) == false) == false)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 날아가는 원자 · 수소 핵 중 나 말고 우호(팀원 · 동맹) 핵의 목표와, 두 안쪽 반경 합 이내로 겹치나 `[소스] overlapsTeammateStrike(e,t){`.
        /// </summary>
        private bool OverlapsTeammateStrike(int tile, EFrontWarsUnit warhead)
        {
            int inner = FrontWarsNationTiles.NukeInnerRadius(warhead);

            foreach (FrontWarsUnit nuke in _game.AllUnits(EFrontWarsUnit.AtomBomb, EFrontWarsUnit.HydrogenBomb))
            {
                FrontWarsPlayer owner = nuke.Owner;
                if (owner == _player || _player.IsFriendly(owner) == false)
                    continue;

                int target = nuke.TargetTile;
                if (target < 0)
                    continue;

                int reach = inner + FrontWarsNationTiles.NukeInnerRadius(nuke.Type);
                if (_game.Map.EuclideanDistanceSquared(tile, target) <= reach * reach)
                    return true;
            }

            return false;
        }

        /// <summary>조준 값 — 계산 순서가 원본과 같아야 소수 끝자리가 같다 `[소스] aimValue(e,t,n,r){`.</summary>
        private double AimValue(int tile, EFrontWarsUnit warhead, List<FrontWarsUnit> silos, List<FrontWarsUnit> structures)
        {
            FrontWarsTerrainMap map = _game.Map;
            int inner = FrontWarsNationTiles.NukeInnerRadius(warhead);
            int outer = FrontWarsNationTiles.NukeOuterRadius(warhead);
            int outerSquared = outer * outer;
            double value = 0;

            foreach (FrontWarsUnit unit in structures)
            {
                if (map.EuclideanDistanceSquared(tile, unit.Tile) > outerSquared)
                    continue;

                value += StructureWeight(unit.Type) * unit.Level;
            }

            if (Difficulty == EFrontWarsDifficulty.Medium)
            {
                foreach (FrontWarsUnit unit in structures)
                {
                    if (unit.Type == EFrontWarsUnit.SAMLauncher && map.EuclideanDistanceSquared(tile, unit.Tile) <= MediumSamAvoidRangeSquared)
                        return -1;
                }
            }

            if (IsHardOrImpossible && _game.Settings.WaterNukes)
            {
                if (map.IsShore(tile))
                    value += ShoreBonus;

                value += ShoreProbeBonus * ShoreProbeHits(tile, inner);
            }

            if (Difficulty == EFrontWarsDifficulty.Impossible && warhead == EFrontWarsUnit.HydrogenBomb)
            {
                foreach (var (sam, distSquared) in _game.Units.NearbyUnits(tile, outer, new[] { EFrontWarsUnit.SAMLauncher }))
                {
                    int level = sam.Level;
                    if (level >= WeakSamMaxLevel)
                        continue;

                    int range = _game.Rules.SamRange(level);
                    if (distSquared > range * range)
                        value += WeakSamBonusPerLevel * level;
                }
            }

            double nearestSilo = double.PositiveInfinity;

            foreach (FrontWarsUnit silo in silos)
            {
                int d = map.EuclideanDistanceSquared(tile, silo.Tile);
                if (d < nearestSilo)
                    nearestSilo = d;
            }

            if (nearestSilo < double.PositiveInfinity)
                value = Math.Max(value * SiloDistanceFloorShare, value - Math.Sqrt(nearestSilo) * SiloDistancePenalty);

            foreach (var nuke in _recentNukes)
            {
                int innerSquared = nuke.InnerRadius * nuke.InnerRadius;
                if (map.EuclideanDistanceSquared(tile, nuke.Tile) <= innerSquared)
                    value -= RecentNukePenalty;
            }

            return value;
        }

        private static double StructureWeight(EFrontWarsUnit type)
        {
            int i = Array.IndexOf(AimStructureTypes, type);
            return i >= 0 ? AimStructureWeights[i] : 0;
        }

        private int ShoreProbeHits(int tile, int inner)
        {
            FrontWarsTerrainMap map = _game.Map;
            int x = map.X(tile), y = map.Y(tile);
            int hits = 0;

            foreach (int radius in new[] { inner, inner >> 1 })
            {
                for (int d = 0; d < ProbeDx.Length; d++)
                {
                    int px = x + ProbeDx[d] * radius, py = y + ProbeDy[d] * radius;
                    if (map.IsValidCoord(px, py) && map.IsShore(map.Ref(px, py)))
                        hits++;
                }
            }

            return hits;
        }

        private List<(int Tile, int Range2)> EnemySams()
        {
            var sams = new List<(int Tile, int Range2)>();

            foreach (FrontWarsUnit sam in _game.AllUnits(EFrontWarsUnit.SAMLauncher))
            {
                FrontWarsPlayer owner = sam.Owner;
                if (owner == _player || _player.IsFriendly(owner))
                    continue;

                int range = _game.Rules.SamRange(sam.Level);
                sams.Add((sam.Tile, range * range));
            }

            return sams;
        }

        /// <summary>
        /// 출발 → 목표 포물선(핵 속도)의 i 번째 칸이 목표나 출발에서 요격 가능 거리 안이고 · SAM 사거리 안이고 ·
        /// SAM 미사일 도달 틱 ceil(맨해튼/미사일 속도) ≤ i 이면 요격 가능 `[소스] samsCanIntercept(e,t,n){`.
        /// </summary>
        private bool SamsCanIntercept(int spawn, int tile, List<(int Tile, int Range2)> sams)
        {
            IFrontWarsParabolaPath path = _game.Paths.Parabola();
            path.ComputeControlPoints(spawn, tile, _game.Settings.Config.NukeSpeed, true);
            IReadOnlyList<int> points = path.AllTiles();

            if (points.Count == 0)
                return false;

            FrontWarsTerrainMap map = _game.Map;
            int targetableRange = _game.Settings.Config.NukeTargetableRange;
            int targetableSquared = targetableRange * targetableRange;
            int missileSpeed = _game.Settings.Config.SamMissileSpeed;

            for (int i = 0; i < points.Count; i++)
            {
                int point = points[i];

                if (map.EuclideanDistanceSquared(point, tile) >= targetableSquared && map.EuclideanDistanceSquared(spawn, point) >= targetableSquared)
                    continue;

                foreach (var sam in sams)
                {
                    if (map.EuclideanDistanceSquared(sam.Tile, point) > sam.Range2)
                        continue;

                    int ticks = (int)Math.Ceiling(map.ManhattanDistance(sam.Tile, point) / (double)missileSpeed);
                    if (ticks <= i && ticks < points.Count)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// SAM 덮개 뚫기 (불가능 전용) `[소스] overwhelmSamCover(e){`.
        /// <para>목표 SAM 을 레벨 낮은 순으로 — 덮는 적 SAM 레벨 합 + 1 발을 쏠 탄 · 골드가 있으면 간격을 두고 몰아 쏜다. 탄이 모자랐던 적이 있으면 방패 사일로 업그레이드.</para>
        /// </summary>
        private void OverwhelmSamCover(FrontWarsPlayer target)
        {
            if (_game.Rules.IsUnitDisabled(EFrontWarsUnit.AtomBomb) || _player.Units(EFrontWarsUnit.AtomBomb).Count > 0)
                return;

            List<FrontWarsUnit> targetSams = target.Units(EFrontWarsUnit.SAMLauncher);
            if (targetSams.Count == 0)
                return;

            bool anyReady = false;
            int readyMissiles = 0;

            foreach (FrontWarsUnit silo in _player.Units(EFrontWarsUnit.MissileSilo))
            {
                int free = silo.Level - silo.MissileTimerQueue.Count;
                if (free > 0)
                {
                    anyReady = true;
                    readyMissiles += free;
                }
            }

            if (anyReady == false)
                return;

            long atomCost = Cost(EFrontWarsUnit.AtomBomb);
            FrontWarsTerrainMap map = _game.Map;
            bool shortOnMissiles = false;

            foreach (FrontWarsUnit targetSam in targetSams.OrderBy(s => s.Level).ToList())
            {
                int samTile = targetSam.Tile;
                int cover = 0;

                foreach (FrontWarsUnit sam in _game.AllUnits(EFrontWarsUnit.SAMLauncher))
                {
                    FrontWarsPlayer owner = sam.Owner;
                    if (owner == _player || _player.IsFriendly(owner))
                        continue;

                    int range = _game.Rules.SamRange(sam.Level);
                    if (map.EuclideanDistanceSquared(samTile, sam.Tile) <= range * range)
                        cover += sam.Level;
                }

                int needed = cover + 1;

                if (readyMissiles < needed)
                {
                    shortOnMissiles = true;
                    continue;
                }

                if (_player.Gold < atomCost * needed || _player.CanBuild(EFrontWarsUnit.AtomBomb, samTile) < 0)
                    continue;

                int spacing = Math.Max(1, (int)Math.Floor(_game.Settings.Config.SamCooldownTicks / 2.0 / needed));

                for (int k = 0; k < needed; k++)
                    Launch(samTile, EFrontWarsUnit.AtomBomb, target, k * spacing);

                return;
            }

            if (shortOnMissiles)
                UpgradeShieldedSilo();
        }

        /// <summary>레벨 &lt; 5 인 내 사일로 중 내 SAM 덮개(레벨 합)가 가장 큰 사일로를 업그레이드 `[소스] upgradeShieldedSilo(){`.</summary>
        private void UpgradeShieldedSilo()
        {
            if (_player.CanUpgradeUnit(EFrontWarsUnit.MissileSilo) == false)
                return;

            List<FrontWarsUnit> mySams = _player.Units(EFrontWarsUnit.SAMLauncher);
            FrontWarsTerrainMap map = _game.Map;
            FrontWarsUnit best = null;
            int bestCover = -1;

            foreach (FrontWarsUnit silo in _player.Units(EFrontWarsUnit.MissileSilo))
            {
                if (silo.Level >= SiloLevelCap)
                    continue;

                int cover = 0;

                foreach (FrontWarsUnit sam in mySams)
                {
                    int range = _game.Rules.SamRange(sam.Level);
                    if (map.EuclideanDistanceSquared(silo.Tile, sam.Tile) <= range * range)
                        cover += sam.Level;
                }

                if (cover > bestCover)
                {
                    bestCover = cover;
                    best = silo;
                }
            }

            if (best != null)
                _game.AddExecution(new FrontWarsUpgradeStructureExecution(_player, best.Id));
        }

        /// <summary>기록 → 인지 비용 × 가산% / 100 (정수 버림) → 핵 실행 → 핵 이모지 `[소스] launch(e,t,n,r=0){`.</summary>
        private void Launch(int tile, EFrontWarsUnit warhead, FrontWarsPlayer target, int waitTicks)
        {
            _recentNukes.Add((_game.Ticks, tile, FrontWarsNationTiles.NukeInnerRadius(warhead)));

            if (warhead == EFrontWarsUnit.AtomBomb)
                _perceivedAtomCost = _perceivedAtomCost * AtomCostMarkupPercent / PercentDivisor;
            else
                _perceivedHydrogenCost = _perceivedHydrogenCost * HydrogenCostMarkupPercent / PercentDivisor;

            _game.AddExecution(new FrontWarsNukeExecution(warhead, _player, tile, -1, -1, waitTicks));
            _emoji?.MaybeSendNukeEmoji(target);
        }

        private void PruneRecentNukes()
        {
            int cutoff = _game.Ticks - RecentNukeTicks;

            while (_recentNukes.Count > 0 && _recentNukes[0].Tick < cutoff)
                _recentNukes.RemoveAt(0);
        }

        private long Cost(EFrontWarsUnit type)
        {
            return _game.Rules.UnitCost(type, _player);
        }
    }
}
