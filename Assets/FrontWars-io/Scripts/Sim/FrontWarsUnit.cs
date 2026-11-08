using System;
using System.Collections.Generic;
using JinHyung.Data;

namespace JinHyung.FrontWarsIo
{
    /// <summary>핵 궤적 한 점 — SAM 이 잡을 수 있는 칸인가 `[소스] trajectory.push({tile:i,targetable:…})`.</summary>
    public readonly struct FrontWarsTrajectoryPoint
    {
        public readonly int Tile;
        public readonly bool Targetable;

        public FrontWarsTrajectoryPoint(int tile, bool targetable)
        {
            Tile = tile;
            Targetable = targetable;
        }
    }

    /// <summary>유닛을 만들 때 넘기는 선택 값 `[소스] Sm=class{constructor(e,t,n,r,i,a={})` 의 a.</summary>
    public sealed class FrontWarsUnitParams
    {
        public int TargetTile = -1;
        public List<FrontWarsTrajectoryPoint> Trajectory;
        public double Troops;
        public int LastSetSafeFromPirates;
        public int PatrolTile = -1;
        public FrontWarsUnit TargetUnit;
        public bool? Loaded;
        public string TrainType;
    }

    /// <summary>
    /// 유닛 · 건물 하나 `[소스] Cq3gr8XO.js:Sm=class{`. 배 · 미사일 · 건물 · 공사 중 표시가 전부 이것이다.
    /// <para>상태가 바뀌면 게임의 유닛 갱신 이벤트를 낸다 — 그리기 쪽이 받는다.</para>
    /// </summary>
    public sealed class FrontWarsUnit
    {
        private readonly FrontWarsGame _game;

        public EFrontWarsUnit Type { get; }
        public int Id { get; }
        public FrontWarsPlayer Owner { get; private set; }
        public FrontWarsPlayer LastOwner { get; private set; }
        public FrontWarsPlayer DestroyedBy { get; private set; }
        public int Tile { get; private set; }
        public int LastTile { get; private set; }
        public bool IsActive { get; private set; } = true;
        public bool Retreating { get; private set; }
        public bool TargetedBySam { get; set; }
        public bool ReachedTarget { get; private set; }
        public List<int> MissileTimerQueue { get; } = new List<int>();
        public bool HasTrainStation { get; private set; }
        public int Level { get; private set; } = 1;
        public bool Targetable { get; private set; } = true;
        public int TrajectoryIndex { get; private set; }
        public List<FrontWarsTrajectoryPoint> Trajectory { get; }
        public double Troops { get; set; }
        public int TargetTile { get; set; }
        public int PatrolTile { get; set; }
        public FrontWarsUnit TargetUnit { get; set; }
        public bool? Loaded { get; private set; }
        public string TrainType { get; }

        /// <summary>공사 중이면 무엇을 짓는가.</summary>
        public EFrontWarsUnit? ConstructionTypeValue { get; private set; }

        private long _health;
        private int _lastSetSafeFromPirates;

        public FrontWarsUnit(EFrontWarsUnit type, FrontWarsGame game, int tile, int id, FrontWarsPlayer owner, FrontWarsUnitParams p = null)
        {
            p ??= new FrontWarsUnitParams();
            Type = type;
            _game = game;
            Tile = tile;
            LastTile = tile;
            Id = id;
            Owner = owner;
            _health = HasHealth ? Info.MaxHealth : 1;
            TargetTile = p.TargetTile;
            Trajectory = p.Trajectory ?? new List<FrontWarsTrajectoryPoint>();
            Troops = p.Troops;
            _lastSetSafeFromPirates = p.LastSetSafeFromPirates;
            PatrolTile = p.PatrolTile;
            TargetUnit = p.TargetUnit;
            Loaded = p.Loaded;
            TrainType = p.TrainType;
        }

        public FrontWarsUnitData Info
        {
            get { return _game.Settings.Unit(Type); }
        }

        /// <summary>최대 체력이 있는 종류만 체력을 쓴다 (전함).</summary>
        public bool HasHealth
        {
            get { return Info.MaxHealth > 0; }
        }

        public long Health
        {
            get { return _health; }
        }

        private void Touch()
        {
            _game.RaiseUnitUpdate(this);
        }

        public void SetTargetable(bool value)
        {
            if (Targetable == value)
                return;

            Targetable = value;
            Touch();
        }

        public void Move(int tile)
        {
            LastTile = Tile;
            Tile = tile;
            _game.Units.UpdateUnitCell(this);
            Touch();
        }

        /// <summary>주인을 바꾸고 양쪽에 알림을 낸다 `[소스] setOwner(e){`.</summary>
        public void SetOwner(FrontWarsPlayer newOwner)
        {
            LastOwner = Owner;
            LastOwner.RemoveUnitRef(this);
            Owner = newOwner;
            Owner.AddUnitRef(this);
            Touch();
            _game.DisplayMessage(EFrontWarsMessage.UnitCapturedByEnemy, LastOwner, 0, newOwner.Name);
            _game.DisplayMessage(EFrontWarsMessage.CapturedEnemyUnit, newOwner, 0, LastOwner.Name);
        }

        /// <summary>체력 더하기 (0 ~ 최대). 0 이 되면 파괴 `[소스] modifyHealth(e,t){`.</summary>
        public void ModifyHealth(double delta, FrontWarsPlayer attacker)
        {
            long max = HasHealth ? Info.MaxHealth : 1;
            _health = Math.Min(Math.Max(_health + FrontWarsPlayer.ToStoredTroops(delta), 0), max);

            if (_health == 0)
                Delete(true, attacker);
        }

        /// <summary>파괴 · 삭제 `[소스] delete(e,t){`. 이미 끝난 유닛이면 멈춘다.</summary>
        public void Delete(bool displayMessage = true, FrontWarsPlayer destroyedBy = null)
        {
            if (IsActive == false)
                throw new InvalidOperationException($"cannot delete {Type} not active");

            DestroyedBy = destroyedBy;
            Owner.RemoveUnitRef(this);
            IsActive = false;
            Touch();
            _game.RemoveUnit(this);

            if (displayMessage && Type != EFrontWarsUnit.MIRVWarhead)
                _game.DisplayMessage(EFrontWarsMessage.UnitDestroyed, Owner, 0, FrontWarsUnitNames.Of(Type));
        }

        public void OrderBoatRetreat()
        {
            if (Type != EFrontWarsUnit.TransportShip)
                throw new InvalidOperationException($"Cannot retreat {Type}");

            Retreating = true;
        }

        public EFrontWarsUnit? ConstructionType
        {
            get
            {
                if (Type != EFrontWarsUnit.Construction)
                    throw new InvalidOperationException($"Cannot get construction type on {Type}");

                return ConstructionTypeValue;
            }
        }

        public void SetConstructionType(EFrontWarsUnit type)
        {
            if (Type != EFrontWarsUnit.Construction)
                throw new InvalidOperationException($"Cannot set construction type on {Type}");

            ConstructionTypeValue = type;
            Touch();
        }

        /// <summary>위치 + 해시(종류 이름) × id — 원본이 숫자로 더한다 `[소스] hash(){return this.tile()+P(this.type())*this._id}`.</summary>
        public double Hash()
        {
            return Tile + (double)FrontWarsHash.Of(FrontWarsUnitNames.Of(Type)) * Id;
        }

        // ── 사일로 · SAM 장전

        public void Launch()
        {
            MissileTimerQueue.Add(_game.Ticks);
            Touch();
        }

        public int TicksLeftInCooldown
        {
            get { return MissileTimerQueue.Count > 0 ? MissileTimerQueue[0] : -1; }
        }

        /// <summary>장전 줄이 레벨만큼 차면 쿨다운 중.</summary>
        public bool IsInCooldown
        {
            get { return MissileTimerQueue.Count == Level; }
        }

        public void ReloadMissile()
        {
            if (MissileTimerQueue.Count > 0)
                MissileTimerQueue.RemoveAt(0);

            Touch();
        }

        public void SetTrajectoryIndex(int index)
        {
            int last = Trajectory.Count - 1;
            TrajectoryIndex = index < 0 ? 0 : index > last ? last : index;
        }

        public void SetReachedTarget()
        {
            ReachedTarget = true;
        }

        public void SetSafeFromPirates()
        {
            _lastSetSafeFromPirates = _game.Ticks;
        }

        public bool IsSafeFromPirates
        {
            get { return _game.Ticks - _lastSetSafeFromPirates < _game.Settings.Config.SafeFromPiratesTicks; }
        }

        public void SetTrainStation(bool value)
        {
            HasTrainStation = value;
            Touch();
        }

        public void SetLoaded(bool value)
        {
            if (Loaded == value)
                return;

            Loaded = value;
            Touch();
        }

        /// <summary>레벨 +1. 사일로 · SAM 은 새 칸을 쿨다운 중으로 넣는다.</summary>
        public void IncreaseLevel()
        {
            Level++;

            if (Type == EFrontWarsUnit.MissileSilo || Type == EFrontWarsUnit.SAMLauncher)
                MissileTimerQueue.Add(_game.Ticks);

            Touch();
        }

        /// <summary>레벨 −1. 0 이하가 되면 파괴 `[소스] decreaseLevel(e){`.</summary>
        public void DecreaseLevel(FrontWarsPlayer destroyer)
        {
            Level--;

            if ((Type == EFrontWarsUnit.MissileSilo || Type == EFrontWarsUnit.SAMLauncher) && MissileTimerQueue.Count > 0)
                MissileTimerQueue.RemoveAt(MissileTimerQueue.Count - 1);

            if (Level <= 0)
            {
                Delete(true, destroyer);
                return;
            }

            Touch();
        }
    }

    /// <summary>
    /// 유닛 칸 격자 — 64칸 단위 칸마다 종류별 유닛 집합 `[소스] Cq3gr8XO.js:th=class{`.
    /// <para>근처 유닛 목록 순서는 (격자 y → x → 요청 종류 순서 → 넣은 순서) 로 원본과 같다.</para>
    /// </summary>
    public sealed class FrontWarsUnitGrid
    {
        private const int CellSize = 64;
        private const int CellShift = 6;

        private readonly FrontWarsGame _game;
        private readonly Dictionary<EFrontWarsUnit, List<FrontWarsUnit>>[,] _cells;
        private readonly int _cols;
        private readonly int _rows;

        public FrontWarsUnitGrid(FrontWarsGame game)
        {
            _game = game;
            _rows = (int)Math.Ceiling(game.Map.Height / (double)CellSize);
            _cols = (int)Math.Ceiling(game.Map.Width / (double)CellSize);
            _cells = new Dictionary<EFrontWarsUnit, List<FrontWarsUnit>>[_rows, _cols];

            for (int y = 0; y < _rows; y++)
            for (int x = 0; x < _cols; x++)
                _cells[y, x] = new Dictionary<EFrontWarsUnit, List<FrontWarsUnit>>();
        }

        public void AddUnit(FrontWarsUnit unit)
        {
            AddAt(unit, unit.Tile);
        }

        private void AddAt(FrontWarsUnit unit, int tile)
        {
            int cx = _game.Map.X(tile) >> CellShift, cy = _game.Map.Y(tile) >> CellShift;
            if (IsValidCell(cx, cy) == false)
                return;

            Dictionary<EFrontWarsUnit, List<FrontWarsUnit>> cell = _cells[cy, cx];
            if (cell.TryGetValue(unit.Type, out List<FrontWarsUnit> list) == false)
                cell[unit.Type] = list = new List<FrontWarsUnit>();

            if (list.Contains(unit) == false)
                list.Add(unit);
        }

        public void RemoveUnit(FrontWarsUnit unit)
        {
            RemoveAt(unit, unit.Tile);
        }

        private void RemoveAt(FrontWarsUnit unit, int tile)
        {
            int cx = _game.Map.X(tile) >> CellShift, cy = _game.Map.Y(tile) >> CellShift;
            if (IsValidCell(cx, cy) == false)
                return;

            if (_cells[cy, cx].TryGetValue(unit.Type, out List<FrontWarsUnit> list))
                list.Remove(unit);
        }

        public void UpdateUnitCell(FrontWarsUnit unit)
        {
            int ox = _game.Map.X(unit.LastTile) >> CellShift, oy = _game.Map.Y(unit.LastTile) >> CellShift;
            int nx = _game.Map.X(unit.Tile) >> CellShift, ny = _game.Map.Y(unit.Tile) >> CellShift;

            if (ox != nx || oy != ny)
            {
                RemoveAt(unit, unit.LastTile);
                AddUnit(unit);
            }
        }

        private bool IsValidCell(int x, int y)
        {
            return x >= 0 && x < _cols && y >= 0 && y < _rows;
        }

        private void CellsInRange(int tile, int range, out int sx, out int ex, out int sy, out int ey)
        {
            int gx = _game.Map.X(tile) >> CellShift, gy = _game.Map.Y(tile) >> CellShift;
            int cells = (int)Math.Ceiling(range / (double)CellSize);
            sx = Math.Max(0, gx - cells);
            ex = Math.Min(_cols - 1, gx + cells);
            sy = Math.Max(0, gy - cells);
            ey = Math.Min(_rows - 1, gy + cells);
        }

        private int SquaredDistance(FrontWarsUnit unit, int tile)
        {
            int dx = _game.Map.X(unit.Tile) - _game.Map.X(tile);
            int dy = _game.Map.Y(unit.Tile) - _game.Map.Y(tile);
            return dx * dx + dy * dy;
        }

        /// <summary>반경 안 활성 유닛 (거리 제곱 포함). filter 가 거짓이면 뺀다 `[소스] nearbyUnits(e,t,n,r){`.</summary>
        public List<(FrontWarsUnit Unit, int DistSquared)> NearbyUnits(int tile, int range, IReadOnlyList<EFrontWarsUnit> types, Func<FrontWarsUnit, int, bool> filter = null)
        {
            var result = new List<(FrontWarsUnit, int)>();
            CellsInRange(tile, range, out int sx, out int ex, out int sy, out int ey);
            int r2 = range * range;
            var uniqueTypes = new List<EFrontWarsUnit>();

            foreach (EFrontWarsUnit t in types)
            {
                if (uniqueTypes.Contains(t) == false)
                    uniqueTypes.Add(t);
            }

            for (int y = sy; y <= ey; y++)
            {
                for (int x = sx; x <= ex; x++)
                {
                    foreach (EFrontWarsUnit type in uniqueTypes)
                    {
                        if (_cells[y, x].TryGetValue(type, out List<FrontWarsUnit> list) == false)
                            continue;

                        foreach (FrontWarsUnit u in list.ToArray())
                        {
                            if (u.IsActive == false)
                                continue;

                            int d = SquaredDistance(u, tile);
                            if (d > r2)
                                continue;

                            if (filter == null || filter(u, d))
                                result.Add((u, d));
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>반경 안에 그 주인(null = 아무나)의 그 종류 활성 유닛이 있나 `[소스] hasUnitNearby(e,t,n,r){`.</summary>
        public bool HasUnitNearby(int tile, int range, EFrontWarsUnit type, FrontWarsPlayer owner)
        {
            CellsInRange(tile, range, out int sx, out int ex, out int sy, out int ey);
            int r2 = range * range;

            for (int y = sy; y <= ey; y++)
            {
                for (int x = sx; x <= ex; x++)
                {
                    if (_cells[y, x].TryGetValue(type, out List<FrontWarsUnit> list) == false)
                        continue;

                    foreach (FrontWarsUnit u in list)
                    {
                        if (u.IsActive == false || (owner != null && u.Owner.Id != owner.Id))
                            continue;

                        if (SquaredDistance(u, tile) <= r2)
                            return true;
                    }
                }
            }

            return false;
        }
    }
}
