using System;
using System.Collections.Generic;
using System.Linq;
using JinHyung.Data;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 네이션 건물 도우미 `[소스] Cq3gr8XO.js:fp=class{`.
    ///
    /// <list type="number">
    /// <item>이미 지었고 방어초소가 허용이면: 전선 보강을 시도하고, 전선이 위협받으면 돈을 아낀다.</item>
    /// <item>간격 대기(시작 골드 많은 판) · 저축 단계면 쉰다.</item>
    /// <item>골라서 짓기 — 첫 SAM → 붐비는 맵 첫 항구/공장 → [항구 · 공장 · SAM · 사일로] 더 원하면 → 도시.</item>
    /// </list>
    /// <para>난이도별 값은 난이도 표 SamPerCity · RandomUpgradePercent · RailScorePercent `[소스] ap= · up= · dp=`, 성격 배율은 성격 표.</para>
    /// </summary>
    public sealed class FrontWarsNationStructureBehavior
    {
        /// <summary>전선 위협 문턱 — 받는 지상 공격 병력 합 / 내 병력 `[소스] lp=.35`.</summary>
        private const double FrontThreatShare = 0.35;

        /// <summary>전선 보강 — 보통 1/2 확률 · 필요한 초소 = 보통 1, 그 밖 ceil(비율/0.4) `[소스] reinforceFront(){`.</summary>
        private const int ReinforceMediumChance = 2;
        private const double PostsPerThreatShare = 0.4;
        private const int MediumPostsNeeded = 1;

        /// <summary>전선 칸 표본 상한 · 초소 후보 수 `[소스] n.length<=128?n:Array.from(Uf(this.random,n,128)) · this.rollPostSites(o,25)`.</summary>
        private const int FrontlineSampleMax = 128;
        private const int PostSiteCount = 25;

        /// <summary>초소 후보 거리 배율 (간격 기준) · 굴리는 횟수 배수 `[소스] rollPostSites(e,t){…r*1.5…r*.75…t*6…t*4`.</summary>
        private const double PostSpacingMul = 1.5;
        private const double PostMinDistanceMul = 0.75;
        private const int PostFirstPassMul = 6;
        private const int PostSecondPassMul = 4;

        /// <summary>시작 골드 많은 판의 건설 간격 (지은 수 번째) `[소스] cp=[0,0,250,150,100]`.</summary>
        private static readonly int[] BuildPacingTicks = { 0, 0, 250, 150, 100 };

        private const long RichStartGold = 3000000;

        /// <summary>저축 단계 — 300틱 주기의 뒤 150틱 `[소스] (this.game.ticks()-this.spendPhaseStart)%300>=150`.</summary>
        private const int SpendCycleTicks = 300;
        private const int SpendPauseFromTicks = 150;

        /// <summary>도시 금지면 도시 수 기준 = max(1, floor(땅/2000)) `[소스] Math.floor(this.player.numTilesOwned()/2e3)`.</summary>
        private const int TilesPerVirtualCity = 2000;

        /// <summary>붐비는 맵 = 네이션 수 / 땅 칸 &gt; 1/7500 `[소스] this.game.nations().length/e>.00013333333333333334`.</summary>
        private const double CrowdedNationsPerLandTile = 0.00013333333333333334;

        /// <summary>도시당 비율 · 진열 가산율 `[소스] function op(e,t){case S.Port:case S.Factory:return{perCity:.75,markup:1};case S.SAMLauncher:return{perCity:ap[t],markup:.3};case S.MissileSilo:return{perCity:.2,markup:1}`.</summary>
        private const double PortFactoryPerCity = 0.75;
        private const double PortFactoryMarkup = 1;
        private const double SamMarkup = 0.3;
        private const double SiloPerCity = 0.2;
        private const double SiloMarkup = 1;
        private const double CityMarkup = 1;
        private const double DefaultMarkup = 0.1;

        /// <summary>해안이 있고 항구 허용이면 공장 ×0.33 · 사일로 최대(불가능 4 · 그 밖 3) · 사일로 0개면 비율 0.4 `[소스] wantsMore(e,t,n){`.</summary>
        private const double FactoryWithPortMul = 0.33;
        private const int SiloMaxImpossible = 4;
        private const int SiloMax = 3;
        private const double FirstSiloPerCity = 0.4;

        /// <summary>구조물 밀도가 이만큼 넘으면 새로 짓기보다 업그레이드 `[소스] this.structureDensity()>.0006666666666666666`.</summary>
        private const double UpgradeStructureDensity = 0.0006666666666666666;

        /// <summary>전쟁 자금 — 골드 배율 &gt; 1 이면 ×2 · 수소 5발 · 원자 20발 `[소스] warChestTarget(){`.</summary>
        private const long GoldMultiplierWarChestMul = 2;
        private const long HydrogenWarChestCount = 5;
        private const long AtomWarChestCount = 20;

        /// <summary>업그레이드 점수 — 덮는 SAM 마다 10 + (레벨−1)×7.5, 난수 0~4, 1/2 로 2~3등 `[소스] pickUpgrade(e){`.</summary>
        private const double SamCoverBase = 10;
        private const double SamCoverPerLevel = 7.5;
        private const int UpgradeJitterMaxExclusive = 5;
        private const int UpgradeRunnerUpChance = 2;
        private const int UpgradeRunnerUpMin = 1;
        private const int UpgradeRunnerUpMaxExclusive = 3;

        /// <summary>자리 표본 수 · SAM 덮개 반영 확률(%) `[소스] this.sampleOceanShore(25):Vf(this.random,this.game,this.player,25) · this.random.nextInt(0,100)<25`.</summary>
        private const int BuildSiteSamples = 25;
        private const int SamCoverageWeightingPercent = 25;

        private const int PercentRoll = 100;

        /// <summary>구조물 6종 `[소스] sp=[S.City,S.Port,S.Factory,S.DefensePost,S.SAMLauncher,S.MissileSilo]`.</summary>
        private static readonly EFrontWarsUnit[] StructureTypes =
        {
            EFrontWarsUnit.City, EFrontWarsUnit.Port, EFrontWarsUnit.Factory, EFrontWarsUnit.DefensePost, EFrontWarsUnit.SAMLauncher, EFrontWarsUnit.MissileSilo
        };

        /// <summary>짓기 검토 순서 `[소스] for(let t of[S.Port,S.Factory,S.SAMLauncher,S.MissileSilo])`.</summary>
        private static readonly EFrontWarsUnit[] WantedTypes =
        {
            EFrontWarsUnit.Port, EFrontWarsUnit.Factory, EFrontWarsUnit.SAMLauncher, EFrontWarsUnit.MissileSilo
        };

        private static readonly EFrontWarsUnit[] StationTypes = { EFrontWarsUnit.City, EFrontWarsUnit.Port, EFrontWarsUnit.Factory };

        /// <summary>역 묶음이 한 번도 정해지지 않은 역 (원본 undefined) — null(묶음 없음)과 따로, 하나의 묶음 키로 모인다.</summary>
        private static readonly object UndefinedClusterKey = new object();

        private readonly FrontWarsRandom _random;
        private readonly FrontWarsGame _game;
        private readonly FrontWarsPlayer _player;
        private readonly FrontWarsPersonalityData _personality;
        private readonly FrontWarsDifficultyData _row;
        private readonly List<FrontWarsAttack> _landAttackScratch = new List<FrontWarsAttack>();
        private List<(int Tile, object Cluster, double Weight)> _stationLedgerCache;
        private int? _lastBuildTick;
        private int _buildsDone;
        private bool? _richStart;
        private int? _spendPhaseStart;

        public FrontWarsNationStructureBehavior(FrontWarsRandom random, FrontWarsGame game, FrontWarsPlayer player, FrontWarsPersonalityData personality)
        {
            _random = random;
            _game = game;
            _player = player;
            _personality = personality;
            _row = game.Settings.DifficultyRow;
        }

        private EFrontWarsDifficulty Difficulty
        {
            get { return _game.Settings.Difficulty; }
        }

        private bool IsDisabled(EFrontWarsUnit type)
        {
            return _game.Rules.IsUnitDisabled(type);
        }

        /// <summary>건물 검토 — 지었으면 참 `[소스] handleStructures(){`.</summary>
        public bool HandleStructures()
        {
            if (_buildsDone > 0 && IsDisabled(EFrontWarsUnit.DefensePost) == false)
            {
                if (ReinforceFront())
                    return true;

                if (FrontUnderThreat())
                    return false;
            }

            if (PacingActive() || InSpendPhase())
                return false;

            bool built = PickAndBuild();

            if (built)
            {
                _lastBuildTick = _game.Ticks;
                _buildsDone++;
            }

            return built;
        }

        /// <summary>배로 내린 공격이 아닌 들어오는 공격 `[소스] landAttacksIn(e){`.</summary>
        private List<FrontWarsAttack> LandAttacksIn(List<FrontWarsAttack> into)
        {
            into.Clear();

            foreach (FrontWarsAttack attack in _player.IncomingAttacks)
            {
                if (attack.SourceTile < 0)
                    into.Add(attack);
            }

            return into;
        }

        private bool FrontUnderThreat()
        {
            if (Difficulty == EFrontWarsDifficulty.Easy)
                return false;

            List<FrontWarsAttack> attacks = LandAttacksIn(_landAttackScratch);
            if (attacks.Count == 0)
                return false;

            double troops = _player.Troops;
            if (troops <= 0)
                return false;

            double incoming = 0;
            foreach (FrontWarsAttack attack in attacks)
                incoming += attack.Troops;

            return incoming / troops >= FrontThreatShare;
        }

        private bool ReinforceFront()
        {
            EFrontWarsDifficulty difficulty = Difficulty;

            if (difficulty == EFrontWarsDifficulty.Easy || (difficulty == EFrontWarsDifficulty.Medium && _random.Chance(ReinforceMediumChance) == false))
                return false;

            List<FrontWarsAttack> attacks = LandAttacksIn(_landAttackScratch);
            if (attacks.Count == 0)
                return false;

            double troops = _player.Troops;
            if (troops <= 0)
                return false;

            double incoming = 0;
            foreach (FrontWarsAttack attack in attacks)
                incoming += attack.Troops;

            double ratio = incoming / troops;
            if (ratio < FrontThreatShare)
                return false;

            int needed = difficulty == EFrontWarsDifficulty.Medium ? MediumPostsNeeded : (int)Math.Ceiling(ratio / PostsPerThreatShare);
            List<int> front = Frontline(attacks);

            if (PostsAlongFront(front, needed) >= needed || _player.Gold < Cost(EFrontWarsUnit.DefensePost))
                return false;

            foreach (int site in RollPostSites(front, PostSiteCount))
            {
                if (FrontWarsNationTiles.IsTruthyTile(_player.CanBuild(EFrontWarsUnit.DefensePost, site)))
                {
                    _game.AddExecution(new FrontWarsConstructionExecution(_player, EFrontWarsUnit.DefensePost, site));
                    return true;
                }
            }

            return false;
        }

        /// <summary>공격자 땅에 붙은 내 국경 칸 (128개 넘으면 비복원 표본) `[소스] frontline(e){`.</summary>
        private List<int> Frontline(List<FrontWarsAttack> attacks)
        {
            var attackers = new HashSet<FrontWarsPlayer>();
            foreach (FrontWarsAttack attack in attacks)
                attackers.Add(attack.Attacker);

            var front = new List<int>();
            if (attackers.Count == 0)
                return front;

            FrontWarsTerrainMap map = _game.Map;
            int[] scratch = new int[4];

            foreach (int border in _player.BorderTiles.Items)
            {
                int n = map.NeighborsInto(border, scratch);

                for (int k = 0; k < n; k++)
                {
                    FrontWarsPlayer owner = _game.Owner(scratch[k]);

                    if (owner != null && attackers.Contains(owner))
                    {
                        front.Add(border);
                        break;
                    }
                }
            }

            return front.Count <= FrontlineSampleMax ? front : FrontWarsNationTiles.SampleWithoutReplacement(_random, front, FrontlineSampleMax);
        }

        private double PostClearanceSquared
        {
            get
            {
                double reach = Spacing * PostSpacingMul;
                return reach * reach;
            }
        }

        /// <summary>전선 칸 어디든 간격×1.5 안에 있는 내 초소 수 (필요한 수에 닿으면 멈춤) `[소스] postsAlongFront(e,t){`.</summary>
        private int PostsAlongFront(List<int> front, int needed)
        {
            if (front.Count == 0)
                return 0;

            double clearance = PostClearanceSquared;
            FrontWarsTerrainMap map = _game.Map;
            int count = 0;

            foreach (FrontWarsUnit post in _player.Units(EFrontWarsUnit.DefensePost))
            {
                foreach (int tile in front)
                {
                    if (map.EuclideanDistanceSquared(post.Tile, tile) > clearance)
                        continue;

                    count++;
                    if (count >= needed)
                        return count;

                    break;
                }
            }

            return count;
        }

        /// <summary>
        /// 초소 후보 `[소스] rollPostSites(e,t){`.
        /// <para>기존 초소와 떨어진 전선 칸(없으면 전선 전체)에서 난수 칸을 — 1차: 전선에서 맨해튼 간격×0.75~1.5 이고 지을 수 있는 내 칸, 2차(1차가 비면): 내 칸 아무거나.</para>
        /// </summary>
        private List<int> RollPostSites(List<int> front, int count)
        {
            var sites = new List<int>();
            if (front.Count == 0)
                return sites;

            FrontWarsTerrainMap map = _game.Map;
            int spacing = Spacing;
            int reach = (int)Math.Ceiling(spacing * PostSpacingMul);
            int minDistance = (int)Math.Ceiling(spacing * PostMinDistanceMul);
            int maxDistance = (int)Math.Ceiling(spacing * PostSpacingMul);
            double clearance = PostClearanceSquared;

            List<int> pool = front;
            List<FrontWarsUnit> posts = _player.Units(EFrontWarsUnit.DefensePost);

            if (posts.Count > 0)
            {
                var clear = new List<int>();

                foreach (int tile in front)
                {
                    bool near = false;

                    foreach (FrontWarsUnit post in posts)
                    {
                        if (map.EuclideanDistanceSquared(tile, post.Tile) < clearance)
                        {
                            near = true;
                            break;
                        }
                    }

                    if (near == false)
                        clear.Add(tile);
                }

                if (clear.Count > 0)
                    pool = clear;
            }

            for (int attempt = 0; attempt < count * PostFirstPassMul && sites.Count < count; attempt++)
            {
                if (TryRollNearFront(map, pool, reach, out int tile) == false)
                    continue;

                double d = MinManhattan(map, front, tile);
                if (d < minDistance || d > maxDistance)
                    continue;

                if (FrontWarsNationTiles.IsTruthyTile(_player.CanBuild(EFrontWarsUnit.DefensePost, tile)))
                    sites.Add(tile);
            }

            if (sites.Count > 0)
                return sites;

            for (int attempt = 0; attempt < count * PostSecondPassMul && sites.Count < count; attempt++)
            {
                if (TryRollNearFront(map, pool, reach, out int tile))
                    sites.Add(tile);
            }

            return sites;
        }

        /// <summary>표본 칸 하나를 골라 ±reach 난수 칸 — 맵 안이고 내 칸이면 참.</summary>
        private bool TryRollNearFront(FrontWarsTerrainMap map, List<int> pool, int reach, out int tile)
        {
            int anchor = _random.RandElement(pool);
            int x = _random.NextInt(map.X(anchor) - reach, map.X(anchor) + reach + 1);
            int y = _random.NextInt(map.Y(anchor) - reach, map.Y(anchor) + reach + 1);
            tile = FrontWarsNationTiles.NoTile;

            if (map.IsValidCoord(x, y) == false)
                return false;

            tile = map.Ref(x, y);
            return _game.OwnerSmallId(tile) == _player.SmallId;
        }

        private static double MinManhattan(FrontWarsTerrainMap map, List<int> tiles, int tile)
        {
            double best = double.PositiveInfinity;

            foreach (int t in tiles)
            {
                int d = map.ManhattanDistance(t, tile);
                if (d < best)
                    best = d;
            }

            return best;
        }

        /// <summary>시작 골드가 많은 판에서만 — 간격표[지은 수] (즉시 건설이면 절반) 만큼 안 지났으면 대기 `[소스] pacingActive(){`.</summary>
        private bool PacingActive()
        {
            if (_lastBuildTick.HasValue == false || IsRichStart() == false)
                return false;

            int wait = _buildsDone < BuildPacingTicks.Length ? BuildPacingTicks[_buildsDone] : 0;
            if (wait == 0)
                return false;

            if (_game.Settings.InstantBuild)
                wait /= 2;

            return _game.Ticks - _lastBuildTick.Value < wait;
        }

        /// <summary>골드가 전쟁 자금에 처음 닿은 틱부터 300틱 주기의 뒤 150틱은 짓지 않는다 `[소스] inSpendPhase(){`.</summary>
        private bool InSpendPhase()
        {
            if (IsDisabled(EFrontWarsUnit.MissileSilo))
                return false;

            if (_spendPhaseStart.HasValue == false)
            {
                if (_player.Gold < WarChestTarget())
                    return false;

                _spendPhaseStart = _game.Ticks;
            }

            return (_game.Ticks - _spendPhaseStart.Value) % SpendCycleTicks >= SpendPauseFromTicks;
        }

        private bool IsRichStart()
        {
            _richStart ??= _game.Rules.StartingGold() >= RichStartGold;
            return _richStart.Value;
        }

        /// <summary>골라서 짓기 `[소스] pickAndBuild(){`.</summary>
        private bool PickAndBuild()
        {
            _stationLedgerCache = null;

            bool citiesDisabled = IsDisabled(EFrontWarsUnit.City);
            int cityBasis = citiesDisabled ? Math.Max(1, _player.NumTilesOwned / TilesPerVirtualCity) : _player.UnitsOwned(EFrontWarsUnit.City);
            bool coast = HasCoast();
            bool silosAllowed = IsDisabled(EFrontWarsUnit.MissileSilo) == false;
            bool hardOrImpossible = Difficulty == EFrontWarsDifficulty.Hard || Difficulty == EFrontWarsDifficulty.Impossible;

            if (_buildsDone == 0 && hardOrImpossible && IsDisabled(EFrontWarsUnit.AtomBomb) == false && silosAllowed
                && IsDisabled(EFrontWarsUnit.SAMLauncher) == false && IsRichStart() && TryBuild(EFrontWarsUnit.SAMLauncher))
                return true;

            if (citiesDisabled == false && _player.UnitsOwned(EFrontWarsUnit.City) == 0 && IsCrowdedMap())
            {
                EFrontWarsUnit first = coast && IsDisabled(EFrontWarsUnit.Port) == false ? EFrontWarsUnit.Port : EFrontWarsUnit.Factory;

                if (IsDisabled(first) == false && TryBuild(first))
                    return true;
            }

            bool nukesAllowed = IsDisabled(EFrontWarsUnit.AtomBomb) == false || IsDisabled(EFrontWarsUnit.HydrogenBomb) == false || IsDisabled(EFrontWarsUnit.MIRV) == false;

            foreach (EFrontWarsUnit type in WantedTypes)
            {
                if (IsDisabled(type))
                    continue;

                if (type == EFrontWarsUnit.Port && coast == false)
                    continue;

                if ((type == EFrontWarsUnit.MissileSilo || type == EFrontWarsUnit.SAMLauncher) && nukesAllowed == false)
                    continue;

                if (type == EFrontWarsUnit.SAMLauncher && silosAllowed == false)
                    continue;

                if (WantsMore(type, cityBasis, coast) && TryBuild(type))
                    return true;
            }

            return citiesDisabled == false && TryBuild(EFrontWarsUnit.City);
        }

        private bool HasCoast()
        {
            foreach (int tile in _player.BorderTiles.Items)
            {
                if (_game.Map.IsOceanShore(tile))
                    return true;
            }

            return false;
        }

        private bool IsCrowdedMap()
        {
            int land = _game.NumLandTiles;
            return land > 0 && _game.Nations.Count / (double)land > CrowdedNationsPerLandTile;
        }

        private bool TryGetPerCity(EFrontWarsUnit type, out double perCity, out double markup)
        {
            switch (type)
            {
                case EFrontWarsUnit.Port:
                case EFrontWarsUnit.Factory:
                    perCity = PortFactoryPerCity;
                    markup = PortFactoryMarkup;
                    return true;
                case EFrontWarsUnit.SAMLauncher:
                    perCity = _row.SamPerCity;
                    markup = SamMarkup;
                    return true;
                case EFrontWarsUnit.MissileSilo:
                    perCity = SiloPerCity;
                    markup = SiloMarkup;
                    return true;
                default:
                    perCity = 0;
                    markup = 0;
                    return false;
            }
        }

        /// <summary>보유 수 &lt; floor(도시 수 × 도시당 비율 × 성격 배율) `[소스] wantsMore(e,t,n){`.</summary>
        private bool WantsMore(EFrontWarsUnit type, int cityBasis, bool coast)
        {
            if (TryGetPerCity(type, out double ratio, out _) == false)
                return false;

            if (type == EFrontWarsUnit.Port || type == EFrontWarsUnit.Factory)
                ratio *= _personality.EconomyRatioMul;
            else if (type == EFrontWarsUnit.SAMLauncher)
                ratio *= _personality.SamRatioMul;

            if (type == EFrontWarsUnit.Factory && coast && IsDisabled(EFrontWarsUnit.Port) == false)
                ratio *= FactoryWithPortMul;

            int owned = _player.UnitsOwned(type);

            if (type == EFrontWarsUnit.MissileSilo)
            {
                if (owned >= (Difficulty == EFrontWarsDifficulty.Impossible ? SiloMaxImpossible : SiloMax))
                    return false;

                if (owned == 0)
                    ratio = FirstSiloPerCity;
            }

            return owned < Math.Floor(cityBasis * ratio);
        }

        /// <summary>
        /// 진열 가격을 낼 수 있으면 — 밀도가 높고 업그레이드 가능한 종류면 업그레이드(가졌으면 새로 안 지음), 아니면 자리를 골라 짓는다 `[소스] tryBuild(e){`.
        /// </summary>
        private bool TryBuild(EFrontWarsUnit type)
        {
            if (_player.Gold < ShelfPrice(type))
                return false;

            List<FrontWarsUnit> owned = _player.Units(type);

            if (StructureDensity() > UpgradeStructureDensity && _game.Settings.Unit(type).Upgradable)
            {
                if (TryUpgrade(owned))
                    return true;

                if (owned.Count > 0)
                    return false;
            }

            int site = PickBuildSite(type);

            // 원본 `canBuild(e,n)===!1` — 엄격 비교
            if (site < 0 || _player.CanBuild(type, site) < 0)
                return false;

            _game.AddExecution(new FrontWarsConstructionExecution(_player, type, site));
            return true;
        }

        /// <summary>전쟁 자금에 못 미치면 비용 × (1 + 가산율 × 보유 수) 올림 · 이상이면 비용 `[소스] shelfPrice(e){`.</summary>
        private long ShelfPrice(EFrontWarsUnit type)
        {
            long cost = Cost(type);
            long chest = WarChestTarget();

            if (chest == 0 || _player.Gold >= chest)
                return cost;

            double markup = type == EFrontWarsUnit.City ? CityMarkup : TryGetPerCity(type, out _, out double m) ? m : DefaultMarkup;
            int owned = _player.UnitsOwned(type);
            return (long)Math.Ceiling((double)cost * (1 + markup * owned));
        }

        private long WarChestTarget()
        {
            if (IsDisabled(EFrontWarsUnit.MissileSilo))
                return Cost(EFrontWarsUnit.SAMLauncher);

            // 팀전은 수소폭탄 한 발 값만 모은다 `[소스] if(e.gameConfig().gameMode===b.Team)return this.cost(S.HydrogenBomb)`
            if (_game.Settings.IsTeamMode)
                return Cost(EFrontWarsUnit.HydrogenBomb);

            long mul = (_game.Settings.GoldMultiplier ?? 1) > 1 ? GoldMultiplierWarChestMul : 1;

            if (IsDisabled(EFrontWarsUnit.MIRV) == false)
                return (Cost(EFrontWarsUnit.MIRV) + Cost(EFrontWarsUnit.HydrogenBomb)) * mul;

            if (IsDisabled(EFrontWarsUnit.HydrogenBomb) == false)
                return Cost(EFrontWarsUnit.HydrogenBomb) * HydrogenWarChestCount * mul;

            if (IsDisabled(EFrontWarsUnit.AtomBomb) == false)
                return Cost(EFrontWarsUnit.AtomBomb) * AtomWarChestCount * mul;

            return Cost(EFrontWarsUnit.SAMLauncher);
        }

        private double StructureDensity()
        {
            int tiles = _player.NumTilesOwned;
            return tiles <= 0 ? 0 : _player.Units(StructureTypes).Count / (double)tiles;
        }

        private bool TryUpgrade(List<FrontWarsUnit> units)
        {
            if (units.Count == 0)
                return false;

            FrontWarsUnit pick = PickUpgrade(units);
            if (pick == null)
                return false;

            _game.AddExecution(new FrontWarsUpgradeStructureExecution(_player, pick.Id));
            return true;
        }

        /// <summary>
        /// 업그레이드할 것 `[소스] pickUpgrade(e){`. 무작위 업그레이드 확률이면 아무거나.
        /// 아니면 SAM 덮개 점수 + 난수 0~4 내림차순, 2개 이상이면 1/2 로 2~3등 중 하나.
        /// </summary>
        private FrontWarsUnit PickUpgrade(List<FrontWarsUnit> units)
        {
            var upgradable = new List<FrontWarsUnit>();

            foreach (FrontWarsUnit unit in units)
            {
                if (_player.CanUpgradeUnit(unit.Type))
                    upgradable.Add(unit);
            }

            if (upgradable.Count == 0)
                return null;

            if (_random.NextInt(0, PercentRoll) < _row.RandomUpgradePercent)
                return _random.RandElement(upgradable);

            List<FrontWarsUnit> sams = _player.Units(EFrontWarsUnit.SAMLauncher);
            FrontWarsTerrainMap map = _game.Map;
            var scored = new List<(FrontWarsUnit Unit, double Score)>(upgradable.Count);

            foreach (FrontWarsUnit unit in upgradable)
            {
                double score = 0;

                foreach (FrontWarsUnit sam in sams)
                {
                    int range = _game.Rules.SamRange(sam.Level);
                    if (map.EuclideanDistanceSquared(unit.Tile, sam.Tile) <= range * range)
                        score += SamCoverBase + (sam.Level - 1) * SamCoverPerLevel;
                }

                score += _random.NextInt(0, UpgradeJitterMaxExclusive);
                scored.Add((unit, score));
            }

            List<(FrontWarsUnit Unit, double Score)> sorted = scored.OrderByDescending(s => s.Score).ToList();

            if (sorted.Count >= 2 && _random.Chance(UpgradeRunnerUpChance))
                return sorted[sorted.Count >= 3 ? _random.NextInt(UpgradeRunnerUpMin, UpgradeRunnerUpMaxExclusive) : 1].Unit;

            return sorted[0].Unit;
        }

        /// <summary>
        /// 자리 표본(항구는 바다 해안 국경 비복원 25개, 그 밖은 땅 표본 25번)에 점수를 매겨 지을 수 있는 칸 중 가장 높은 칸 (같으면 먼저) `[소스] pickBuildSite(e){`.
        /// <para>⚠ 순서: 표본(난수) → 점수 설정(난수) → 점수. 첫 후보는 점수와 상관없이 지을 수 있으면 잡는다.</para>
        /// </summary>
        private int PickBuildSite(EFrontWarsUnit type)
        {
            List<int> samples = type == EFrontWarsUnit.Port
                ? SampleOceanShore(BuildSiteSamples)
                : FrontWarsNationTiles.SampleOwnedTiles(_random, _game, _player, BuildSiteSamples);

            if (samples.Count == 0)
                return FrontWarsNationTiles.NoTile;

            FrontWarsNationSiteScorer scorer = CreateSiteScorer(type);
            bool found = false;
            int best = FrontWarsNationTiles.NoTile;
            double bestScore = 0;

            foreach (int tile in samples)
            {
                double score = scorer.Score(tile);

                if (score <= bestScore && found)
                    continue;

                // 원본 `canBuild(e,a)&&(…)` — 칸 0 은 거짓
                if (FrontWarsNationTiles.IsTruthyTile(_player.CanBuild(type, tile)))
                {
                    found = true;
                    best = tile;
                    bestScore = score;
                }
            }

            return best;
        }

        /// <summary>종류별 점수 설정 — 도시 · 공장은 짝 종류 칸과 철도 점수, SAM 은 레벨 가중 · 덮개 반영 `[소스] siteScoring(e){`.</summary>
        private FrontWarsNationSiteScorer CreateSiteScorer(EFrontWarsUnit type)
        {
            switch (type)
            {
                case EFrontWarsUnit.City:
                {
                    List<int> factories = TilesOf(EFrontWarsUnit.Factory);
                    return new FrontWarsNationSiteScorer(_game, _player, type, factories, RailBonusScorer(), false, false);
                }
                case EFrontWarsUnit.Factory:
                {
                    List<int> cities = TilesOf(EFrontWarsUnit.City);
                    return new FrontWarsNationSiteScorer(_game, _player, type, cities, RailBonusScorer(), false, false);
                }
                case EFrontWarsUnit.SAMLauncher:
                {
                    bool levelWeighted = Difficulty == EFrontWarsDifficulty.Hard || Difficulty == EFrontWarsDifficulty.Impossible;
                    bool coverage = Difficulty != EFrontWarsDifficulty.Easy && _random.NextInt(0, PercentRoll) < SamCoverageWeightingPercent;
                    return new FrontWarsNationSiteScorer(_game, _player, type, null, null, levelWeighted, coverage);
                }
                default:
                    return new FrontWarsNationSiteScorer(_game, _player, type, null, null, false, false);
            }
        }

        private List<int> TilesOf(EFrontWarsUnit type)
        {
            var tiles = new List<int>();

            foreach (FrontWarsUnit unit in _player.Units(type))
                tiles.Add(unit.Tile);

            return tiles;
        }

        /// <summary>
        /// 철도 점수 — 확률표(RailScorePercent)에 걸리고 역 장부가 비지 않았을 때만 `[소스] railBonusScorer(){`.
        /// <para>난수는 늘 한 번 굴린다 (확률 0 인 쉬움도).</para>
        /// </summary>
        private Func<int, double> RailBonusScorer()
        {
            if (_random.NextInt(0, PercentRoll) >= _row.RailScorePercent)
                return null;

            _stationLedgerCache ??= BuildStationLedger();
            List<(int Tile, object Cluster, double Weight)> ledger = _stationLedgerCache;

            if (ledger.Count == 0)
                return null;

            int minRange = _game.Settings.Config.TrainStationMinRange;
            int maxRange = _game.Settings.Config.TrainStationMaxRange;
            int minSquared = minRange * minRange;
            int maxSquared = maxRange * maxRange;

            return tile => RailScore(tile, ledger, minSquared, maxSquared);
        }

        private double TrainGoldFor(EFrontWarsRelationKind kind)
        {
            return _game.Settings.EconomyMode == EFrontWarsEconomyMode.Fast ? _game.Rules.TrainGoldFast(kind) : _game.Rules.TrainGold(kind);
        }

        /// <summary>
        /// 역 장부 — 내 역, 그다음 무역 가능한 사람 · 네이션 이웃의 역. 가중치 = 그 관계 기차 골드 / 동맹 기차 골드 `[소스] buildStationLedger(){`.
        /// </summary>
        private List<(int Tile, object Cluster, double Weight)> BuildStationLedger()
        {
            var ledger = new List<(int Tile, object Cluster, double Weight)>();
            double allyGold = Math.Max(TrainGoldFor(EFrontWarsRelationKind.Ally), 1);

            AddStations(ledger, _player, TrainGoldFor(EFrontWarsRelationKind.Self) / allyGold);

            foreach (FrontWarsPlayer neighbor in _player.Neighbors())
            {
                if (neighbor == null || neighbor.Type == EFrontWarsPlayerType.Bot || _player.CanTrade(neighbor) == false)
                    continue;

                EFrontWarsRelationKind kind = EFrontWarsRelationKind.Other;

                if (_player.IsOnSameTeam(neighbor))
                    kind = EFrontWarsRelationKind.Team;
                else if (_player.IsAlliedWith(neighbor))
                    kind = EFrontWarsRelationKind.Ally;

                AddStations(ledger, neighbor, TrainGoldFor(kind) / allyGold);
            }

            return ledger;
        }

        private void AddStations(List<(int Tile, object Cluster, double Weight)> ledger, FrontWarsPlayer owner, double weight)
        {
            foreach (FrontWarsUnit unit in owner.Units(StationTypes))
            {
                FrontWarsTrainStation station = _game.RailNetwork.FindStation(unit);
                if (station == null)
                    continue;

                object cluster = station.ClusterUndefined ? UndefinedClusterKey : station.Cluster;
                ledger.Add((unit.Tile, cluster, weight));
            }
        }

        /// <summary>
        /// 후보 칸에서 유클리드 역 최소~최대 거리 안의 역 — 묶음 없는 역은 전부 더하고, 같은 묶음은 가장 큰 가중치 하나만 (묶음 처음 나온 순서로 더함) `[소스] railScore(e,t,n,r){`.
        /// </summary>
        private double RailScore(int tile, List<(int Tile, object Cluster, double Weight)> ledger, int minSquared, int maxSquared)
        {
            FrontWarsTerrainMap map = _game.Map;
            var clusterIndex = new Dictionary<object, int>();
            var clusterWeights = new List<double>();
            double score = 0;

            foreach (var entry in ledger)
            {
                int d = map.EuclideanDistanceSquared(tile, entry.Tile);
                if (d < minSquared || d > maxSquared)
                    continue;

                if (entry.Cluster == null)
                {
                    score += entry.Weight;
                    continue;
                }

                bool known = clusterIndex.TryGetValue(entry.Cluster, out int index);
                double current = known ? clusterWeights[index] : 0;

                if (entry.Weight > current)
                {
                    if (known)
                    {
                        clusterWeights[index] = entry.Weight;
                    }
                    else
                    {
                        clusterIndex[entry.Cluster] = clusterWeights.Count;
                        clusterWeights.Add(entry.Weight);
                    }
                }
            }

            foreach (double w in clusterWeights)
                score += w;

            return score;
        }

        private List<int> SampleOceanShore(int count)
        {
            var shores = new List<int>();

            foreach (int tile in _player.BorderTiles.Items)
            {
                if (_game.Map.IsOceanShore(tile))
                    shores.Add(tile);
            }

            return FrontWarsNationTiles.SampleWithoutReplacement(_random, shores, count);
        }

        /// <summary>초소 간격 = 원자폭탄 outer 반경 `[소스] spacing(){return this.game.config().nukeMagnitudes(S.AtomBomb).outer}`.</summary>
        private int Spacing
        {
            get { return FrontWarsNationTiles.NukeOuterRadius(EFrontWarsUnit.AtomBomb); }
        }

        private long Cost(EFrontWarsUnit type)
        {
            return _game.Rules.UnitCost(type, _player);
        }
    }
}
