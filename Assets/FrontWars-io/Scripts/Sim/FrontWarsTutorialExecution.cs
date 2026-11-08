using System;
using System.Collections.Generic;
using System.Linq;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 튜토리얼 판 도우미 — 사람 곁에 반란군 전초기지를 심고, 초반엔 이웃을 약하게 묶어 두고, 도시를 지으면 반격한다 `[소스] Cq3gr8XO.js:wp=class e{`.
    ///
    /// <list type="number">
    /// <item>스폰 끝 8틱 전부터 (스폰 단계 · 사람 칸 있음): 사람 첫 칸에서 20~33칸 · 맨해튼 18~40 안 빈 땅에 전초기지 봇을 스폰한다.</item>
    /// <item>스폰 단계 뒤 2400틱 전까지 50틱마다: 전초기지 병력 ≤ 사람 30%, 사람 아닌 이웃(반격자 제외) 병력 ≤ 사람 60%.</item>
    /// <item>전초기지가 무너진 뒤 150틱 · 사람에게 도시가 있거나, 3600틱이면 (스폰 보호 끝난 뒤) 병력 많은 이웃 봇이 사람 병력 35% 로 반격한다.</item>
    /// </list>
    /// </summary>
    public sealed class FrontWarsTutorialExecution : IFrontWarsExecution
    {
        public const string OutpostName = "Rebel Outpost";

        private const int BubbleEndTick = 2400;
        private const int BubbleInterval = 50;
        private const double NeighborTroopRatio = 0.6;
        private const double OutpostTroopRatio = 0.3;
        private const int CounterattackDelayTicks = 150;
        private const int CounterattackFallbackTick = 3600;
        private const double AttackerTroopRatio = 0.5;
        private const double AttackTroopRatio = 0.35;
        private const int PlantBeforeSpawnEnd = 8;
        private const int PlantAttempts = 100;
        private const int PlantRadiusMin = 20;
        private const int PlantRadiusMaxExclusive = 34;
        private const int PlantManhattanMin = 18;
        private const int PlantManhattanMax = 40;
        private const int DegreesInCircle = 360;

        private readonly FrontWarsRandom _random;
        private FrontWarsGame _game;
        private FrontWarsPlayerInfo _outpostInfo;
        private bool _outpostPlanted;
        private int? _outpostFellTick;
        private bool _counterattackLaunched;
        private string _attackerId;

        public bool IsActive => true;
        public bool ActiveDuringSpawnPhase => true;

        /// <summary>시드 = 해시(게임 id) + 1.</summary>
        public FrontWarsTutorialExecution(string gameId)
        {
            _random = new FrontWarsRandom(FrontWarsHash.Of(gameId) + 1);
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _game = game;
        }

        public void Tick(int tick)
        {
            FrontWarsPlayer human = _game.Players().Find(p => p.Type == EFrontWarsPlayerType.Human);

            if (human == null)
                return;

            int spawnTurns = _game.Settings.SpawnPhaseTurns;

            if (_outpostPlanted == false && tick >= spawnTurns - PlantBeforeSpawnEnd && _game.InSpawnPhase && human.NumTilesOwned > 0)
            {
                PlantOutpost(human);
                _outpostPlanted = true;
            }

            if (_game.InSpawnPhase || human.IsAlive == false)
                return;

            if (tick < BubbleEndTick && tick % BubbleInterval == 0)
                ApplySafetyBubble(human);

            if (_counterattackLaunched)
                return;

            if (_outpostFellTick == null)
            {
                FrontWarsPlayer outpost = Outpost();
                if (_outpostPlanted && (outpost == null || outpost.IsAlive == false))
                    _outpostFellTick = tick;
            }

            bool afterFall = _outpostFellTick != null && tick >= _outpostFellTick.Value + CounterattackDelayTicks && human.UnitsOwned(EFrontWarsUnit.City) > 0;

            if ((afterFall || tick >= CounterattackFallbackTick) && tick >= spawnTurns + _game.Settings.Config.SpawnImmunityTicks)
            {
                LaunchCounterattack(human);
                _counterattackLaunched = true;
            }
        }

        private FrontWarsPlayer Outpost()
        {
            if (_outpostInfo == null || _game.HasPlayer(_outpostInfo.Id) == false)
                return null;

            return _game.Player(_outpostInfo.Id);
        }

        private void PlantOutpost(FrontWarsPlayer human)
        {
            int first = human.Tiles.First;
            if (first < 0)
                return;

            FrontWarsTerrainMap map = _game.Map;
            int hx = map.X(first), hy = map.Y(first);

            for (int i = 0; i < PlantAttempts; i++)
            {
                int radius = _random.NextInt(PlantRadiusMin, PlantRadiusMaxExclusive);
                double angle = _random.NextInt(0, DegreesInCircle) * Math.PI / 180;
                int x = (int)Math.Floor(hx + Math.Cos(angle) * radius + 0.5);
                int y = (int)Math.Floor(hy + Math.Sin(angle) * radius + 0.5);

                if (x < 1 || y < 1 || x >= map.Width - 1 || y >= map.Height - 1)
                    continue;

                int tile = map.Ref(x, y);

                if (map.IsLand(tile) == false || _game.HasOwner(tile) || map.IsImpassable(tile))
                    continue;

                int d = map.ManhattanDistance(first, tile);
                if (d < PlantManhattanMin || d > PlantManhattanMax)
                    continue;

                _outpostInfo = new FrontWarsPlayerInfo(OutpostName, EFrontWarsPlayerType.Bot, null, _random.NextId());
                _game.AddExecution(new FrontWarsSpawnExecution(_outpostInfo, tile));
                return;
            }
        }

        private void ApplySafetyBubble(FrontWarsPlayer human)
        {
            long troops = human.Troops;
            if (troops <= 0)
                return;

            FrontWarsPlayer outpost = Outpost();
            double neighborCap = Math.Ceiling(troops * NeighborTroopRatio);
            double outpostCap = Math.Ceiling(troops * OutpostTroopRatio);

            if (outpost != null && outpost.IsAlive)
                outpost.SetTroops(Math.Min(outpost.Troops, outpostCap));

            foreach (FrontWarsPlayer n in human.Neighbors())
            {
                if (n == null || n.Type == EFrontWarsPlayerType.Human || n == outpost || (_attackerId != null && n.Id == _attackerId))
                    continue;

                n.SetTroops(Math.Min(n.Troops, neighborCap));
            }
        }

        private void LaunchCounterattack(FrontWarsPlayer human)
        {
            FrontWarsPlayer outpost = Outpost();
            List<FrontWarsPlayer> neighbors = human.Neighbors();

            FrontWarsPlayer attacker = neighbors
                .Where(p => p != null && p.Type == EFrontWarsPlayerType.Bot && p.IsAlive && p != outpost && p.IsFriendly(human) == false)
                .OrderByDescending(p => p.Troops)
                .FirstOrDefault();

            if (attacker == null && outpost != null && outpost.IsAlive && outpost.SharesBorderWith(human))
                attacker = outpost;

            if (attacker == null)
                return;

            long troops = human.Troops;
            attacker.SetTroops(Math.Max(attacker.Troops, Math.Ceiling(troops * AttackerTroopRatio)));
            _attackerId = attacker.Id;
            _game.AddExecution(new FrontWarsAttackExecution(Math.Ceiling(troops * AttackTroopRatio), attacker, human.Id));
        }
    }
}
