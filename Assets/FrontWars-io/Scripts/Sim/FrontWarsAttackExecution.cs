using System.Linq;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 공격 실행 — 병력을 떼어 공격을 만들고 매 틱 앞줄 칸을 먹는다 `[소스] Cq3gr8XO.js:_u=class{`.
    ///
    /// <list type="number">
    /// <item>init: 대상 확인 → 우호면 취소 → (봇 아닌 둘이면) 대상이 임시 금수 · 동맹 요청 거절 → 스폰 보호 중이면 취소
    /// → 병력 떼기 → 공격 만들기 → 앞줄 칸 큐 채우기 → 들어오는 반대 공격과 상쇄 → 같은 대상 공격 합치기 → 대상 관계 −80.</item>
    /// <item>tick: 후퇴 실행이면 병력 돌려주기(플레이어 대상 25% 손실) → 이번 틱 칸 예산만큼 큐에서 꺼내 먹기.</item>
    /// </list>
    ///
    /// <para>⚠ 난수는 공격마다 시드 123 으로 새로 시작한다 `[소스] this.random=new W(123)`. 큐 우선순위는 Float32 다.</para>
    /// </summary>
    public sealed class FrontWarsAttackExecution : IFrontWarsExecution
    {
        /// <summary>공격 난수 고정 시드.</summary>
        private const int RandomSeed = 123;

        /// <summary>칸 우선순위 — (난수 0~6 + 10) × (1 − 내 칸 이웃 수 × 0.5 + 지형 가중/2) + 틱.</summary>
        private const int PriorityRandomMax = 7;
        private const int PriorityBase = 10;
        private const double PriorityOwnNeighborWeight = 0.5;
        private const double PlainsWeight = 1;
        private const double HighlandWeight = 1.5;
        private const double MountainWeight = 2;

        /// <summary>칸 예산에 더하는 난수 폭 `[소스] this.attack.borderSize()+this.random.nextInt(0,5)`.</summary>
        private const int BorderSizeRandomMax = 5;

        /// <summary>대상 관계 변화 `[소스] updateRelation(this._owner,-80)`.</summary>
        private const int TargetRelationPenalty = -80;

        /// <summary>죽은 방어자 칸 넘기기 반복 수 `[소스] for(let e=0;e<10;e++)`.</summary>
        private const int DeadDefenderPasses = 10;

        private readonly FrontWarsPlayer _owner;
        private readonly string _targetId;
        private readonly int _sourceTile;
        private readonly bool _removeTroops;
        private readonly FrontWarsTileQueue _toConquer = new FrontWarsTileQueue();
        private readonly int[] _neighborScratch = new int[4];
        private readonly int[] _innerScratch = new int[4];
        private readonly FrontWarsRandom _random = new FrontWarsRandom(RandomSeed);

        private double? _startTroops;
        private FrontWarsGame _game;
        private FrontWarsPlayer _target;
        private FrontWarsAttack _attack;
        private int _ownerSmallId = -1;
        private int _targetSmallId = -1;

        public bool IsActive { get; private set; } = true;

        public bool ActiveDuringSpawnPhase
        {
            get { return false; }
        }

        public FrontWarsPlayer Owner
        {
            get { return _owner; }
        }

        /// <summary>대상 id. null = 주인 없는 땅.</summary>
        public string TargetId
        {
            get { return _targetId; }
        }

        /// <param name="startTroops">null 이면 설정 비율(사람·네이션 /5 · 봇 /20).</param>
        /// <param name="targetId">null = 주인 없는 땅.</param>
        /// <param name="sourceTile">배로 내린 공격 시작 칸. 땅 공격은 −1.</param>
        public FrontWarsAttackExecution(double? startTroops, FrontWarsPlayer owner, string targetId, int sourceTile = -1, bool removeTroops = true)
        {
            _startTroops = startTroops;
            _owner = owner;
            _targetId = targetId;
            _sourceTile = sourceTile;
            _removeTroops = removeTroops;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            if (IsActive == false)
                return;

            _game = game;

            if (_targetId != null && game.HasPlayer(_targetId) == false)
            {
                IsActive = false;
                return;
            }

            _target = _targetId == null ? null : game.Player(_targetId);

            if (_target == _owner)
            {
                IsActive = false;
                return;
            }

            _ownerSmallId = _owner.SmallId;
            _targetSmallId = _target?.SmallId ?? 0;

            if (_target != null && _owner.IsFriendly(_target))
            {
                IsActive = false;
                return;
            }

            if (_target != null && _target.Type != EFrontWarsPlayerType.Bot && _owner.Type != EFrontWarsPlayerType.Bot)
            {
                _target.AddEmbargo(_owner, true);
                game.Diplomacy.RejectIncomingRequest(_owner, _target);
            }

            if (_target != null && game.Settings.SpawnPhaseTurns + game.Settings.Config.SpawnImmunityTicks > game.Ticks)
            {
                IsActive = false;
                return;
            }

            _startTroops ??= game.Rules.AttackAmount(_owner);

            if (_removeTroops)
            {
                _startTroops = System.Math.Min(_owner.Troops, _startTroops.Value);
                _owner.RemoveTroops(_startTroops.Value);
            }

            _attack = _owner.CreateAttack(_target, _startTroops.Value, _sourceTile);

            if (_sourceTile < 0)
                RefreshToConquer();
            else
                AddNeighbors(_sourceTile, -1);

            // 원본 for-of 는 옛 배열을 끝까지 돈다 (delete 가 배열을 새로 만든다) — 복사본으로 돈다
            foreach (FrontWarsAttack incoming in _owner.IncomingAttacks.ToArray())
            {
                if (incoming.Attacker != _target)
                    continue;

                if (incoming.Troops > _attack.Troops)
                {
                    incoming.Troops -= _attack.Troops;
                    _attack.Delete();
                    IsActive = false;
                    return;
                }

                _attack.Troops -= incoming.Troops;
                incoming.Delete();
            }

            foreach (FrontWarsAttack outgoing in _owner.OutgoingAttacks.ToArray())
            {
                if (outgoing != _attack && outgoing.Target == _attack.Target && _attack.SourceTile < 0)
                {
                    _attack.Troops += outgoing.Troops;
                    outgoing.Delete();
                }
            }

            _target?.UpdateRelation(_owner, TargetRelationPenalty);
        }

        private void RefreshToConquer()
        {
            _toConquer.Clear();
            _attack.ClearBorder();

            var border = _owner.BorderTiles.Items;
            for (int i = 0; i < border.Count; i++)
                AddNeighbors(border[i], -1);
        }

        /// <summary>남은 병력에서 percent% 를 잃고 나머지를 돌려준다 `[소스] retreat(e=0){`.</summary>
        private void Retreat(int percent = 0)
        {
            double lost = _attack.Troops * (percent / 100.0);

            if (lost != 0)
                _game.DisplayMessage(EFrontWarsMessage.AttackCancelled, _owner, lost);

            _owner.AddTroops(_attack.Troops - lost);
            _attack.Delete();
            IsActive = false;
        }

        public void Tick(int tick)
        {
            double troops = _attack.Troops;
            bool targetIsPlayer = _target != null;

            if (_attack.Retreated)
            {
                Retreat(targetIsPlayer ? _game.Settings.Config.RetreatLossPercent : 0);
                IsActive = false;
                return;
            }

            if (_attack.Retreating)
                return;

            if (_attack.IsActive == false)
            {
                IsActive = false;
                return;
            }

            if (targetIsPlayer && _owner.IsFriendly(_target))
            {
                Retreat();
                return;
            }

            double budget = _game.Rules.AttackTilesPerTick(troops, _owner, _target, _attack.BorderSize + _random.NextInt(0, BorderSizeRandomMax));
            FrontWarsTerrainMap map = _game.Map;

            while (budget > 0)
            {
                if (troops < 1)
                {
                    _attack.Delete();
                    IsActive = false;
                    return;
                }

                if (_toConquer.Count == 0)
                {
                    RefreshToConquer();
                    Retreat();
                    return;
                }

                int tile = _toConquer.Dequeue();
                _attack.RemoveBorderTile(tile);

                bool touchesOwner = false;
                int n = map.NeighborsInto(tile, _neighborScratch);

                for (int k = 0; k < n; k++)
                {
                    if (_game.OwnerSmallId(_neighborScratch[k]) == _ownerSmallId)
                    {
                        touchesOwner = true;
                        break;
                    }
                }

                if (map.IsWater(tile) || _game.OwnerSmallId(tile) != _targetSmallId || touchesOwner == false)
                    continue;

                AddNeighbors(tile, n);

                FrontWarsAttackResult result = _game.Rules.AttackLogic(_game, troops, _owner, _target, tile);
                budget -= result.TilesPerTickUsed;
                troops -= result.AttackerTroopLoss;
                _attack.Troops = troops;
                _target?.RemoveTroops(result.DefenderTroopLoss);
                _owner.Conquer(tile);
                HandleDeadDefender();
            }
        }

        /// <summary>
        /// 대상 칸인 이웃을 앞줄에 넣고 우선순위를 매겨 큐에 넣는다 `[소스] addNeighbors(e,t){`.
        /// <para>count ≥ 0 이면 이웃 임시 배열이 이미 채워져 있다 (원본이 같은 배열을 다시 쓴다).</para>
        /// </summary>
        private void AddNeighbors(int tile, int count)
        {
            FrontWarsTerrainMap map = _game.Map;
            int ticks = _game.Ticks;
            int r = count >= 0 ? count : map.NeighborsInto(tile, _neighborScratch);

            for (int e = 0; e < r; e++)
            {
                int t = _neighborScratch[e];

                if (map.IsWater(t) || map.IsImpassable(t) || _game.OwnerSmallId(t) != _targetSmallId)
                    continue;

                _attack.AddBorderTile(t);

                int ownNeighbors = 0;
                int inner = map.NeighborsInto(t, _innerScratch);
                for (int k = 0; k < inner; k++)
                {
                    if (_game.OwnerSmallId(_innerScratch[k]) == _ownerSmallId)
                        ownNeighbors++;
                }

                double weight = 0;
                switch (map.TerrainType(t))
                {
                    case EFrontWarsTerrain.Plains: weight = PlainsWeight; break;
                    case EFrontWarsTerrain.Highland: weight = HighlandWeight; break;
                    case EFrontWarsTerrain.Mountain: weight = MountainWeight; break;
                }

                double priority = (_random.NextInt(0, PriorityRandomMax) + PriorityBase) * (1 - ownNeighbors * PriorityOwnNeighborWeight + weight / 2) + ticks;
                _toConquer.Enqueue(t, priority);
            }
        }

        /// <summary>
        /// 대상 칸이 100 아래면 정복 — 골드를 넘기고, 10번 돌며 내 땅에 닿은 칸은 내가, 아니면 닿은 다른 플레이어가 가져간다 `[소스] handleDeadDefender(){`.
        /// </summary>
        private void HandleDeadDefender()
        {
            if (_target == null || _target.NumTilesOwned >= _game.Settings.Config.DeadDefenderTiles)
                return;

            _game.ConquerPlayer(_owner, _target);
            FrontWarsTerrainMap map = _game.Map;
            var scratch = new int[4];

            for (int pass = 0; pass < DeadDefenderPasses; pass++)
            {
                foreach (int tile in _target.Tiles.Snapshot())
                {
                    int n = map.NeighborsInto(tile, scratch);
                    bool touchesOwner = false;

                    for (int k = 0; k < n; k++)
                    {
                        if (_game.OwnerSmallId(scratch[k]) == _ownerSmallId)
                        {
                            touchesOwner = true;
                            break;
                        }
                    }

                    if (touchesOwner)
                    {
                        _owner.Conquer(tile);
                        continue;
                    }

                    for (int k = 0; k < n; k++)
                    {
                        FrontWarsPlayer other = _game.Owner(scratch[k]);
                        if (other != null && other != _target)
                        {
                            other.Conquer(tile);
                            break;
                        }
                    }
                }
            }
        }
    }

    /// <summary>공격 한 칸의 결과 `[소스] attackLogic(e,t,n,r,i){ … return{attackerTroopLoss,defenderTroopLoss,tilesPerTickUsed}`.</summary>
    public readonly struct FrontWarsAttackResult
    {
        public readonly double AttackerTroopLoss;
        public readonly double DefenderTroopLoss;
        public readonly double TilesPerTickUsed;

        public FrontWarsAttackResult(double attackerTroopLoss, double defenderTroopLoss, double tilesPerTickUsed)
        {
            AttackerTroopLoss = attackerTroopLoss;
            DefenderTroopLoss = defenderTroopLoss;
            TilesPerTickUsed = tilesPerTickUsed;
        }
    }

    /// <summary>
    /// 공격 취소 — 후퇴 명령 뒤 20틱에 실행 `[소스] Cq3gr8XO.js:xp=class{`.
    /// </summary>
    public sealed class FrontWarsCancelAttackExecution : IFrontWarsExecution
    {
        private readonly FrontWarsPlayer _player;
        private readonly string _attackId;
        private FrontWarsGame _game;
        private int _startTick;
        private bool _retreatOrdered;

        public bool IsActive { get; private set; } = true;

        public bool ActiveDuringSpawnPhase
        {
            get { return false; }
        }

        public FrontWarsCancelAttackExecution(FrontWarsPlayer player, string attackId)
        {
            _player = player;
            _attackId = attackId;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _game = game;
            _startTick = game.Ticks;
        }

        public void Tick(int tick)
        {
            if (_retreatOrdered == false)
            {
                _player.OrderRetreat(_attackId);
                _retreatOrdered = true;
            }

            if (_game.Ticks >= _startTick + _game.Settings.Config.CancelAttackDelayTicks)
            {
                _player.ExecuteRetreat(_attackId);
                IsActive = false;
            }
        }
    }
}
