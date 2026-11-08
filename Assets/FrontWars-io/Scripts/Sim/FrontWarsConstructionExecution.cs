namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 건설 명령 `[소스] Cq3gr8XO.js:yf=class{`.
    ///
    /// <list type="number">
    /// <item>공사 시간이 없는 종류(핵 · 전함)는 첫 틱에 바로 완성 실행을 넣는다.</item>
    /// <item>있으면 첫 틱에 자리를 확인하고 공사 중 유닛을 세운 뒤 비용을 뺀다.</item>
    /// <item>공사 중 유닛이 없어지면(빼앗김 · 파괴) 끝. 주인이 바뀌면 새 주인이 짓는다.</item>
    /// <item>남은 틱이 0 이면 공사 유닛을 치우고 비용을 돌려준 뒤 완성 실행을 넣는다 — 완성 실행이 다시 비용을 낸다.</item>
    /// </list>
    /// </summary>
    public sealed class FrontWarsConstructionExecution : IFrontWarsExecution
    {
        private FrontWarsPlayer _player;
        private readonly EFrontWarsUnit _type;
        private readonly int _tile;
        private FrontWarsGame _game;
        private FrontWarsUnit _construction;
        private long _cost;
        private int _ticksUntilComplete;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public EFrontWarsUnit Type => _type;
        public FrontWarsPlayer Player => _player;

        public FrontWarsConstructionExecution(FrontWarsPlayer player, EFrontWarsUnit type, int tile)
        {
            _player = player;
            _type = type;
            _tile = tile;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _game = game;

            if (game.Rules.IsUnitDisabled(_type) || game.Map.IsValidRef(_tile) == false)
                IsActive = false;
        }

        public void Tick(int tick)
        {
            if (_construction == null)
            {
                int duration = _game.Rules.ConstructionDuration(_type);

                if (duration < 0)
                {
                    CompleteConstruction();
                    IsActive = false;
                    return;
                }

                int spawn = _player.CanBuild(_type, _tile);

                if (spawn == FrontWarsPlayer.NoTile)
                {
                    IsActive = false;
                    return;
                }

                _construction = _player.BuildUnit(EFrontWarsUnit.Construction, spawn);
                _cost = _game.Rules.UnitCost(_type, _player);
                _player.RemoveGold(_cost);
                _construction.SetConstructionType(_type);
                _ticksUntilComplete = duration;
                return;
            }

            if (_construction.IsActive == false)
            {
                IsActive = false;
                return;
            }

            if (_player != _construction.Owner)
                _player = _construction.Owner;

            if (_ticksUntilComplete == 0)
            {
                _player = _construction.Owner;
                _construction.Delete(false);
                _player.AddGold(_cost);
                CompleteConstruction();
                IsActive = false;
                return;
            }

            _ticksUntilComplete--;
        }

        /// <summary>종류마다 완성 실행을 넣는다. ⚠ 핵 · MIRV · 전함은 명령 칸(<c>_tile</c>)을, 건물도 명령 칸을 넘긴다 (원본 그대로).</summary>
        private void CompleteConstruction()
        {
            switch (_type)
            {
                case EFrontWarsUnit.AtomBomb:
                case EFrontWarsUnit.HydrogenBomb:
                    _game.AddExecution(new FrontWarsNukeExecution(_type, _player, _tile));
                    break;
                case EFrontWarsUnit.MIRV:
                    _game.AddExecution(new FrontWarsMirvExecution(_player, _tile));
                    break;
                case EFrontWarsUnit.Warship:
                    _game.AddExecution(new FrontWarsWarshipExecution(_player, _tile));
                    break;
                case EFrontWarsUnit.Port:
                    _game.AddExecution(new FrontWarsPortExecution(_player, _tile));
                    break;
                case EFrontWarsUnit.MissileSilo:
                    _game.AddExecution(new FrontWarsMissileSiloExecution(_player, _tile));
                    break;
                case EFrontWarsUnit.DefensePost:
                    _game.AddExecution(new FrontWarsDefensePostExecution(_player, _tile));
                    break;
                case EFrontWarsUnit.SAMLauncher:
                    _game.AddExecution(new FrontWarsSamLauncherExecution(_player, _tile));
                    break;
                case EFrontWarsUnit.City:
                    _game.AddExecution(new FrontWarsCityExecution(_player, _tile));
                    break;
                case EFrontWarsUnit.Factory:
                    _game.AddExecution(new FrontWarsFactoryExecution(_player, _tile));
                    break;
            }
        }
    }

    /// <summary>
    /// 건물 업그레이드 — init 에서 조건을 보고 바로 올린다 `[소스] Cq3gr8XO.js:Bf=class{`.
    /// </summary>
    public sealed class FrontWarsUpgradeStructureExecution : IFrontWarsExecution
    {
        private readonly FrontWarsPlayer _player;
        private readonly int _unitId;

        public bool IsActive => false;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsUpgradeStructureExecution(FrontWarsPlayer player, int unitId)
        {
            _player = player;
            _unitId = unitId;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            FrontWarsUnit structure = _player.Units().Find(u => u.Id == _unitId);

            if (structure == null || _player.CanUpgradeUnit(structure.Type) == false || _player.CanUpgradeNow() == false)
                return;

            _player.UpgradeUnit(structure);
        }

        public void Tick(int tick)
        {
        }
    }
}
