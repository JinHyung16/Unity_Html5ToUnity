using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 도시 — 첫 틱에 짓고, 반경 안에 공장이 있으면 기차역을 만든다. 빼앗기면 새 주인을 따라간다 `[소스] Cq3gr8XO.js:wd=class{`.
    /// </summary>
    public sealed class FrontWarsCityExecution : IFrontWarsExecution
    {
        private FrontWarsPlayer _player;
        private readonly int _tile;
        private FrontWarsGame _game;
        private FrontWarsUnit _city;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsCityExecution(FrontWarsPlayer player, int tile)
        {
            _player = player;
            _tile = tile;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _game = game;
        }

        public void Tick(int tick)
        {
            if (_city == null)
            {
                int spawn = _player.CanBuild(EFrontWarsUnit.City, _tile);

                if (spawn == FrontWarsPlayer.NoTile)
                {
                    IsActive = false;
                    return;
                }

                _city = _player.BuildUnit(EFrontWarsUnit.City, spawn);
                FrontWarsTrainStationExecution.CreateStationIfFactoryNearby(_game, _city);
            }

            if (_city.IsActive == false)
            {
                IsActive = false;
                return;
            }

            if (_player != _city.Owner)
                _player = _city.Owner;
        }
    }

    /// <summary>
    /// 미사일 사일로 — 첫 틱에 짓고, 장전 줄 맨 앞이 쿨다운(75틱)을 지나면 하나 장전한다 `[소스] Cq3gr8XO.js:df=class{`.
    /// <para>⚠ 원본은 사일로가 파괴돼도 이 실행을 끝내지 않는다 (활성 유지). 그대로 둔다.</para>
    /// </summary>
    public sealed class FrontWarsMissileSiloExecution : IFrontWarsExecution
    {
        private FrontWarsPlayer _player;
        private readonly int _tile;
        private FrontWarsGame _game;
        private FrontWarsUnit _silo;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsMissileSiloExecution(FrontWarsPlayer player, int tile)
        {
            _player = player;
            _tile = tile;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _game = game;
        }

        public void Tick(int tick)
        {
            if (_silo == null)
            {
                int spawn = _player.CanBuild(EFrontWarsUnit.MissileSilo, _tile);

                if (spawn == FrontWarsPlayer.NoTile)
                {
                    IsActive = false;
                    return;
                }

                _silo = _player.BuildUnit(EFrontWarsUnit.MissileSilo, spawn);

                if (_player != _silo.Owner)
                    _player = _silo.Owner;
            }

            if (_silo.MissileTimerQueue.Count > 0 && _game.Settings.Config.SiloCooldownTicks - (_game.Ticks - _silo.MissileTimerQueue[0]) <= 0)
                _silo.ReloadMissile();
        }
    }

    /// <summary>
    /// 방어초소 — 첫 틱에 짓는다. 주변 적 공격에 방어 ×5 · 속도 ×3 은 공격 식이 근처 검사로 건다 `[소스] Cq3gr8XO.js:of=class{`.
    /// <para>원본에는 포탄 쏘기 코드가 있지만 목표를 정하는 곳이 없어 절대 쏘지 않는다 — 그대로 둔다 (`02_시스템_건물.md`).</para>
    /// </summary>
    public sealed class FrontWarsDefensePostExecution : IFrontWarsExecution
    {
        private FrontWarsPlayer _player;
        private readonly int _tile;
        private FrontWarsUnit _post;
        private FrontWarsUnit _target;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsDefensePostExecution(FrontWarsPlayer player, int tile)
        {
            _player = player;
            _tile = tile;
        }

        public void Init(FrontWarsGame game, int tick)
        {
        }

        public void Tick(int tick)
        {
            if (_post == null)
            {
                int spawn = _player.CanBuild(EFrontWarsUnit.DefensePost, _tile);

                if (spawn == FrontWarsPlayer.NoTile)
                {
                    IsActive = false;
                    return;
                }

                _post = _player.BuildUnit(EFrontWarsUnit.DefensePost, spawn);
            }

            if (_post.IsActive == false)
            {
                IsActive = false;
                return;
            }

            if (_player != _post.Owner)
                _player = _post.Owner;

            if (_target != null && _target.IsActive == false)
                _target = null;
        }
    }

    /// <summary>
    /// 공장 — 첫 틱에 짓고, 자기 기차역을 만든 뒤 반경 안 도시 · 항구 · 공장 중 역이 없는 것마다 역을 만든다 `[소스] Cq3gr8XO.js:sf=class{`.
    /// </summary>
    public sealed class FrontWarsFactoryExecution : IFrontWarsExecution
    {
        private FrontWarsPlayer _player;
        private readonly int _tile;
        private FrontWarsGame _game;
        private FrontWarsUnit _factory;

        private static readonly EFrontWarsUnit[] StationTypes = { EFrontWarsUnit.City, EFrontWarsUnit.Port, EFrontWarsUnit.Factory };

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsFactoryExecution(FrontWarsPlayer player, int tile)
        {
            _player = player;
            _tile = tile;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _game = game;
        }

        public void Tick(int tick)
        {
            if (_factory == null)
            {
                int spawn = _player.CanBuild(EFrontWarsUnit.Factory, _tile);

                if (spawn == FrontWarsPlayer.NoTile)
                {
                    IsActive = false;
                    return;
                }

                _factory = _player.BuildUnit(EFrontWarsUnit.Factory, spawn);
                CreateStations();
            }

            if (_factory.IsActive == false)
            {
                IsActive = false;
                return;
            }

            if (_player != _factory.Owner)
                _player = _factory.Owner;
        }

        private void CreateStations()
        {
            List<(FrontWarsUnit Unit, int DistSquared)> nearby = _game.Units.NearbyUnits(_factory.Tile, _game.Settings.Config.TrainStationMaxRange, StationTypes);
            _game.AddExecution(new FrontWarsTrainStationExecution(_factory, true));

            foreach (var (unit, _) in nearby)
            {
                if (unit.HasTrainStation == false)
                    _game.AddExecution(new FrontWarsTrainStationExecution(unit));
            }
        }
    }

    /// <summary>
    /// 유닛 스스로 지우기 — 내 땅 위 · 땅 칸 · 스폰 단계 아님 · 쿨다운(10틱) 끝일 때만 `[소스] Cq3gr8XO.js:bf=class{`.
    /// </summary>
    public sealed class FrontWarsDeleteUnitExecution : IFrontWarsExecution
    {
        private readonly FrontWarsPlayer _player;
        private readonly int _unitId;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsDeleteUnitExecution(FrontWarsPlayer player, int unitId)
        {
            _player = player;
            _unitId = unitId;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            if (IsActive == false)
                return;

            IsActive = false;
            FrontWarsUnit unit = _player.Units().Find(u => u.Id == _unitId);

            if (unit == null || unit.IsActive == false)
                return;

            FrontWarsPlayer owner = game.Owner(unit.Tile);

            if (owner == null || owner.Id != _player.Id || game.Map.IsLand(unit.Tile) == false || game.InSpawnPhase || _player.CanDeleteUnit() == false)
                return;

            unit.Delete(false);
            _player.RecordDeleteUnit();
            game.DisplayMessage(EFrontWarsMessage.UnitDestroyed, _player);
        }

        public void Tick(int tick)
        {
        }
    }
}
