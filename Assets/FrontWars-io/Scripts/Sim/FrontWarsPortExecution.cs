using System.Collections.Generic;
using System.Linq;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 항구 — 첫 틱에 짓고(근처 공장이 있으면 기차역), 10틱에 한 번씩 무역선을 띄울지 굴린다 `[소스] Cq3gr8XO.js:pf=class{`.
    /// <para>굴림은 레벨 수만큼 1/r 로 한다. 뜨면 교역 가능한 항구 목록(가까운 항구 · 우호 항구는 여러 번 들어간다)에서 하나를 고른다.</para>
    /// </summary>
    public sealed class FrontWarsPortExecution : IFrontWarsExecution
    {
        private const int SpawnCheckInterval = 10;

        private FrontWarsPlayer _player;
        private readonly int _tile;
        private FrontWarsGame _game;
        private FrontWarsRandom _random;
        private int _checkOffset;
        private FrontWarsUnit _port;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsPortExecution(FrontWarsPlayer player, int tile)
        {
            _player = player;
            _tile = tile;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _game = game;
            _random = new FrontWarsRandom(game.Ticks);
            _checkOffset = game.Ticks % SpawnCheckInterval;
        }

        public void Tick(int tick)
        {
            if (_port == null)
            {
                int spawn = _player.CanBuild(EFrontWarsUnit.Port, _tile);

                if (spawn == FrontWarsPlayer.NoTile)
                {
                    IsActive = false;
                    return;
                }

                _port = _player.BuildUnit(EFrontWarsUnit.Port, spawn);
                FrontWarsTrainStationExecution.CreateStationIfFactoryNearby(_game, _port);
            }

            if (_port.IsActive == false)
            {
                IsActive = false;
                return;
            }

            if (_player.Id != _port.Owner.Id)
                _player = _port.Owner;

            if ((_game.Ticks + _checkOffset) % SpawnCheckInterval != 0 || ShouldSpawnTradeShip() == false)
                return;

            List<FrontWarsUnit> ports = _player.TradingPorts(_port);

            if (ports.Count == 0)
                return;

            FrontWarsUnit destination = _random.RandElement(ports);
            _game.AddExecution(new FrontWarsTradeShipExecution(_player, _port, destination));
        }

        private bool ShouldSpawnTradeShip()
        {
            int totalTradeShips = _game.UnitCount(EFrontWarsUnit.TradeShip);
            int rate = _game.Settings.EconomyMode == EFrontWarsEconomyMode.Classic
                ? _game.Rules.TradeShipSpawnRate(totalTradeShips, _player.UnitCount(EFrontWarsUnit.Port), _player.UnitCount(EFrontWarsUnit.TradeShip))
                : _game.Rules.TradeShipSpawnRateFast(totalTradeShips);

            for (int i = 0; i < _port.Level; i++)
            {
                if (_random.Chance(rate))
                    return true;
            }

            return false;
        }
    }

    public sealed partial class FrontWarsPlayer
    {
        /// <summary>
        /// 무역선 목적지 후보 — 교역 가능한 다른 살아 있는 플레이어의 항구를 가까운 순으로, 레벨만큼 넣는다.
        /// 앞쪽 몇 곳과 우호 항구는 한 번씩 더 넣는다 `[소스] tradingPorts(e){`.
        /// </summary>
        public List<FrontWarsUnit> TradingPorts(FrontWarsUnit port)
        {
            FrontWarsPlayer owner = port.Owner;
            List<FrontWarsUnit> ports = _game.Players()
                .Where(p => p != owner && p.CanTrade(owner))
                .SelectMany(p => p.Units(EFrontWarsUnit.Port))
                .OrderBy(u => _game.Map.ManhattanDistance(port.Tile, u.Tile))
                .ToList();

            double bonusCount = _game.Rules.ProximityBonusPortsNb(ports.Count);
            var result = new List<FrontWarsUnit>();

            for (int i = 0; i < ports.Count; i++)
            {
                FrontWarsUnit p = ports[i];

                for (int k = 0; k < p.Level; k++)
                    result.Add(p);

                if (i < bonusCount)
                {
                    for (int k = 0; k < p.Level; k++)
                        result.Add(p);
                }

                if (owner.IsFriendly(p.Owner))
                {
                    for (int k = 0; k < p.Level; k++)
                        result.Add(p);
                }
            }

            return result;
        }
    }
}
