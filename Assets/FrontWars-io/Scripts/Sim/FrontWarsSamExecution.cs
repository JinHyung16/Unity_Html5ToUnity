using System.Collections.Generic;
using System.Linq;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// SAM 발사대 `[소스] Cq3gr8XO.js:gf=class{`.
    ///
    /// <list type="number">
    /// <item>첫 틱에 짓는다. 쿨다운 중이면 장전만 보고 끝.</item>
    /// <item>반경 400 안에서 목표가 발사대 50칸 안인 적 MIRV 탄두를 찾는다. 없으면 표적 시스템이 원자탄 · 수소탄 하나를 고른다.</item>
    /// <item>쏠 게 있으면 발사(장전 줄에 넣음) → 발사대 id 시드 난수로 명중 판정. 원자탄은 늘 명중, 탄두 0.5, 수소탄 0.8.</item>
    /// <item>탄두 명중이면 찾은 탄두 전부 제거. 원자 · 수소탄 명중이면 요격 미사일 실행을 넣는다.</item>
    /// </list>
    /// </summary>
    public sealed class FrontWarsSamLauncherExecution : IFrontWarsExecution
    {
        private const int WarheadSearchRadius = 400;
        private const int WarheadProtectionRadius = 50;

        private static readonly EFrontWarsUnit[] WarheadTypes = { EFrontWarsUnit.MIRVWarhead };

        private FrontWarsPlayer _player;
        private readonly int _tile;
        private FrontWarsGame _game;
        private FrontWarsUnit _sam;
        private FrontWarsSamTargeting _targeting;
        private FrontWarsRandom _pseudoRandom;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsSamLauncherExecution(FrontWarsPlayer player, int tile, FrontWarsUnit existing = null)
        {
            _player = player;
            _tile = existing?.Tile ?? tile;
            _sam = existing;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _game = game;
        }

        private bool IsHit(EFrontWarsUnit type, double roll)
        {
            if (type == EFrontWarsUnit.AtomBomb)
                return true;

            return type == EFrontWarsUnit.MIRVWarhead
                ? roll < _game.Settings.Config.SamWarheadHitChance
                : roll < _game.Settings.Config.SamHitChance;
        }

        public void Tick(int tick)
        {
            if (_sam == null)
            {
                int spawn = _player.CanBuild(EFrontWarsUnit.SAMLauncher, _tile);

                if (spawn == FrontWarsPlayer.NoTile)
                {
                    IsActive = false;
                    return;
                }

                _sam = _player.BuildUnit(EFrontWarsUnit.SAMLauncher, spawn);
            }

            _targeting ??= new FrontWarsSamTargeting(_game, _sam);

            if (_sam.IsInCooldown)
            {
                if (_sam.MissileTimerQueue.Count == 0)
                    return;

                if (_game.Settings.Config.SamCooldownTicks - (_game.Ticks - _sam.MissileTimerQueue[0]) <= 0)
                    _sam.ReloadMissile();

                return;
            }

            if (_sam.IsActive == false)
            {
                IsActive = false;
                return;
            }

            if (_player != _sam.Owner)
                _player = _sam.Owner;

            _pseudoRandom ??= new FrontWarsRandom(_sam.Id);

            List<(FrontWarsUnit Unit, int DistSquared)> warheads = _game.Units.NearbyUnits(_sam.Tile, WarheadSearchRadius, WarheadTypes, (u, _) =>
            {
                if (u.Owner == _player || _player.IsFriendly(u.Owner))
                    return false;

                return u.TargetTile >= 0 && _game.Map.ManhattanDistance(u.TargetTile, _sam.Tile) < WarheadProtectionRadius;
            });

            FrontWarsSamTarget? single = null;

            if (warheads.Count == 0)
                single = _targeting.GetSingleTarget(tick);

            if ((single != null && single.Value.Unit.TargetedBySam == false) || warheads.Count > 0)
            {
                _sam.Launch();
                EFrontWarsUnit type = warheads.Count > 0 ? EFrontWarsUnit.MIRVWarhead : single.Value.Unit.Type;
                double roll = _pseudoRandom.Next();

                if (IsHit(type, roll) == false)
                {
                    _game.DisplayMessage(EFrontWarsMessage.SamMiss, _sam.Owner, 0, FrontWarsUnitNames.Of(type));
                }
                else if (warheads.Count > 0)
                {
                    _game.DisplayMessage(EFrontWarsMessage.SamHit, _sam.Owner, warheads.Count);

                    foreach (var (unit, _) in warheads)
                        unit.Delete();
                }
                else
                {
                    single.Value.Unit.TargetedBySam = true;
                    _game.AddExecution(new FrontWarsSamMissileExecution(_sam.Tile, _sam.Owner, _sam, single.Value.Unit, single.Value.Tile));
                }
            }
        }
    }

    public readonly struct FrontWarsSamTarget
    {
        public readonly FrontWarsUnit Unit;
        public readonly int Tile;

        public FrontWarsSamTarget(FrontWarsUnit unit, int tile)
        {
            Unit = unit;
            Tile = tile;
        }
    }

    /// <summary>
    /// SAM 표적 시스템 — 핵 궤적에서 미사일이 먼저 닿는 칸을 미리 계산해 두고 그 틱에 쏜다 `[소스] Cq3gr8XO.js:hf=class{`.
    /// </summary>
    public sealed class FrontWarsSamTargeting
    {
        private static readonly EFrontWarsUnit[] NukeTypes = { EFrontWarsUnit.AtomBomb, EFrontWarsUnit.HydrogenBomb };

        private readonly FrontWarsGame _game;
        private readonly FrontWarsUnit _sam;
        private readonly int _missileSpeed;

        /// <summary>핵 id → 쏠 틱 · 칸. 값 없음(null) = 못 잡는 핵.</summary>
        private readonly Dictionary<int, (int Tick, int Tile)?> _precomputed = new Dictionary<int, (int, int)?>();

        public FrontWarsSamTargeting(FrontWarsGame game, FrontWarsUnit sam)
        {
            _game = game;
            _sam = sam;
            _missileSpeed = game.Settings.Config.SamMissileSpeed;
        }

        private bool IsInRange(int tile)
        {
            int range = _game.Rules.SamRange(_sam.Level);
            return _game.Map.EuclideanDistanceSquared(_sam.Tile, tile) <= range * range;
        }

        private int TicksToReach(int from, int to)
        {
            return (int)System.Math.Ceiling(_game.Map.ManhattanDistance(from, to) / (double)_missileSpeed);
        }

        /// <summary>남은 궤적 중 잡을 수 있고 사거리 안이며, 미사일이 제때 닿는 첫 칸 `[소스] computeInterceptionTile(e){`.</summary>
        private (int Tick, int Tile)? ComputeInterceptionTile(FrontWarsUnit nuke)
        {
            List<FrontWarsTrajectoryPoint> trajectory = nuke.Trajectory;
            int start = nuke.TrajectoryIndex;
            int remaining = trajectory.Count - start;

            for (int i = start; i < trajectory.Count; i++)
            {
                FrontWarsTrajectoryPoint p = trajectory[i];

                if (p.Targetable == false || IsInRange(p.Tile) == false)
                    continue;

                int nukeTicks = i - start;
                int missileTicks = TicksToReach(_sam.Tile, p.Tile);
                int wait = nukeTicks - missileTicks;

                if (missileTicks < remaining && wait >= 0)
                    return (wait, p.Tile);
            }

            return null;
        }

        /// <summary>이번 틱에 쏠 핵 하나 — 수소탄 먼저 `[소스] getSingleTarget(e){`.</summary>
        public FrontWarsSamTarget? GetSingleTarget(int tick)
        {
            int range = _game.Rules.SamRange(_sam.Level) * 2;
            List<(FrontWarsUnit Unit, int DistSquared)> nukes = _game.Units.NearbyUnits(_sam.Tile, range, NukeTypes,
                (u, _) => u.Owner != _sam.Owner && _sam.Owner.IsFriendly(u.Owner) == false);

            var alive = new HashSet<int>(nukes.Select(n => n.Unit.Id));
            foreach (int id in _precomputed.Keys.ToList())
            {
                if (alive.Contains(id) == false)
                    _precomputed.Remove(id);
            }

            var ready = new List<FrontWarsSamTarget>();

            foreach (var (unit, _) in nukes)
            {
                if (_precomputed.TryGetValue(unit.Id, out (int Tick, int Tile)? known))
                {
                    if (known == null)
                        continue;

                    if (known.Value.Tick == tick)
                    {
                        ready.Add(new FrontWarsSamTarget(unit, known.Value.Tile));
                        _precomputed.Remove(unit.Id);
                        continue;
                    }

                    if (known.Value.Tick > tick)
                        continue;

                    _precomputed.Remove(unit.Id);
                }

                (int Tick, int Tile)? hit = ComputeInterceptionTile(unit);

                if (hit == null)
                    _precomputed[unit.Id] = null;
                else if (hit.Value.Tick <= 1)
                    ready.Add(new FrontWarsSamTarget(unit, hit.Value.Tile));
                else
                    _precomputed[unit.Id] = (hit.Value.Tick + tick, hit.Value.Tile);
            }

            if (ready.Count == 0)
                return null;

            // 원본 sort 비교: 수소탄이 앞 — 안정 정렬
            return ready.OrderBy(t => t.Unit.Type == EFrontWarsUnit.HydrogenBomb ? 0 : 1).First();
        }
    }
}
