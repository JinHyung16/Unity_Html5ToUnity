using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 네이션 전함 도우미 — 전함 짓기 · 배 추적 보복 · 적 전함 대응 `[소스] Cq3gr8XO.js:hp=class{`.
    ///
    /// <para>
    /// 보복 확률은 난이도 표 WarshipRetaliatePercent `[소스] mp={`. 확률이 0 이면 난수를 굴리지 않는다.
    /// 적 전함 대응은 팀이 있으면 팀 단위로 센다.
    /// </para>
    /// </summary>
    public sealed class FrontWarsNationWarshipBehavior
    {
        /// <summary>팀전 대응 문턱 — 팀 전함 합 15척 초과 · 그 팀 1위가 3척 초과 `[소스] if(r<=15)continue · if(i.length>3)`.</summary>
        private const int TeamInfestationWarships = 15;
        private const int TeamInfestationPlayerWarships = 3;

        /// <summary>전함 짓기 1/40 · 항구 둘레 ±250 바다 칸 `[소스] maybeSpawnWarship(){…chance(40)…randomOceanTileNear(t.tile(),250)`.</summary>
        private const int SpawnChance = 40;
        private const int SpawnSearchRadius = 250;

        /// <summary>바다 칸 찾기 시도 수 `[소스] randomOceanTileNear(e,t){…for(let e=0;e<50;e++)`.</summary>
        private const int OceanTileAttempts = 50;

        /// <summary>전함이 이 수 이상이면 새로 짓지 않고 가까운 전함을 보낸다 `[소스] this.player.units(S.Warship).length>=10`.</summary>
        private const int MaxOwnWarships = 10;

        /// <summary>보복 관계 변화 `[소스] this.retaliate(e.tile(),t,-15) · -7.5`.</summary>
        private const double TransportLossRelation = -15;
        private const double TraderLossRelation = -7.5;
        private const double InboundRelation = -15;

        /// <summary>들어오는 수송선 — 목표 20 안이면 대응 끝 · 목표 90 안 내 전함/순찰 칸이면 덮임 · 목표 ±30 바다 `[소스] watchInboundTransports(){`.</summary>
        private const int InboundArrivedDistance = 20;
        private const int BeachCoverRange = 90;
        private const int InboundSearchRadius = 30;

        /// <summary>보낼 전함은 순찰 칸에서 130 안에 있어야 한다 `[소스] redirectNearestWarship(e){…>=130`.</summary>
        private const int RedirectPatrolRange = 130;

        /// <summary>적 전함 대응 — 맵 전체 &gt; 10 · 상대 &gt; 10 · 나보다 부자 3명 미만 `[소스] canAffordInfestationResponse(){ · ffaInfestationTarget(){ · isTopEconomy(e){`.</summary>
        private const int InfestationGlobalWarships = 10;
        private const int InfestationPlayerWarships = 10;
        private const int TopEconomyRivals = 3;

        private const int PercentRoll = 100;

        private readonly FrontWarsRandom _random;
        private readonly FrontWarsGame _game;
        private readonly FrontWarsPlayer _player;
        private readonly FrontWarsNationSharedState _shared;
        private readonly FrontWarsNationUnitSet _ownTransports = new FrontWarsNationUnitSet();
        private readonly FrontWarsNationUnitSet _ownTraders = new FrontWarsNationUnitSet();
        private readonly FrontWarsNationUnitSet _inboundThreats = new FrontWarsNationUnitSet();
        private readonly FrontWarsNationUnitSet _answeredThreats = new FrontWarsNationUnitSet();
        private readonly FrontWarsNationEmojiBehavior _emoji;

        public FrontWarsNationWarshipBehavior(FrontWarsRandom random, FrontWarsGame game, FrontWarsPlayer player, FrontWarsNationSharedState shared, FrontWarsNationEmojiBehavior emoji)
        {
            _random = random;
            _game = game;
            _player = player;
            _shared = shared;
            _emoji = emoji;
        }

        /// <summary>1/40 · 항구 있음 · 전함 0 · 골드 &gt; 전함 비용이면 무작위 항구 근처 바다에 전함 `[소스] maybeSpawnWarship(){`.</summary>
        public bool MaybeSpawnWarship()
        {
            if (_game.Rules.IsUnitDisabled(EFrontWarsUnit.Warship) || _random.Chance(SpawnChance) == false)
                return false;

            List<FrontWarsUnit> ports = _player.Units(EFrontWarsUnit.Port);

            if (ports.Count == 0 || _player.Units(EFrontWarsUnit.Warship).Count > 0 || _player.Gold <= Cost(EFrontWarsUnit.Warship))
                return false;

            FrontWarsUnit port = _random.RandElement(ports);
            int tile = RandomOceanTileNear(port.Tile, SpawnSearchRadius);

            if (tile < 0 || _player.CanBuild(EFrontWarsUnit.Warship, tile) < 0)
                return false;

            _game.AddExecution(new FrontWarsConstructionExecution(_player, EFrontWarsUnit.Warship, tile));
            return true;
        }

        private int RandomOceanTileNear(int tile, int radius)
        {
            FrontWarsTerrainMap map = _game.Map;
            int x = map.X(tile), y = map.Y(tile);

            for (int attempt = 0; attempt < OceanTileAttempts; attempt++)
            {
                int rx = _random.NextInt(x - radius, x + radius);
                int ry = _random.NextInt(y - radius, y + radius);

                if (map.IsValidCoord(rx, ry) == false)
                    continue;

                int t = map.Ref(rx, ry);
                if (map.IsOcean(t))
                    return t;
            }

            return FrontWarsNationTiles.NoTile;
        }

        /// <summary>내 수송선 · 내 무역선 · 들어오는 수송선 순서로 본다 `[소스] trackShipsAndRetaliate(){`.</summary>
        public void TrackShipsAndRetaliate()
        {
            WatchOwnTransports();
            WatchOwnTraders();
            WatchInboundTransports();
        }

        private void WatchOwnTransports()
        {
            if (_game.Rules.IsUnitDisabled(EFrontWarsUnit.TransportShip))
                return;

            foreach (FrontWarsUnit ship in _player.Units(EFrontWarsUnit.TransportShip))
                _ownTransports.Add(ship);

            foreach (FrontWarsUnit ship in _ownTransports.Snapshot())
            {
                if (ship.IsActive)
                    continue;

                _ownTransports.Remove(ship);

                if (ship.DestroyedBy != null)
                    Retaliate(ship.Tile, ship.DestroyedBy, TransportLossRelation);
            }
        }

        private void WatchOwnTraders()
        {
            foreach (FrontWarsUnit ship in _player.Units(EFrontWarsUnit.TradeShip))
                _ownTraders.Add(ship);

            foreach (FrontWarsUnit ship in _ownTraders.Snapshot())
            {
                if (ship.IsActive == false)
                {
                    _ownTraders.Remove(ship);
                    continue;
                }

                if (ship.Owner.Id != _player.Id)
                {
                    _ownTraders.Remove(ship);
                    Retaliate(ship.Tile, ship.Owner, TraderLossRelation);
                }
            }
        }

        /// <summary>틱마다 하나만 보복한다 (보복하면 순회를 멈춘다) `[소스] watchInboundTransports(){`.</summary>
        private void WatchInboundTransports()
        {
            foreach (FrontWarsUnit ship in _shared.IncomingTransportsFor(_game, _player.SmallId))
                _inboundThreats.Add(ship);

            foreach (FrontWarsUnit ship in _inboundThreats.Snapshot())
            {
                int target = ship.TargetTile;

                if (ship.IsActive == false || target < 0 || ship.Retreating)
                {
                    _inboundThreats.Remove(ship);
                    _answeredThreats.Remove(ship);
                    continue;
                }

                if (_answeredThreats.Contains(ship))
                    continue;

                if (_game.Map.ManhattanDistance(ship.Tile, target) < InboundArrivedDistance)
                {
                    _answeredThreats.Add(ship);
                    continue;
                }

                if (ship.Owner.IsAlliedWith(_player))
                    continue;

                if (IsBeachCovered(target))
                {
                    _answeredThreats.Add(ship);
                    continue;
                }

                int ocean = RandomOceanTileNear(target, InboundSearchRadius);

                if (ocean >= 0)
                {
                    Retaliate(ocean, ship.Owner, InboundRelation);
                    _answeredThreats.Add(ship);
                    break;
                }
            }
        }

        private bool IsBeachCovered(int tile)
        {
            if (_game.HasUnitNearby(tile, BeachCoverRange, EFrontWarsUnit.Warship, _player))
                return true;

            foreach (FrontWarsUnit warship in _player.Units(EFrontWarsUnit.Warship))
            {
                int patrol = warship.PatrolTile;
                if (patrol >= 0 && _game.Map.ManhattanDistance(tile, patrol) < BeachCoverRange)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 그 칸에 전함으로 보복 `[소스] retaliate(e,t,n){`.
        /// <para>전함 10척 이상 → 가까운 전함 보내기. 아니면 확률에 걸릴 때 지을 수 있으면 짓고 이모지 · 관계 변화, 못 지으면 가까운 전함 보내기.</para>
        /// </summary>
        private void Retaliate(int tile, FrontWarsPlayer target, double relationDelta)
        {
            if (target == _player)
                return;

            if (_player.Units(EFrontWarsUnit.Warship).Count >= MaxOwnWarships)
            {
                RedirectNearestWarship(tile);
                return;
            }

            int percent = _game.Settings.DifficultyRow.WarshipRetaliatePercent;

            if (percent == 0 || _random.NextInt(0, PercentRoll) >= percent)
                return;

            if (_player.CanBuild(EFrontWarsUnit.Warship, tile) < 0)
            {
                RedirectNearestWarship(tile);
                return;
            }

            _game.AddExecution(new FrontWarsConstructionExecution(_player, EFrontWarsUnit.Warship, tile));
            _emoji?.MaybeSendWarshipRetaliationEmoji(target);
            _player.UpdateRelation(target, relationDelta);
        }

        /// <summary>물 칸이면 순찰 칸 근처(130 안)에 있는 내 전함 중 가장 가까운 것을 그 칸으로 `[소스] redirectNearestWarship(e){`.</summary>
        private void RedirectNearestWarship(int tile)
        {
            if (_game.Map.IsWater(tile) == false)
                return;

            FrontWarsUnit nearest = null;
            double nearestDistance = double.PositiveInfinity;

            foreach (FrontWarsUnit warship in _player.Units(EFrontWarsUnit.Warship))
            {
                int patrol = warship.PatrolTile;
                if (patrol < 0 || _game.Map.ManhattanDistance(warship.Tile, patrol) >= RedirectPatrolRange)
                    continue;

                int d = _game.Map.ManhattanDistance(warship.Tile, tile);
                if (d < nearestDistance)
                {
                    nearestDistance = d;
                    nearest = warship;
                }
            }

            if (nearest != null)
                _game.AddExecution(new FrontWarsMoveWarshipExecution(_player, nearest.Id, tile));
        }

        /// <summary>
        /// 적 전함이 들끓으면 그 전함 하나에 전함을 지어 맞선다 `[소스] counterWarshipInfestation(){`.
        /// <para>개인전: 전함을 10척 넘게 가진 비우호 플레이어. 팀전: 비우호 팀 전함 합이 15척 넘는 팀에서 가장 많이 가진 플레이어가 3척 넘으면.</para>
        /// </summary>
        public void CounterWarshipInfestation()
        {
            if (CanAffordInfestationResponse() == false)
                return;

            bool hasTeam = _player.Team != null;

            if (IsTopEconomy(hasTeam) == false)
                return;

            FrontWarsUnit warship = hasTeam ? TeamInfestationTarget() : FfaInfestationTarget();
            if (warship == null)
                return;

            int tile = warship.Tile;

            if (_player.CanBuild(EFrontWarsUnit.Warship, tile) < 0)
            {
                RedirectNearestWarship(tile);
                return;
            }

            _game.AddExecution(new FrontWarsConstructionExecution(_player, EFrontWarsUnit.Warship, tile));
            _emoji?.SendWarshipRetaliationEmojiToAll();
        }

        private bool CanAffordInfestationResponse()
        {
            if (_game.Rules.IsUnitDisabled(EFrontWarsUnit.Warship))
                return false;

            EFrontWarsDifficulty difficulty = _game.Settings.Difficulty;

            if ((difficulty != EFrontWarsDifficulty.Hard && difficulty != EFrontWarsDifficulty.Impossible)
                || _game.UnitCount(EFrontWarsUnit.Warship) <= InfestationGlobalWarships
                || Cost(EFrontWarsUnit.Warship) > _player.Gold
                || _player.Units(EFrontWarsUnit.Port).Count == 0)
                return false;

            return _player.Units(EFrontWarsUnit.Warship).Count < MaxOwnWarships;
        }

        /// <summary>나보다 골드가 많은 사람 아닌 플레이어(팀이 있으면 같은 팀만)가 3명 미만 `[소스] isTopEconomy(e){`.</summary>
        private bool IsTopEconomy(bool sameTeamOnly)
        {
            long gold = _player.Gold;
            int richer = 0;

            foreach (FrontWarsPlayer p in _game.Players())
            {
                if (p == _player || p.Type == EFrontWarsPlayerType.Human || (sameTeamOnly && p.Team != _player.Team) || p.Gold <= gold)
                    continue;

                richer++;
                if (richer >= TopEconomyRivals)
                    return false;
            }

            return true;
        }

        private FrontWarsUnit FfaInfestationTarget()
        {
            foreach (FrontWarsPlayer p in _game.Players())
            {
                if (p == _player || _player.IsFriendly(p))
                    continue;

                List<FrontWarsUnit> warships = p.Units(EFrontWarsUnit.Warship);
                if (warships.Count > InfestationPlayerWarships)
                    return _random.RandElement(warships);
            }

            return null;
        }

        /// <summary>
        /// 비우호 플레이어를 팀별로 모아 전함 합 · 가장 많이 가진 사람(같으면 먼저)을 잰 뒤, 처음 나온 팀 순서로
        /// 합이 15척 넘고 그 사람이 3척 넘게 가진 첫 팀의 전함 하나 `[소스] teamInfestationTarget(){`.
        /// </summary>
        private FrontWarsUnit TeamInfestationTarget()
        {
            var order = new List<string>();
            var counts = new Dictionary<string, int>();
            var top = new Dictionary<string, FrontWarsPlayer>();

            foreach (FrontWarsPlayer p in _game.Players())
            {
                if (p == _player || _player.IsFriendly(p))
                    continue;

                string team = p.Team;
                if (team == null)
                    continue;

                int n = p.Units(EFrontWarsUnit.Warship).Count;

                if (counts.TryGetValue(team, out int sum) == false)
                    order.Add(team);

                counts[team] = sum + n;

                if (top.TryGetValue(team, out FrontWarsPlayer best) == false || n > best.Units(EFrontWarsUnit.Warship).Count)
                    top[team] = p;
            }

            foreach (string team in order)
            {
                if (counts[team] <= TeamInfestationWarships)
                    continue;

                if (top.TryGetValue(team, out FrontWarsPlayer p) == false)
                    continue;

                List<FrontWarsUnit> warships = p.Units(EFrontWarsUnit.Warship);
                if (warships.Count > TeamInfestationPlayerWarships)
                    return _random.RandElement(warships);
            }

            return null;
        }

        private long Cost(EFrontWarsUnit type)
        {
            return _game.Rules.UnitCost(type, _player);
        }
    }
}
