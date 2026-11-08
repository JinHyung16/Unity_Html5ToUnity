using System;
using JinHyung.Data;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 병력 · 골드 식 `[소스] Cq3gr8XO.js:startManpower(e){ · maxTroops(e){ · troopIncreaseRate(e){ · goldAdditionRate(e){`.
    ///
    /// <para>
    /// 난이도 · 종류별로 갈리는 값(시작 병력 · 배율)은 표에서 읽고, 식의 모양을 정하는 계수(0.6 · 1000 · 50000 · 0.73 …)는 사양이라 여기 상수로 둔다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 거듭제곱은 <c>Math.Pow</c> 다. 원본 JS <c>**</c> 와 마지막 자리(1ulp)가 다를 수 있다 —
    /// 병력을 버림으로 담으니 드물게 1 차이가 날 수 있다. 원본과 틱 단위로 맞출 때 이 자리부터 본다.
    /// </para>
    /// </summary>
    public sealed class FrontWarsRules
    {
        private const double TilesExponent = 0.6;
        private const double TilesScale = 1000;
        private const double BaseCapacity = 50000;
        private const double CapacityMultiplier = 2;
        private const double BotMaxTroopsDivisor = 3;
        private const double InfiniteMaxTroops = 1e9;
        private const double InfiniteStartTroops = 1e6;
        private const int StartingTroopsScale = 10;

        private const double GrowthBase = 10;
        private const double GrowthExponent = 0.73;
        private const double GrowthDivisor = 4;
        private const double BotGrowthMul = 0.6;

        private readonly FrontWarsGameSettings _settings;

        public FrontWarsRules(FrontWarsGameSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// 시작 병력 — 봇 · 네이션(난이도 × 강도) · 무한 병력 · 설정값 × 10 · 기본 순.
        /// <para>네이션 강도는 원본 싱글에서 늘 1 이다 (플레이어 정보에 네이션이 안 실림 · <see cref="FrontWarsGameSetup"/>).</para>
        /// </summary>
        public double StartManpower(FrontWarsPlayerInfo info)
        {
            if (info.Type == EFrontWarsPlayerType.Bot)
                return _settings.Config.BotStartTroops;

            if (info.Type == EFrontWarsPlayerType.FakeHuman)
                return (double)_settings.DifficultyRow.NationStartTroops * info.NationStrength;

            if (_settings.InfiniteTroops)
                return InfiniteStartTroops;

            return _settings.StartingTroops.HasValue
                ? _settings.StartingTroops.Value * StartingTroopsScale
                : _settings.Config.HumanStartTroops;
        }

        /// <summary>2×(칸^0.6×1000 + 50000) + 도시 레벨 합 × 도시 증가량 → 봇 ÷3 · 사람 max(식, 시작설정×10) · 네이션 × 난이도.</summary>
        public double MaxTroops(FrontWarsPlayer player)
        {
            double t;

            if (player.Type == EFrontWarsPlayerType.Human && _settings.InfiniteTroops)
            {
                t = InfiniteMaxTroops;
            }
            else
            {
                int cityLevels = 0;
                foreach (FrontWarsUnit city in player.Units(EFrontWarsUnit.City))
                    cityLevels += city.Level;

                t = CapacityMultiplier * (Math.Pow(player.NumTilesOwned, TilesExponent) * TilesScale + BaseCapacity)
                    + (double)cityLevels * _settings.Config.CityTroopIncrease;
            }

            switch (player.Type)
            {
                case EFrontWarsPlayerType.Bot:
                    return t / BotMaxTroopsDivisor;
                case EFrontWarsPlayerType.Human:
                    return Math.Max(t, (double)(_settings.StartingTroops ?? 0) * StartingTroopsScale);
                default:
                    return t * _settings.DifficultyRow.NationMaxTroopsMul;
            }
        }

        /// <summary>min(병력 + (10 + 병력^0.73/4)×(1 − 병력/최대)×배율, 최대) − 병력.</summary>
        public double TroopIncreaseRate(FrontWarsPlayer player)
        {
            double max = MaxTroops(player);
            double troops = player.Troops;
            double n = GrowthBase + Math.Pow(troops, GrowthExponent) / GrowthDivisor;
            n *= 1 - troops / max;

            if (player.Type == EFrontWarsPlayerType.Bot)
                n *= BotGrowthMul;

            if (player.Type == EFrontWarsPlayerType.FakeHuman)
                n *= _settings.DifficultyRow.NationTroopIncreaseMul;

            return Math.Min(troops + n, max) - troops;
        }

        /// <summary>틱 골드 — 봇 50 · 그 밖 100, 골드 배율이 1 이 아니면 곱해서 버림.</summary>
        public long GoldAdditionRate(FrontWarsPlayer player)
        {
            long gold = player.Type == EFrontWarsPlayerType.Bot ? _settings.Config.GoldPerTickBot : _settings.Config.GoldPerTickHuman;
            return ApplyGoldMultiplier(gold);
        }

        public long ApplyGoldMultiplier(long gold)
        {
            double m = _settings.GoldMultiplier ?? 1;
            return m == 1 ? gold : (long)Math.Floor(gold * m);
        }

        // ── 공격 `[소스] attackLogic(e,t,n,r,i){ · attackTilesPerTick( · attackAmount(`

        /// <summary>대상 칸 수가 클수록 방어 · 속도 배율을 줄이는 로지스틱 — 기울기 ln2/50000 · 중심 150000.</summary>
        private static readonly double DefenderSizeSlope = Math.Log(2) / 50000;
        private const double DefenderSizeMidpoint = 150000;
        private const double DefenderSizeBase = 0.7;
        private const double DefenderSizeRange = 0.3;

        /// <summary>공격자 칸이 10만을 넘으면 손실 · 속도에 거듭제곱 감쇠.</summary>
        private const double LargeAttackerTiles = 1e5;
        private const double LargeAttackerLossExponent = 0.7;
        private const double LargeAttackerSpeedExponent = 0.6;

        private const double AttackerLossScale = 0.8;
        private const double TroopRatioMin = 0.6;
        private const double TroopRatioMax = 2;
        private const double SpeedRatioDivisor = 5;
        private const double SpeedRatioMin = 0.2;
        private const double SpeedRatioMax = 1.5;
        private const double VsBotDefenseMul = 0.8;

        /// <summary>주인 없는 땅 — 봇 손실 ÷10 · 그 밖 ÷5 · 예산 2000×max(10,속도)/병력 을 5~100 으로.</summary>
        private const double NeutralLossDivisorBot = 10;
        private const double NeutralLossDivisor = 5;
        private const double NeutralSpeedScale = 2000;
        private const double NeutralSpeedFloor = 10;
        private const double NeutralBudgetMin = 5;
        private const double NeutralBudgetMax = 100;

        /// <summary>낙진 칸 방어 배율 = 5 − 낙진 비율 × 2 `[소스] falloutDefenseModifier(e){return 5-e*2}`.</summary>
        private const double FalloutModifierBase = 5;
        private const double FalloutModifierSlope = 2;

        /// <summary>칸 예산 — 플레이어 대상 clamp(5×병력/대상병력×2, 0.01, 0.5)×(앞줄+난수)×3 · 주인 없는 땅 ×2.</summary>
        private const double BudgetRatioScale = 10;
        private const double BudgetRatioMin = 0.01;
        private const double BudgetRatioMax = 0.5;
        private const double BudgetPlayerMul = 3;
        private const double BudgetNeutralMul = 2;

        public double AttackAmount(FrontWarsPlayer attacker)
        {
            return attacker.Type == EFrontWarsPlayerType.Bot
                ? attacker.Troops / (double)_settings.Config.AttackAmountDivisorBot
                : attacker.Troops / (double)_settings.Config.AttackAmountDivisorHuman;
        }

        public double AttackTilesPerTick(double troops, FrontWarsPlayer attacker, FrontWarsPlayer defender, double borderPlusRandom)
        {
            if (defender != null)
                return Clamp(BudgetRatioScale / 2 * troops / defender.Troops * 2, BudgetRatioMin, BudgetRatioMax) * borderPlusRandom * BudgetPlayerMul;

            return borderPlusRandom * BudgetNeutralMul;
        }

        /// <param name="defender">null = 주인 없는 땅.</param>
        public FrontWarsAttackResult AttackLogic(FrontWarsGame game, double troops, FrontWarsPlayer attacker, FrontWarsPlayer defender, int tile)
        {
            FrontWarsTerrainData terrain;

            switch (game.Map.TerrainType(tile))
            {
                case EFrontWarsTerrain.Plains: terrain = _settings.Plains; break;
                case EFrontWarsTerrain.Highland: terrain = _settings.Highland; break;
                case EFrontWarsTerrain.Mountain: terrain = _settings.Mountain; break;
                default: throw new InvalidOperationException($"terrain type {game.Map.TerrainType(tile)} not supported");
            }

            double defense = terrain.DefenseValue;
            double speed = terrain.SpeedValue;

            if (defender != null && game.HasUnitNearby(tile, _settings.Config.DefensePostRange, EFrontWarsUnit.DefensePost, defender))
            {
                defense *= _settings.Config.DefensePostDefenseBonus;
                speed *= _settings.Config.DefensePostSpeedBonus;
            }

            if (game.HasFallout(tile))
            {
                double ratio = (double)game.NumTilesWithFallout / game.NumLandTiles;
                double m = FalloutModifierBase - ratio * FalloutModifierSlope;
                defense *= m;
                speed *= m;
            }

            if (defender != null)
            {
                // 끊긴 같은 팀 방어자는 방어 0 `[소스] r.isDisconnected()&&n.isOnSameTeam(r)&&(a=0)`
                if (defender.IsDisconnected && attacker.IsOnSameTeam(defender))
                    defense = 0;

                if ((attacker.Type == EFrontWarsPlayerType.Human || attacker.Type == EFrontWarsPlayerType.FakeHuman) && defender.Type == EFrontWarsPlayerType.Bot)
                    defense *= VsBotDefenseMul;

                double shrink = 1 - Logistic(defender.NumTilesOwned, DefenderSizeSlope, DefenderSizeMidpoint);
                double speedMul = DefenderSizeBase + DefenderSizeRange * shrink;
                double lossMul = DefenderSizeBase + DefenderSizeRange * shrink;

                double lossDecay = 1;
                double speedDecay = 1;

                if (attacker.NumTilesOwned > LargeAttackerTiles)
                {
                    lossDecay = Math.Pow(Math.Sqrt(LargeAttackerTiles / attacker.NumTilesOwned), LargeAttackerLossExponent);
                    speedDecay = Math.Pow(LargeAttackerTiles / attacker.NumTilesOwned, LargeAttackerSpeedExponent);
                }

                bool traitor = defender.IsTraitor;
                double defenderTroops = defender.Troops;

                return new FrontWarsAttackResult(
                    Clamp(defenderTroops / troops, TroopRatioMin, TroopRatioMax) * defense * AttackerLossScale * lossMul * lossDecay * (traitor ? _settings.Config.TraitorDefenseDebuff : 1),
                    defenderTroops / defender.NumTilesOwned,
                    Clamp(defenderTroops / (SpeedRatioDivisor * troops), SpeedRatioMin, SpeedRatioMax) * speed * speedMul * speedDecay * (traitor ? _settings.Config.TraitorSpeedDebuff : 1));
            }

            return new FrontWarsAttackResult(
                attacker.Type == EFrontWarsPlayerType.Bot ? defense / NeutralLossDivisorBot : defense / NeutralLossDivisor,
                0,
                Clamp(NeutralSpeedScale * Math.Max(NeutralSpeedFloor, speed) / troops, NeutralBudgetMin, NeutralBudgetMax));
        }

        /// <summary>clamp — 원본 At(e,t,n)=min(max(e,t),n).</summary>
        public static double Clamp(double v, double min, double max)
        {
            return Math.Min(Math.Max(v, min), max);
        }

        /// <summary>1/(1+e^(−k(x−x0))) `[소스] function Ht(e,t,n){`.</summary>
        public static double Logistic(double x, double k, double x0)
        {
            return 1 / (1 + Math.Exp(-k * (x - x0)));
        }

        // ── 건물 비용 · 공사 시간 `[소스] unitInfo(e){switch(e){ · costWrapper(e,...t){`

        /// <summary>
        /// 유닛 비용. 사람 + 무한 골드면 0. 개수 n = 표의 셈 종류마다 min(가진 수, 지은 수) 합.
        /// <list type="bullet">
        /// <item>Fixed: 기본값</item>
        /// <item>Linear: min(상한, (n+1)×기본값)</item>
        /// <item>Doubling: min(상한, 2^n × 기본값)</item>
        /// </list>
        /// </summary>
        public long UnitCost(EFrontWarsUnit type, FrontWarsPlayer player)
        {
            FrontWarsUnitData row = _settings.Unit(type);

            if (row.CostRule == "Free")
                return 0;

            if (player.Type == EFrontWarsPlayerType.Human && _settings.InfiniteGold)
                return 0;

            int n = 0;

            foreach (string code in row.CostCountCodes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var counted = (EFrontWarsUnit)Enum.Parse(typeof(EFrontWarsUnit), code);
                n += Math.Min(player.UnitsOwned(counted), player.UnitsConstructed(counted));
            }

            switch (row.CostRule)
            {
                case "Fixed":
                    return row.CostBase;
                case "Linear":
                    return (long)Math.Min(row.CostCap, (n + 1) * (double)row.CostBase);
                case "Doubling":
                    return (long)Math.Min(row.CostCap, Math.Pow(2, n) * row.CostBase);
                default:
                    throw new InvalidOperationException($"비용 규칙 «{row.CostRule}» 을 모른다 ({type})");
            }
        }

        /// <summary>공사 틱. 공사가 없는 종류는 −1. 즉시 건설이면 0.</summary>
        public int ConstructionDuration(EFrontWarsUnit type)
        {
            int ticks = _settings.Unit(type).ConstructionTicks;

            if (ticks < 0)
                return -1;

            return _settings.InstantBuild ? 0 : ticks;
        }

        // ── 무역선 · 핵 · SAM · 배 공격 `[소스] tradeShipSpawnRate( · tradeShipGold( · nukeDeathFactor( · samRange(`

        private const double TradeSpawnScale = 25;
        private const int TradeBaseFewShips = 3;
        private static readonly double TradeBaseSlope = Math.Log(2) / 10;
        private const double TradeBaseMidpoint = 55;
        private const double TradePortFactor = 0.1;
        private const int TradeFastLowShips = 20;
        private const int TradeFastHighShips = 150;
        private const double TradeFastExponent = 0.85;
        private const int TradeFastOffset = 5;
        private const int TradeFastNever = 1000000;
        private const double TradeGoldBase = 1e5;
        private const double TradeGoldPerTile = 100;
        private const double TradeGoldPortBonus = 2;
        private const double TradeGoldPortSoftener = 5;
        private const double TradeGoldFastBase = 5e4;
        private const double TradeGoldFastStep = 0.25;
        private const double TradeGoldFastDecay = 0.9;

        private const double NukeDeathScale = 5;
        private const double WarheadReserveShare = 0.03;
        private const double WarheadDeathScale = 500;
        private const double WarheadDeathSlope = 2;

        private const double SamRangeBase = 70;
        private const double SamRangeDecay = 0.7;
        private const int SamMaxLevel = 100;

        private const double ProximityPortsDivisor = 3;
        private const double ProximityPortsMin = 4;

        /// <summary>한 번 굴려 1/r 로 무역선을 띄운다. 무역선이 3척 미만이면 기본 1 `[소스] tradeShipSpawnRate(e,t,n){`.</summary>
        public int TradeShipSpawnRate(int totalTradeShips, int playerPorts, int playerTradeShips)
        {
            double baseRate = playerTradeShips < TradeBaseFewShips ? 1 : 1 - Logistic(totalTradeShips, TradeBaseSlope, TradeBaseMidpoint);
            double portMul = 1 / (1 + TradePortFactor * playerPorts);
            return (int)Math.Floor(TradeSpawnScale / Math.Sqrt(baseRate * portMul));
        }

        public int TradeShipSpawnRateFast(int totalTradeShips)
        {
            if (totalTradeShips < TradeFastLowShips)
                return TradeFastOffset;

            if (totalTradeShips <= TradeFastHighShips)
                return (int)Math.Floor(Math.Pow(totalTradeShips - TradeFastLowShips, TradeFastExponent) + TradeFastOffset);

            return TradeFastNever;
        }

        /// <summary>도착 골드 — (10만 + 100×이동 칸) × (1 + 2×(항구−1)/(항구+4)), 골드 배율 적용.</summary>
        public long TradeShipGold(int tilesTraveled, int ports)
        {
            double n = Math.Floor(TradeGoldBase + TradeGoldPerTile * tilesTraveled);
            double r = ports - 1;
            double mul = 1 + TradeGoldPortBonus * (r / (r + TradeGoldPortSoftener));
            return ApplyGoldMultiplier((long)Math.Floor(n * mul));
        }

        public long TradeShipGoldFast(int tilesTraveled, int ports)
        {
            double n = Math.Floor(TradeGoldFastBase + TradeGoldPerTile * tilesTraveled);
            double mul = 1;

            for (int i = 0; i < ports; i++)
                mul += TradeGoldFastStep * Math.Pow(TradeGoldFastDecay, i);

            return ApplyGoldMultiplier((long)Math.Floor(n * mul));
        }

        /// <summary>핵이 칸 하나에서 죽이는 병력. MIRV 탄두만 최대 병력 기준 포화 곡선.</summary>
        public double NukeDeathFactor(EFrontWarsUnit nukeType, double troops, int tilesOwned, double maxTroops)
        {
            if (nukeType != EFrontWarsUnit.MIRVWarhead)
                return NukeDeathScale * troops / Math.Max(1, tilesOwned);

            double reserve = WarheadReserveShare * maxTroops;
            double a = Math.Max(0, troops - reserve) / maxTroops;
            return WarheadDeathScale * (1 - Math.Exp(-WarheadDeathSlope * a));
        }

        /// <summary>SAM 사거리 = round(70 × (2 − 0.7^(레벨−1))).</summary>
        public int SamRange(int level)
        {
            double t = 1 - Math.Pow(SamRangeDecay, Math.Min(Math.Max(level, 1), SamMaxLevel) - 1);
            return (int)Math.Floor(SamRangeBase * (1 + t) + 0.5);
        }

        /// <summary>배 공격 기본 병력 = floor(병력/5).</summary>
        public double BoatAttackAmount(FrontWarsPlayer attacker)
        {
            return Math.Floor(attacker.Troops / (double)_settings.Config.BoatAttackDivisor);
        }

        /// <summary>가까운 항구 보너스 개수 = clamp(항구 수/3, 4, 항구 수).</summary>
        public double ProximityBonusPortsNb(int ports)
        {
            return Clamp(ports / ProximityPortsDivisor, ProximityPortsMin, ports);
        }

        // ── 기차 `[소스] trainSpawnRate( · trainGold(`

        private const int TrainSpawnBase = 10;
        private const int TrainSpawnScale = 20;
        private const double TrainSpawnFastScale = 40;
        private const double TrainSpawnFastExponent = 0.5;
        private const int TrainSpawnFastCap = 1400;

        /// <summary>기차를 1/r 로 띄운다 — r = (공장 수 + 10) × 20.</summary>
        public int TrainSpawnRate(int factories)
        {
            return (factories + TrainSpawnBase) * TrainSpawnScale;
        }

        public int TrainSpawnRateFast(int tradeStations)
        {
            return Math.Min(TrainSpawnFastCap, (int)Math.Floor(TrainSpawnFastScale * Math.Pow(tradeStations, TrainSpawnFastExponent) + 0.5));
        }

        /// <summary>기차가 선 역에서 받는 골드 — 관계 종류별 (동맹 60000 · 팀/남 30000 · 자기 15000).</summary>
        public long TrainGold(EFrontWarsRelationKind kind)
        {
            switch (kind)
            {
                case EFrontWarsRelationKind.Ally: return ApplyGoldMultiplier(60000);
                case EFrontWarsRelationKind.Self: return ApplyGoldMultiplier(15000);
                default: return ApplyGoldMultiplier(30000);
            }
        }

        /// <summary>빠른 경제 — 팀 50000 · 자기 25000 · 동맹/남 12500.</summary>
        public long TrainGoldFast(EFrontWarsRelationKind kind)
        {
            switch (kind)
            {
                case EFrontWarsRelationKind.Team: return ApplyGoldMultiplier(50000);
                case EFrontWarsRelationKind.Self: return ApplyGoldMultiplier(25000);
                default: return ApplyGoldMultiplier(12500);
            }
        }

        /// <summary>표에 올린 실험 유닛이라도 원본 시뮬은 막지 않는다 — 막는 것은 설정의 끈 유닛 목록뿐 `[소스] isUnitDisabled(e){`.</summary>
        public bool IsUnitDisabled(EFrontWarsUnit type)
        {
            return _settings.DisabledUnits != null && _settings.DisabledUnits.Contains(type);
        }

        /// <summary>시작 골드 = 백만 × 설정 `[소스] startingGold(){`.</summary>
        public long StartingGold()
        {
            return (_settings.StartingGoldMillions ?? 0) * _settings.Config.StartingGoldUnit;
        }
    }
}
