using System.Collections.Generic;
using System.Linq;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 봇 AI 실행 `[소스] Cq3gr8XO.js:Qu=class{`. 스폰 단계에는 안 돈다.
    ///
    /// <list type="number">
    /// <item>생성: 봇 id 해시 시드 난수로 공격 주기(40~79) · 공격 틱 · 발동/보유/확장 비율을 뽑는다.</item>
    /// <item>공격 틱마다: 처음이면 행동을 만들고 빈 땅을 친다. 그 뒤로는 동맹 요청 답 → 연장 → 공격 고르기.</item>
    /// <item>공격 고르기: 이웃 배신자(1/3, 우호면 1/6) → 빈 땅이 닿아 있으면 빈 땅 → 무작위 적.</item>
    /// </list>
    /// </summary>
    public sealed class FrontWarsBotExecution : IFrontWarsExecution
    {
        private const int AttackRateMin = 40;
        private const int AttackRateMaxExclusive = 80;
        private const int TraitorAttackChance = 3;
        private const int FriendlyTraitorAttackChance = 6;

        private readonly FrontWarsPlayer _bot;
        private readonly FrontWarsRandom _random;
        private readonly int _attackRate;
        private readonly int _attackTick;
        private readonly double _triggerRatio;
        private readonly double _reserveRatio;
        private readonly double _expandRatio;
        private FrontWarsGame _game;
        private FrontWarsBotBehavior _behavior;
        private bool _neighborsTerraNullius = true;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsBotExecution(FrontWarsPlayer bot)
        {
            _bot = bot;
            _random = new FrontWarsRandom(FrontWarsHash.Of(bot.Id));
            _attackRate = _random.NextInt(AttackRateMin, AttackRateMaxExclusive);
            _attackTick = _random.NextInt(0, _attackRate);
            _triggerRatio = _random.NextInt(50, 60) / 100.0;
            _reserveRatio = _random.NextInt(30, 40) / 100.0;
            _expandRatio = _random.NextInt(10, 20) / 100.0;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _game = game;
        }

        public void Tick(int tick)
        {
            if (tick % _attackRate != _attackTick)
                return;

            if (_bot.IsAlive == false)
            {
                IsActive = false;
                return;
            }

            if (_behavior == null)
            {
                _behavior = new FrontWarsBotBehavior(_random, _game, _bot, _triggerRatio, _reserveRatio, _expandRatio);
                _behavior.SendAttack(null);
                return;
            }

            _behavior.HandleAllianceRequests();
            _behavior.HandleAllianceExtensionRequests();
            MaybeAttack();
        }

        private void MaybeAttack()
        {
            FrontWarsPlayer traitor = _behavior.GetNeighborTraitorToAttack();

            if (traitor != null)
            {
                int chance = _bot.IsFriendly(traitor) ? FriendlyTraitorAttackChance : TraitorAttackChance;

                if (_random.Chance(chance))
                {
                    FrontWarsAlliance alliance = _bot.AllianceWith(traitor);
                    if (alliance != null)
                        _bot.BreakAlliance(alliance);

                    _behavior.SendAttack(traitor);
                    return;
                }
            }

            if (_neighborsTerraNullius)
            {
                if (_bot.SharesBorderWith(0))
                {
                    _behavior.SendAttack(null);
                    return;
                }

                _neighborsTerraNullius = false;
            }

            _behavior.ForgetOldEnemies();
            FrontWarsPlayer enemy = _behavior.SelectRandomEnemy();

            if (enemy != null && _bot.SharesBorderWith(enemy))
                _behavior.SendAttack(enemy);
        }
    }

    /// <summary>
    /// 봇 행동 도우미 — 적 고르기 · 공격 보내기 · 동맹 답 `[소스] Cq3gr8XO.js:Xu=class{`.
    /// <para>난수는 봇 실행의 난수를 같이 쓴다 — 호출 순서가 원본과 같아야 한다.</para>
    /// </summary>
    public sealed class FrontWarsBotBehavior
    {
        /// <summary>적 기억 시간 (틱) `[소스] this.game.ticks()-(this.enemyUpdated??0)>100`.</summary>
        private const int EnemyMemoryTicks = 100;

        private const int AllianceAttackChanceDiscouraged = 200;
        private const int AllianceAttackChance = 50;
        private const int DiscouragedAttackChance = 4;
        private const int FakeHumanSkipChance = 2;
        private const int TraitorEnemyChance = 3;
        private const int NoTriggerEnemyChance = 10;
        private const int HostileEnemyChance = 2;
        private const double ExtensionSkipChance = 1.5;
        private const int AssistRelationPenalty = -20;

        /// <summary>동맹 요청 수락 기준 — 요청자 칸이 내 3배 초과거나, 요청자 동맹이 3개 미만 `[소스] function Zu(e,t){`.</summary>
        private const int AcceptTilesMultiplier = 3;
        private const int AcceptMaxAlliances = 3;

        private readonly FrontWarsRandom _random;
        private readonly FrontWarsGame _game;
        private readonly FrontWarsPlayer _player;
        private readonly double _triggerRatio;
        private readonly double _reserveRatio;
        private readonly double _expandRatio;
        private FrontWarsPlayer _enemy;
        private int? _enemyUpdated;

        public FrontWarsBotBehavior(FrontWarsRandom random, FrontWarsGame game, FrontWarsPlayer player, double triggerRatio, double reserveRatio, double expandRatio)
        {
            _random = random;
            _game = game;
            _player = player;
            _triggerRatio = triggerRatio;
            _reserveRatio = reserveRatio;
            _expandRatio = expandRatio;
        }

        public void HandleAllianceRequests()
        {
            foreach (FrontWarsAllianceRequest request in _player.IncomingAllianceRequests())
            {
                if (ShouldAcceptAllianceRequest(_player, request))
                    request.Accept();
                else
                    request.Reject();
            }
        }

        public static bool ShouldAcceptAllianceRequest(FrontWarsPlayer player, FrontWarsAllianceRequest request)
        {
            if (player.Relation(request.Requestor) < EFrontWarsRelation.Neutral || request.Requestor.IsTraitor)
                return false;

            return request.Requestor.NumTilesOwned > player.NumTilesOwned * AcceptTilesMultiplier
                   || request.Requestor.Alliances().Count < AcceptMaxAlliances;
        }

        public void HandleAllianceExtensionRequests()
        {
            foreach (FrontWarsAlliance alliance in _player.Alliances())
            {
                if (alliance.OnlyOneAgreedToExtend == false)
                    continue;

                FrontWarsPlayer other = alliance.Other(_player);

                if (_player.Type == EFrontWarsPlayerType.FakeHuman && _player.Relation(other) == EFrontWarsRelation.Neutral && _random.Chance(ExtensionSkipChance) == false)
                    continue;

                _game.AddExecution(new FrontWarsAllianceExtensionExecution(_player, other.Id));
            }
        }

        private void Emoji(FrontWarsPlayer recipient, int emojiIndex)
        {
            if (recipient.Type == EFrontWarsPlayerType.Human)
                _game.AddExecution(new FrontWarsEmojiExecution(_player, recipient.Id, emojiIndex));
        }

        private void SetNewEnemy(FrontWarsPlayer enemy, bool force = false)
        {
            if (enemy != null && force == false && ShouldAttack(enemy) == false)
                return;

            _enemy = enemy;
            _enemyUpdated = _game.Ticks;
        }

        private bool ShouldAttack(FrontWarsPlayer other)
        {
            if (_player.IsOnSameTeam(other))
                return false;

            bool attack = AttackChance(other);

            if (attack && _player.IsAlliedWith(other))
            {
                Betray(other);
                return true;
            }

            return attack;
        }

        private void Betray(FrontWarsPlayer other)
        {
            FrontWarsAlliance alliance = _player.AllianceWith(other);
            if (alliance != null)
                _player.BreakAlliance(alliance);
        }

        private bool AttackChance(FrontWarsPlayer other)
        {
            if (_player.IsAlliedWith(other))
                return ShouldDiscourageAttack(other) ? _random.Chance(AllianceAttackChanceDiscouraged) : _random.Chance(AllianceAttackChance);

            return ShouldDiscourageAttack(other) == false || _random.Chance(DiscouragedAttackChance);
        }

        /// <summary>배신자가 아니고, 쉬움 · 보통 난이도에서 사람이면 공격을 줄인다.</summary>
        private bool ShouldDiscourageAttack(FrontWarsPlayer other)
        {
            if (other.IsTraitor)
                return false;

            EFrontWarsDifficulty d = _game.Settings.Difficulty;
            return d != EFrontWarsDifficulty.Hard && d != EFrontWarsDifficulty.Impossible && other.Type == EFrontWarsPlayerType.Human;
        }

        public void ClearEnemy()
        {
            _enemy = null;
        }

        public void ForgetOldEnemies()
        {
            if (_game.Ticks - (_enemyUpdated ?? 0) > EnemyMemoryTicks)
                ClearEnemy();
        }

        private bool HasReserveRatioTroops()
        {
            return _player.Troops / _game.Rules.MaxTroops(_player) >= _reserveRatio;
        }

        private bool HasTriggerRatioTroops()
        {
            return _player.Troops / _game.Rules.MaxTroops(_player) >= _triggerRatio;
        }

        /// <summary>들어오는 공격 중 병력이 가장 큰 공격자를 적으로 (먼저 온 쪽 우선).</summary>
        private void CheckIncomingAttacks()
        {
            double best = 0;
            FrontWarsPlayer attacker = null;

            foreach (FrontWarsAttack a in _player.IncomingAttacks)
            {
                if (a.Troops <= best)
                    continue;

                best = a.Troops;
                attacker = a.Attacker;
            }

            if (attacker != null)
                SetNewEnemy(attacker, true);
        }

        public FrontWarsPlayer GetNeighborTraitorToAttack()
        {
            List<FrontWarsPlayer> traitors = _player.Neighbors().FindAll(p => p != null && p.IsTraitor);
            return traitors.Count > 0 ? _random.RandElement(traitors) : null;
        }

        public void AssistAllies()
        {
            foreach (FrontWarsPlayer ally in _player.Allies())
            {
                List<FrontWarsPlayer> targets = ally.Targets();
                if (targets.Count == 0)
                    continue;

                if (_player.Relation(ally) < EFrontWarsRelation.Friendly)
                {
                    Emoji(ally, _random.RandElement(FrontWarsEmojis.RelationTooLow));
                    continue;
                }

                foreach (FrontWarsPlayer target in targets)
                {
                    if (target == _player)
                    {
                        Emoji(ally, _random.RandElement(FrontWarsEmojis.TargetMe));
                        continue;
                    }

                    if (_player.IsAlliedWith(target))
                    {
                        Emoji(ally, _random.RandElement(FrontWarsEmojis.TargetAlly));
                        continue;
                    }

                    _player.UpdateRelation(ally, AssistRelationPenalty);
                    SetNewEnemy(target);
                    Emoji(ally, _random.RandElement(FrontWarsEmojis.Assist));
                    return;
                }
            }
        }

        /// <summary>네이션 쪽에서 쓰는 적 고르기 `[소스] selectEnemy(e){`. candidates 는 부르는 쪽이 준 후보.</summary>
        public FrontWarsPlayer SelectEnemy(List<FrontWarsPlayer> candidates)
        {
            if (_enemy == null)
            {
                if (HasReserveRatioTroops() == false || (HasTriggerRatioTroops() == false && _random.Chance(NoTriggerEnemyChance) == false))
                    return null;

                List<FrontWarsPlayer> bots = _player.Neighbors().FindAll(p => p != null && p.Type == EFrontWarsPlayerType.Bot);

                if (bots.Count > 0)
                {
                    FrontWarsPlayer weakest = null;
                    double density = double.PositiveInfinity;

                    foreach (FrontWarsPlayer b in bots)
                    {
                        double d = b.Troops / (double)b.NumTilesOwned;
                        if (d < density)
                        {
                            density = d;
                            weakest = b;
                        }
                    }

                    if (weakest != null)
                        SetNewEnemy(weakest);
                }

                if (_enemy == null)
                    CheckIncomingAttacks();

                if (_enemy == null && _random.Chance(HostileEnemyChance))
                {
                    var sorted = _player.AllRelationsSorted();
                    if (sorted.Count > 0 && sorted[0].Relation == EFrontWarsRelation.Hostile)
                        SetNewEnemy(sorted[0].Player);
                }

                if (_enemy == null && candidates.Count > 0)
                    SetNewEnemy(candidates[0]);

                if (_enemy == null && candidates.Count > 0)
                    SetNewEnemy(_random.RandElement(candidates));
            }

            return EnemySanityCheck();
        }

        public FrontWarsPlayer SelectRandomEnemy()
        {
            if (_enemy == null)
            {
                if (HasTriggerRatioTroops() == false)
                    return null;

                FrontWarsPlayer[] neighbors = _random.Shuffle(_player.Neighbors());

                foreach (FrontWarsPlayer n in neighbors)
                {
                    if (n == null)
                        continue;

                    if (_player.IsFriendly(n))
                        continue;

                    if (n.Type == EFrontWarsPlayerType.FakeHuman && _random.Chance(FakeHumanSkipChance))
                        continue;

                    SetNewEnemy(n);
                }

                if (_enemy == null)
                    CheckIncomingAttacks();

                if (_enemy == null)
                {
                    FrontWarsPlayer traitor = GetNeighborTraitorToAttack();
                    if (traitor != null && _player.IsFriendly(traitor) == false && _random.Chance(TraitorEnemyChance))
                        SetNewEnemy(traitor);
                }
            }

            return EnemySanityCheck();
        }

        private FrontWarsPlayer EnemySanityCheck()
        {
            if (_enemy != null && _player.IsFriendly(_enemy))
                ClearEnemy();

            return _enemy;
        }

        /// <summary>
        /// 최대 병력 × (플레이어면 보유 비율 · 빈 땅이면 확장 비율) 을 남기고 나머지로 친다. 1 미만이면 안 친다 `[소스] sendAttack(e){`.
        /// </summary>
        /// <param name="target">null = 주인 없는 땅.</param>
        public void SendAttack(FrontWarsPlayer target)
        {
            if (target != null && _player.IsFriendly(target))
                return;

            double keep = _game.Rules.MaxTroops(_player) * (target != null ? _reserveRatio : _expandRatio);
            double troops = _player.Troops - keep;

            if (troops < 1)
                return;

            _game.AddExecution(new FrontWarsAttackExecution(troops, _player, target?.Id));
        }
    }
}
