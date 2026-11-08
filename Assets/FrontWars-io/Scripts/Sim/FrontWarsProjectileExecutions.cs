using System;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 포탄 —쏜 유닛 칸에서 목표 유닛으로 틱마다 3걸음. 닿으면 피해를 준다 `[소스] Cq3gr8XO.js:af=class{`.
    /// <para>쏜 유닛이 없어지면 50틱 뒤 사라진다. 목표가 없어지거나 같은 편이 되면 바로 사라진다.</para>
    /// </summary>
    public sealed class FrontWarsShellExecution : IFrontWarsExecution
    {
        private const int StepsPerTick = 3;
        private const int DamageBase = 250;
        private const int DamageRollBase = 200;
        private const int DamageRollStep = 25;

        private readonly int _spawn;
        private readonly FrontWarsPlayer _owner;
        private readonly FrontWarsUnit _ownerUnit;
        private readonly FrontWarsUnit _target;
        private FrontWarsGame _game;
        private FrontWarsStraightPathFinder _path;
        private FrontWarsRandom _random;
        private FrontWarsUnit _shell;
        private int _destroyAtTick = -1;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsShellExecution(int spawn, FrontWarsPlayer owner, FrontWarsUnit ownerUnit, FrontWarsUnit target)
        {
            _spawn = spawn;
            _owner = owner;
            _ownerUnit = ownerUnit;
            _target = target;
        }

        /// <summary>⚠ 원본은 경로 난수와 피해 난수를 같은 시드(틱)로 따로 만든다.</summary>
        public void Init(FrontWarsGame game, int tick)
        {
            _path = new FrontWarsStraightPathFinder(game.Map, new FrontWarsRandom(game.Ticks));
            _game = game;
            _random = new FrontWarsRandom(game.Ticks);
        }

        public void Tick(int tick)
        {
            _shell ??= _owner.BuildUnit(EFrontWarsUnit.Shell, _spawn);

            if (_shell.IsActive == false)
            {
                IsActive = false;
                return;
            }

            if (_target.IsActive == false || _target.Owner == _shell.Owner || (_destroyAtTick != -1 && _game.Ticks >= _destroyAtTick))
            {
                _shell.Delete(false);
                IsActive = false;
                return;
            }

            if (_destroyAtTick == -1 && _ownerUnit.IsActive == false)
                _destroyAtTick = _game.Ticks + _game.Settings.Config.ShellLifetimeTicks;

            for (int i = 0; i < StepsPerTick; i++)
            {
                bool arrived = _path.TryNextTile(_shell.Tile, _target.Tile, out int next) == false;

                if (arrived)
                {
                    IsActive = false;
                    _target.ModifyHealth(-EffectOnTarget(), _owner);
                    _shell.SetReachedTarget();
                    _shell.Delete(false);
                    return;
                }

                _shell.Move(next);
            }
        }

        /// <summary>피해 = round(표 피해/250 × (200 + 25×(1~5 난수 −1))).</summary>
        private int EffectOnTarget()
        {
            int damage = _game.Settings.Unit(EFrontWarsUnit.Shell).Damage;
            if (damage == 0)
                damage = DamageBase;

            int roll = (_random.NextInt(1, 6) - 1) * DamageRollStep + DamageRollBase;
            return (int)Math.Floor((double)damage / DamageBase * roll + 0.5);
        }
    }

    /// <summary>
    /// SAM 요격 미사일 — 발사대 칸에서 요격 칸으로 틱마다 12걸음. 닿으면 핵을 없앤다 `[소스] Cq3gr8XO.js:mf=class{`.
    /// </summary>
    public sealed class FrontWarsSamMissileExecution : IFrontWarsExecution
    {
        private readonly int _spawn;
        private readonly FrontWarsPlayer _owner;
        private readonly FrontWarsUnit _ownerUnit;
        private readonly FrontWarsUnit _target;
        private readonly int _targetTile;
        private FrontWarsGame _game;
        private FrontWarsStraightPathFinder _path;
        private FrontWarsUnit _missile;
        private int _speed;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsSamMissileExecution(int spawn, FrontWarsPlayer owner, FrontWarsUnit ownerUnit, FrontWarsUnit target, int targetTile)
        {
            _spawn = spawn;
            _owner = owner;
            _ownerUnit = ownerUnit;
            _target = target;
            _targetTile = targetTile;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _path = new FrontWarsStraightPathFinder(game.Map, new FrontWarsRandom(game.Ticks));
            _game = game;
            _speed = game.Settings.Config.SamMissileSpeed;
        }

        public void Tick(int tick)
        {
            _missile ??= _owner.BuildUnit(EFrontWarsUnit.SAMMissile, _spawn);

            if (_missile.IsActive == false)
            {
                IsActive = false;
                return;
            }

            bool nuke = _target.Type == EFrontWarsUnit.AtomBomb || _target.Type == EFrontWarsUnit.HydrogenBomb;

            if (_target.IsActive == false || _ownerUnit.IsActive == false || _target.Owner == _missile.Owner || nuke == false)
            {
                _missile.Delete(false);
                IsActive = false;
                return;
            }

            for (int i = 0; i < _speed; i++)
            {
                bool arrived = _path.TryNextTile(_missile.Tile, _targetTile, out int next) == false;

                if (arrived)
                {
                    _game.DisplayMessage(EFrontWarsMessage.SamHit, _owner, 1, FrontWarsUnitNames.Of(_target.Type));
                    IsActive = false;
                    _target.Delete(true, _owner);
                    _missile.Delete(false);
                    return;
                }

                _missile.Move(next);
            }
        }
    }
}
