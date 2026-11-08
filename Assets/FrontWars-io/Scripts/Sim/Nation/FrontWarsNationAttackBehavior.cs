using System;
using System.Collections.Generic;
using System.Linq;
using JinHyung.Data;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 네이션 공격 도우미 `[소스] Cq3gr8XO.js:Lf=class{`. 봇 행동(<see cref="FrontWarsBotBehavior"/>)과 다른 클래스다.
    ///
    /// <list type="number">
    /// <item>국경 정리: 국경에 닿은 플레이어를 우호 / 적으로 나눠 병력 적은 순.</item>
    /// <item>빈 땅이 닿아 있으면 빈 땅 공격, 보냈으면 끝.</item>
    /// <item>적이 없으면 1/5 기회 수송선. 있으면 1/10 적 대상 기회 수송선 후 끝, 아니면 적에게 동맹 요청 검토.</item>
    /// <item>행동 목록(난이도 표 Plays)을 순서대로 — 처음 성공한 행동에서 끝.</item>
    /// </list>
    /// <para>대상 <c>null</c> = 주인 없는 땅 (원본 terraNullius). 팀 모드 분기는 싱글 FFA 라 옮기지 않았다 — 팀 분기에서만 굴리는 난수도 없다.</para>
    /// </summary>
    public sealed class FrontWarsNationAttackBehavior
    {
        private const string PlayClaimFallout = "claimFallout";
        private const string PlayPurgeBots = "purgeBots";
        private const string PlayStrikeBack = "strikeBack";
        private const string PlayHonorAllies = "honorAllies";
        private const string PlayPunishTraitor = "punishTraitor";
        private const string PlayConsumeAfk = "consumeAfk";
        private const string PlayTurnOnAlly = "turnOnAlly";
        private const string PlayJoinPileOn = "joinPileOn";
        private const string PlaySettleGrudge = "settleGrudge";
        private const string PlayFinishCrippled = "finishCrippled";
        private const string PlayPickOnWeakest = "pickOnWeakest";
        private const string PlayRaidNearestIsland = "raidNearestIsland";
        private const string PlaySupportTeam = "supportTeam";

        private static readonly string[] KnownPlays =
        {
            PlayClaimFallout, PlayPurgeBots, PlayStrikeBack, PlayHonorAllies, PlayPunishTraitor, PlayConsumeAfk, PlayTurnOnAlly,
            PlayJoinPileOn, PlaySettleGrudge, PlayFinishCrippled, PlayPickOnWeakest, PlayRaidNearestIsland, PlaySupportTeam
        };

        /// <summary>기회 수송선 확률 — 적 없음 1/5 · 적 있음 1/10 `[소스] this.random.chance(5)&&this.launchOpportunityBoat() · this.random.chance(10)`.</summary>
        private const int OpportunityBoatChanceNoEnemies = 5;
        private const int OpportunityBoatChanceWithEnemies = 10;

        /// <summary>공격 문턱 미달이어도 1/10 확률로 행동 목록을 돈다 `[소스] !this.reachedRatio(this.triggerRatio)&&!this.random.chance(10)`.</summary>
        private const int PlaybookBelowTriggerChance = 10;

        /// <summary>수송선 · 기회 수송선 병력 = 병력/5 · 강제 공격 = 병력/2 `[소스] this.player.troops()/5 · this.player.troops()/2`.</summary>
        private const double BoatTroopsDivisor = 5;
        private const double ForceAttackDivisor = 2;

        /// <summary>FFA 에서 이만큼 센 상대는 건드리지 않는다 `[소스] punishTraitor · consumeAfk · joinPileOn · settleGrudge · finishCrippled`.</summary>
        private const double TraitorTroopsMul = 1.2;
        private const double AfkTroopsMul = 3;
        private const double PileOnTroopsMul = 1.2;
        private const double PileOnIncomingShare = 0.5;
        private const double GrudgeTroopsMul = 3;
        private const double CrippledMaxTroopsShare = 0.15;
        private const double CrippledTroopsMul = 1.2;

        /// <summary>동맹을 도우면 그 동맹과 관계 −20 `[소스] this.player.updateRelation(e,-20)`.</summary>
        private const double AssistRelationPenalty = -20;

        /// <summary>기회 수송선 목표 — 해안 칸 ±150 · 500번 `[소스] rollBoatTarget(e,t,n){…for(let e=0;e<500;e++){let e=this.random.nextInt(r-150,r+150)`.</summary>
        private const int BoatRollRadius = 150;
        private const int BoatRollAttempts = 500;

        /// <summary>빈 땅 수송선 — 해안 10칸마다 · 5칸 앞 `[소스] sendBoatAttackToNearbyTerraNullius(){…t+=10…n+e*5`.</summary>
        private const int NeutralBoatShoreStep = 10;
        private const int NeutralBoatReach = 5;

        /// <summary>섬 습격 후보 2명 · 2명이면 1/3 로 둘째 `[소스] r.length>=2 · this.random.chance(3)?r[1]:r[0]`.</summary>
        private const int IslandCandidates = 2;
        private const int IslandSecondChance = 3;

        /// <summary>사람을 칠까 — 쉬움 난수 0~3 이 0 · 보통 1/4 이면 안 친다 `[소스] shouldAttack(e){…this.random.nextInt(0,4)===0:t!==d.Medium||!this.random.chance(4)`.</summary>
        private const int EasyHumanAttackRoll = 4;
        private const int MediumHumanSkipChance = 4;

        /// <summary>너무 약함 = 보낼 병력 &lt; 대상 병력×0.2 `[소스] isAttackTooWeak(e,t){…e<t.troops()*.2`.</summary>
        private const double TooWeakShare = 0.2;

        /// <summary>봇 예산 — 봇 병력×4 로 자르고 예산이 봇 병력×2 미만이면 0 `[소스] botAttackBudget(e,t){`.</summary>
        private const double BotBudgetTroopsMul = 4;
        private const double BotBudgetMinTroopsMul = 2;

        /// <summary>봇 소탕 폭 — 보통은 1/2 로 최소 · 최대 `[소스] botSweepWidth(){…case d.Medium:return this.random.chance(2)?1:2`.</summary>
        private const int BotSweepPickChance = 2;

        /// <summary>구조물로 치는 종류 `[소스] Ff=new Set([S.City,S.Port,S.Factory,S.DefensePost,S.SAMLauncher,S.MissileSilo])`.</summary>
        private static readonly EFrontWarsUnit[] StructureTypes =
        {
            EFrontWarsUnit.City, EFrontWarsUnit.Port, EFrontWarsUnit.Factory, EFrontWarsUnit.DefensePost, EFrontWarsUnit.SAMLauncher, EFrontWarsUnit.MissileSilo
        };

        /// <summary>빈 땅 수송선 방향 — 위 · 아래 · 왼쪽 · 오른쪽 `[소스] [[0,-1],[0,1],[-1,0],[1,0]]`.</summary>
        private static readonly int[] CardinalDx = { 0, 0, -1, 1 };
        private static readonly int[] CardinalDy = { -1, 1, 0, 0 };

        private static readonly List<FrontWarsPlayer> NoPlayers = new List<FrontWarsPlayer>();

        private readonly FrontWarsRandom _random;
        private readonly FrontWarsGame _game;
        private readonly FrontWarsPlayer _player;
        private readonly double _triggerRatio;
        private readonly double _reserveRatio;
        private readonly double _expandRatio;
        private readonly FrontWarsNationAllianceBehavior _alliance;
        private readonly FrontWarsNationEmojiBehavior _emoji;
        private readonly FrontWarsDifficultyData _row;
        private readonly List<string> _plays;
        private readonly int[] _neighborScratch = new int[4];
        private List<FrontWarsPlayer> _friends = new List<FrontWarsPlayer>();
        private List<FrontWarsPlayer> _enemies = new List<FrontWarsPlayer>();
        private List<int> _shoreCache = new List<int>();
        private int _shoreCacheTick = -1;
        private double _botAttackTroopsSent;

        public FrontWarsNationAttackBehavior(FrontWarsRandom random, FrontWarsGame game, FrontWarsPlayer player,
            double triggerRatio, double reserveRatio, double expandRatio,
            FrontWarsNationAllianceBehavior alliance, FrontWarsNationEmojiBehavior emoji)
        {
            _random = random;
            _game = game;
            _player = player;
            _triggerRatio = triggerRatio;
            _reserveRatio = reserveRatio;
            _expandRatio = expandRatio;
            _alliance = alliance;
            _emoji = emoji;
            _row = game.Settings.DifficultyRow;
            _plays = ParsePlays(_row.Plays);
        }

        /// <summary>행동 목록 문자열(`;` 구분)을 읽는다. 모르는 이름이면 멈춘다 — 표 오타가 조용히 행동을 빼지 않게.</summary>
        private static List<string> ParsePlays(string plays)
        {
            var result = new List<string>();

            foreach (string raw in (plays ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string play = raw.Trim();

                if (Array.IndexOf(KnownPlays, play) < 0)
                    throw new InvalidOperationException($"네이션 행동 이름을 모른다: {play}");

                result.Add(play);
            }

            return result;
        }

        /// <summary>`[소스] isFFA(){return this.game.config().gameConfig().gameMode===b.FFA}`</summary>
        private bool IsFFA
        {
            get { return _game.Settings.IsTeamMode == false; }
        }

        private bool IsTransportDisabledOrFull
        {
            get
            {
                return _game.Rules.IsUnitDisabled(EFrontWarsUnit.TransportShip)
                       || _player.UnitCount(EFrontWarsUnit.TransportShip) >= _game.Settings.Config.BoatMaxNumber;
            }
        }

        /// <summary>내 국경의 해안 칸 — 한 틱에 한 번 모은다 `[소스] myShoreTiles(){`.</summary>
        private List<int> MyShoreTiles()
        {
            int ticks = _game.Ticks;

            if (_shoreCacheTick != ticks)
            {
                _shoreCacheTick = ticks;
                var shores = new List<int>();

                foreach (int tile in _player.BorderTiles.Items)
                {
                    if (_game.Map.IsShore(tile))
                        shores.Add(tile);
                }

                _shoreCache = shores;
            }

            return _shoreCache;
        }

        /// <summary>공격 틱마다 `[소스] maybeAttack(){`.</summary>
        public void MaybeAttack()
        {
            RefreshBorderContext();

            if (BordersCleanTerraNullius() && SendAttack(null))
                return;

            if (_enemies.Count == 0)
            {
                if (_random.Chance(OpportunityBoatChanceNoEnemies))
                    LaunchOpportunityBoat(NoPlayers);
            }
            else
            {
                if (_random.Chance(OpportunityBoatChanceWithEnemies))
                {
                    LaunchOpportunityBoat(_enemies);
                    return;
                }

                if (_game.Settings.DisableAlliances == false)
                    _alliance?.MaybeSendAllianceRequests(_enemies);
            }

            RunPlaybook();
        }

        /// <summary>국경 칸 옆 땅(지나갈 수 있는)의 주인 + 이웃 플레이어를 처음 만난 순서로 모아 우호 / 적으로 나누고 병력 오름차순 (안정) `[소스] refreshBorderContext(){`.</summary>
        private void RefreshBorderContext()
        {
            FrontWarsTerrainMap map = _game.Map;
            int mine = _player.SmallId;
            var seen = new HashSet<FrontWarsPlayer>();
            var found = new List<FrontWarsPlayer>();
            IReadOnlyList<int> border = _player.BorderTiles.Items;

            for (int i = 0; i < border.Count; i++)
            {
                int n = map.NeighborsInto(border[i], _neighborScratch);

                for (int k = 0; k < n; k++)
                {
                    int tile = _neighborScratch[k];
                    if (map.IsLand(tile) == false || map.IsImpassable(tile))
                        continue;

                    int ownerId = _game.OwnerSmallId(tile);
                    if (ownerId == mine)
                        continue;

                    FrontWarsPlayer owner = _game.PlayerBySmallId(ownerId);
                    if (owner != null && seen.Add(owner))
                        found.Add(owner);
                }
            }

            foreach (FrontWarsPlayer neighbor in _player.Neighbors())
            {
                if (neighbor != null && seen.Add(neighbor))
                    found.Add(neighbor);
            }

            var friends = new List<FrontWarsPlayer>();
            var enemies = new List<FrontWarsPlayer>();

            foreach (FrontWarsPlayer p in found)
            {
                if (_player.IsFriendly(p))
                    friends.Add(p);
                else
                    enemies.Add(p);
            }

            _friends = friends.OrderBy(p => p.Troops).ToList();
            _enemies = enemies.OrderBy(p => p.Troops).ToList();
        }

        /// <summary>국경 옆에 낙진 없는 빈 땅이 있거나 이웃에 빈 땅이 있다 `[소스] bordersCleanTerraNullius(){`.</summary>
        private bool BordersCleanTerraNullius()
        {
            FrontWarsTerrainMap map = _game.Map;
            IReadOnlyList<int> border = _player.BorderTiles.Items;

            for (int i = 0; i < border.Count; i++)
            {
                int n = map.NeighborsInto(border[i], _neighborScratch);

                for (int k = 0; k < n; k++)
                {
                    int tile = _neighborScratch[k];
                    if (map.IsLand(tile) && map.IsImpassable(tile) == false && _game.HasOwner(tile) == false && _game.HasFallout(tile) == false)
                        return true;
                }
            }

            foreach (FrontWarsPlayer neighbor in _player.Neighbors())
            {
                if (neighbor == null)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 구조물 가진 이웃 봇 소탕 → 남길 비율 → 공격 문턱(미달이면 1/10) → 행동 목록 `[소스] runPlaybook(){`.
        /// </summary>
        private void RunPlaybook()
        {
            if (AnyNeighborBotHoldsStructures() && PurgeBots())
                return;

            if (ReachedRatio(_reserveRatio) == false)
                return;

            if (ReachedRatio(_triggerRatio) == false && _random.Chance(PlaybookBelowTriggerChance) == false)
                return;

            foreach (string play in _plays)
            {
                if (RunPlay(play))
                    return;
            }
        }

        private bool RunPlay(string play)
        {
            switch (play)
            {
                case PlayClaimFallout: return ClaimFallout();
                case PlayPurgeBots: return PurgeBots();
                case PlayStrikeBack: return StrikeBack();
                case PlayHonorAllies: return HonorAllies();
                case PlayPunishTraitor: return PunishTraitor();
                case PlayConsumeAfk: return ConsumeAfk();
                case PlayTurnOnAlly: return TurnOnAlly();
                case PlayJoinPileOn: return JoinPileOn();
                case PlaySettleGrudge: return SettleGrudge();
                case PlayFinishCrippled: return FinishCrippled();
                case PlayPickOnWeakest: return PickOnWeakest();
                case PlayRaidNearestIsland: return RaidNearestIsland();
                case PlaySupportTeam: return SupportTeam();
                default: throw new InvalidOperationException($"네이션 행동 이름을 모른다: {play}");
            }
        }

        private bool ReachedRatio(double ratio)
        {
            return _player.Troops >= _game.Rules.MaxTroops(_player) * ratio;
        }

        // ── 행동

        private bool ClaimFallout()
        {
            if (_game.Rules.IsUnitDisabled(EFrontWarsUnit.MissileSilo))
                return false;

            FrontWarsTerrainMap map = _game.Map;
            int[] scratch = new int[4];

            foreach (int border in _player.BorderTiles.Items)
            {
                int n = map.NeighborsInto(border, scratch);

                for (int k = 0; k < n; k++)
                {
                    int tile = scratch[k];
                    if (map.IsLand(tile) && _game.HasOwner(tile) == false && _game.HasFallout(tile))
                        return SendAttack(null);
                }
            }

            return false;
        }

        private bool StrikeBack()
        {
            FrontWarsPlayer attacker = FindIncomingAttackPlayer();
            return attacker != null && SendAttack(attacker, true);
        }

        /// <summary>비우호 이웃 봇을 (구조물 가진 봇 먼저 → 병력/땅 오름차순) 앞에서 폭만큼 친다. 보낸 병력이 있으면 성공 `[소스] purgeBots(){`.</summary>
        private bool PurgeBots()
        {
            var bots = new List<FrontWarsPlayer>();

            foreach (FrontWarsPlayer neighbor in _player.Neighbors())
            {
                if (neighbor != null && neighbor.Type == EFrontWarsPlayerType.Bot && _player.IsFriendly(neighbor) == false)
                    bots.Add(neighbor);
            }

            if (bots.Count == 0)
                return false;

            _botAttackTroopsSent = 0;

            List<FrontWarsPlayer> sorted = bots
                .OrderBy(b => HoldsStructures(b) ? 0 : 1)
                .ThenBy(b => b.Troops / (double)b.NumTilesOwned)
                .ToList();

            int count = Math.Min(sorted.Count, BotSweepWidth());

            for (int i = 0; i < count; i++)
                SendAttack(sorted[i]);

            return _botAttackTroopsSent > 0;
        }

        private int BotSweepWidth()
        {
            if (_row.BotSweepMin == _row.BotSweepMax)
                return _row.BotSweepMin;

            return _random.Chance(BotSweepPickChance) ? _row.BotSweepMin : _row.BotSweepMax;
        }

        private static bool HoldsStructures(FrontWarsPlayer player)
        {
            foreach (FrontWarsUnit unit in player.AllUnits)
            {
                if (Array.IndexOf(StructureTypes, unit.Type) >= 0)
                    return true;
            }

            return false;
        }

        private bool AnyNeighborBotHoldsStructures()
        {
            foreach (FrontWarsPlayer neighbor in _player.Neighbors())
            {
                if (neighbor != null && neighbor.Type == EFrontWarsPlayerType.Bot && _player.IsFriendly(neighbor) == false && HoldsStructures(neighbor))
                    return true;
            }

            return false;
        }

        /// <summary>타겟을 찍은 동맹마다 — 관계가 낮거나 · 나를 찍었거나 · 우호를 찍었으면 이모지만, 아니면 그 타겟을 친다 `[소스] honorAllies(){`.</summary>
        private bool HonorAllies()
        {
            if (_game.Settings.DisableAlliances)
                return false;

            foreach (FrontWarsPlayer ally in _player.Allies())
            {
                if (ally.Targets().Count == 0)
                    continue;

                if (_player.Relation(ally) < EFrontWarsRelation.Friendly)
                {
                    _emoji?.SendAssistEmoji(ally, EFrontWarsNationAssistEmoji.RelationTooLow);
                    continue;
                }

                foreach (FrontWarsPlayer target in ally.Targets())
                {
                    if (target == _player)
                    {
                        _emoji?.SendAssistEmoji(ally, EFrontWarsNationAssistEmoji.TargetMe);
                        continue;
                    }

                    if (_player.IsFriendly(target))
                    {
                        _emoji?.SendAssistEmoji(ally, EFrontWarsNationAssistEmoji.TargetAlly);
                        continue;
                    }

                    if (SendAttack(target))
                    {
                        _player.UpdateRelation(ally, AssistRelationPenalty);
                        _emoji?.SendAssistEmoji(ally, EFrontWarsNationAssistEmoji.Accept);
                        return true;
                    }
                }
            }

            return false;
        }

        private bool PunishTraitor()
        {
            if (_game.Settings.DisableAlliances)
                return false;

            foreach (FrontWarsPlayer enemy in _enemies)
            {
                if (enemy.IsTraitor && (IsFFA && enemy.Troops >= _player.Troops * TraitorTroopsMul) == false)
                    return SendAttack(enemy);
            }

            return false;
        }

        private bool ConsumeAfk()
        {
            foreach (FrontWarsPlayer enemy in _enemies)
            {
                if (enemy.IsDisconnected && (IsFFA && enemy.Troops >= _player.Troops * AfkTroopsMul) == false)
                    return SendAttack(enemy);
            }

            return false;
        }

        private bool TurnOnAlly()
        {
            if (_game.Settings.DisableAlliances || _alliance == null)
                return false;

            int neighborCount = _friends.Count + _enemies.Count;

            foreach (FrontWarsPlayer friend in _friends)
            {
                if (_alliance.MaybeBetray(friend, neighborCount))
                    return SendAttack(friend, true);
            }

            return false;
        }

        private bool JoinPileOn()
        {
            foreach (FrontWarsPlayer enemy in _enemies)
            {
                if (IsFFA && enemy.Troops > _player.Troops * PileOnTroopsMul)
                    continue;

                double incoming = 0;
                foreach (FrontWarsAttack attack in enemy.IncomingAttacks)
                    incoming += attack.Troops;

                if (incoming > enemy.Troops * PileOnIncomingShare)
                    return SendAttack(enemy);
            }

            return false;
        }

        private bool SettleGrudge()
        {
            foreach (var (other, relation) in _player.AllRelationsSorted())
            {
                if (relation != EFrontWarsRelation.Hostile)
                    continue;

                if (_player.IsFriendly(other) == false && (IsFFA && other.Troops > _player.Troops * GrudgeTroopsMul) == false)
                    return SendAttack(other);
            }

            return false;
        }

        private bool FinishCrippled()
        {
            foreach (FrontWarsPlayer enemy in _enemies)
            {
                if (enemy.Troops >= _game.Rules.MaxTroops(enemy) * CrippledMaxTroopsShare)
                    continue;

                if (IsFFA && enemy.Troops >= _player.Troops * CrippledTroopsMul)
                    continue;

                return SendAttack(enemy);
            }

            return false;
        }

        private bool PickOnWeakest()
        {
            if (_enemies.Count == 0)
                return false;

            FrontWarsPlayer weakest = _enemies[0];

            if (IsFFA && weakest.Troops >= _player.Troops)
                return false;

            return SendAttack(weakest);
        }

        private bool RaidNearestIsland()
        {
            if (_enemies.Count > 0)
                return false;

            FrontWarsPlayer target = FindNearestIslandEnemy();
            return target != null && SendAttack(target);
        }

        /// <summary>
        /// 싸우는 중인 팀원에게 병력을 보낸다 `[소스] supportTeam(){`.
        /// <list type="number">
        /// <item>팀전 · 병력 기부 켜짐 · 승자 없음이어야 한다 (공개 판 금지 조건은 싱글이라 늘 통과). 아니면 난수 없이 거짓.</item>
        /// <item>쉬움은 안 한다 · 보통 1/4 · 어려움 1/2 · 불가능은 난수 없이 한다.</item>
        /// <item>공격을 받거나 거는 중이고 기부할 수 있는 팀원 중 병력 ÷ 최대 병력이 가장 낮은 쪽(같으면 먼저)에게 (병력 − 최대×보유 비율)을 보낸다. 1 미만이면 안 보낸다.</item>
        /// </list>
        /// </summary>
        private bool SupportTeam()
        {
            if (_game.Settings.IsTeamMode == false || _game.Settings.DonateTroops == false || _game.HasWinner)
                return false;

            switch (_game.Settings.Difficulty)
            {
                case EFrontWarsDifficulty.Easy:
                    return false;
                case EFrontWarsDifficulty.Medium:
                    if (_random.Chance(SupportTeamMediumChance) == false)
                        return false;
                    break;
                case EFrontWarsDifficulty.Hard:
                    if (_random.Chance(SupportTeamHardChance) == false)
                        return false;
                    break;
            }

            FrontWarsPlayer weakest = null;
            double lowest = double.PositiveInfinity;

            foreach (FrontWarsPlayer mate in _game.Players())
            {
                if (_player.IsOnSameTeam(mate) == false
                    || (mate.IncomingAttacks.Count == 0 && mate.OutgoingAttacks.Count == 0)
                    || _player.CanDonateTroops(mate) == false)
                    continue;

                double ratio = mate.Troops / Math.Max(_game.Rules.MaxTroops(mate), 1);
                if (ratio < lowest)
                {
                    lowest = ratio;
                    weakest = mate;
                }
            }

            if (weakest == null)
                return false;

            double troops = _player.Troops - _game.Rules.MaxTroops(_player) * _reserveRatio;
            if (troops < 1)
                return false;

            _game.AddExecution(new FrontWarsDonateTroopsExecution(_player, weakest.Id, troops));
            return true;
        }

        /// <summary>팀 돕기 확률 — 보통 1/4 · 어려움 1/2 `[소스] case d.Medium:if(!this.random.chance(4))return!1;break;case d.Hard:if(!this.random.chance(2))return!1`.</summary>
        private const int SupportTeamMediumChance = 4;
        private const int SupportTeamHardChance = 2;

        // ── 수송선

        /// <summary>
        /// 무작위 해안 칸에서 기회 수송선 `[소스] launchOpportunityBoat(e=[]){`.
        /// <para>봇 · 빈 땅 목표를 먼저 굴리고, 없으면 비우호 목표. 병력 = min(병력/5, 플레이어면 보내기 상한).</para>
        /// </summary>
        private void LaunchOpportunityBoat(IReadOnlyList<FrontWarsPlayer> exclude)
        {
            if (IsTransportDisabledOrFull)
                return;

            List<int> shores = MyShoreTiles();
            if (shores.Count == 0)
                return;

            int from = _random.RandElement(shores);
            int tile = RollBoatTarget(from, exclude, true);

            if (tile < 0)
                tile = RollBoatTarget(from, exclude, false);

            if (tile < 0)
                return;

            FrontWarsPlayer owner = _game.Owner(tile);
            double cap = owner != null ? TroopSendCap() : double.PositiveInfinity;
            double troops = Math.Min(_player.Troops / BoatTroopsDivisor, cap);

            if (troops < 1 || (owner != null && IsAttackTooWeak(troops, owner)))
                return;

            _game.AddExecution(new FrontWarsTransportShipExecution(_player, owner?.Id, tile, troops));
        }

        /// <summary>
        /// 해안 칸 ±150 난수 칸을 500번 — 땅 · 지나갈 수 있음 · 내 땅 아님 · 실패한 플레이어 아님 · 제외 목록 아님 · (FFA) 나보다 세지 않음,
        /// botsOnly 면 빈 땅이나 봇 · 아니면 빈 땅이나 비우호. 배를 보낼 수 있으면 그 칸 `[소스] rollBoatTarget(e,t,n){`.
        /// </summary>
        private int RollBoatTarget(int from, IReadOnlyList<FrontWarsPlayer> exclude, bool botsOnly)
        {
            FrontWarsTerrainMap map = _game.Map;
            int x = map.X(from), y = map.Y(from);
            var failed = new HashSet<string>();

            for (int attempt = 0; attempt < BoatRollAttempts; attempt++)
            {
                int rx = _random.NextInt(x - BoatRollRadius, x + BoatRollRadius);
                int ry = _random.NextInt(y - BoatRollRadius, y + BoatRollRadius);

                if (map.IsValidCoord(rx, ry) == false)
                    continue;

                int tile = map.Ref(rx, ry);
                if (map.IsLand(tile) == false || map.IsImpassable(tile))
                    continue;

                FrontWarsPlayer owner = _game.Owner(tile);
                if (owner == _player)
                    continue;

                if (owner != null && (failed.Contains(owner.Id) || exclude.Contains(owner) || (IsFFA && owner.Troops > _player.Troops)))
                    continue;

                bool wanted = botsOnly
                    ? owner == null || owner.Type == EFrontWarsPlayerType.Bot
                    : owner == null || owner.IsFriendly(_player) == false;

                if (wanted == false)
                    continue;

                // 원본 `Df(…)===!1` — 엄격 비교
                if (FrontWarsTransportShipRules.CanBuildTransportShip(_game, _player, tile) < 0)
                {
                    if (owner != null)
                        failed.Add(owner.Id);

                    continue;
                }

                return tile;
            }

            return FrontWarsNationTiles.NoTile;
        }

        /// <summary>
        /// 가까운 섬 적 `[소스] findNearestIslandEnemy(){`.
        /// <para>비우호 · (FFA) 나보다 약한 플레이어를 영토 중심 맨해튼 거리 순으로, 내 해안과 그 해안 국경의 가장 가까운 칸 쌍으로 배를 보낼 수 있는 앞 2명. 2명이면 1/3 로 둘째.</para>
        /// </summary>
        private FrontWarsPlayer FindNearestIslandEnemy()
        {
            if (IsTransportDisabledOrFull)
                return null;

            List<int> shores = MyShoreTiles();
            if (shores.Count == 0)
                return null;

            FrontWarsTerrainMap map = _game.Map;
            int center = PlayerCenterTile(_player);
            var candidates = new List<(FrontWarsPlayer Player, int Distance)>();

            foreach (FrontWarsPlayer p in _game.Players())
            {
                if (p == _player || _player.IsFriendly(p) || (IsFFA && p.Troops >= _player.Troops))
                    continue;

                candidates.Add((p, map.ManhattanDistance(center, PlayerCenterTile(p))));
            }

            if (candidates.Count == 0)
                return null;

            var found = new List<FrontWarsPlayer>(IslandCandidates);

            foreach (var candidate in candidates.OrderBy(c => c.Distance))
            {
                List<int> theirShores = ShoreBorderTiles(candidate.Player);

                if (FrontWarsNationTiles.ClosestPair(map, shores, theirShores, out _, out int landing)
                    && FrontWarsNationTiles.IsTruthyTile(FrontWarsTransportShipRules.CanBuildTransportShip(_game, _player, landing)))
                {
                    found.Add(candidate.Player);

                    if (found.Count >= IslandCandidates)
                        break;
                }
            }

            if (found.Count == 0)
                return null;

            return found.Count >= IslandCandidates && _random.Chance(IslandSecondChance) ? found[1] : found[0];
        }

        /// <summary>영토 중심 칸 — 좌표 검사 없이 칸 번호로 `[소스] playerCenterTile(e){`. 살아 있는 플레이어는 늘 중심이 있다.</summary>
        private int PlayerCenterTile(FrontWarsPlayer player)
        {
            FrontWarsNationTiles.TryTerritoryCenter(_game, player, out int x, out int y);
            return _game.Map.Ref(x, y);
        }

        private List<int> ShoreBorderTiles(FrontWarsPlayer player)
        {
            var shores = new List<int>();

            foreach (int tile in player.BorderTiles.Items)
            {
                if (_game.Map.IsShore(tile))
                    shores.Add(tile);
            }

            return shores;
        }

        // ── 공격 보내기

        /// <summary>
        /// 나를 치는 공격 중 우호가 아니고 (내가 봇이 아니면 봇 제외) 병력이 가장 큰 공격자 (같으면 먼저 온 쪽) `[소스] findIncomingAttackPlayer(){`.
        /// </summary>
        public FrontWarsPlayer FindIncomingAttackPlayer()
        {
            bool skipBots = _player.Type != EFrontWarsPlayerType.Bot;
            FrontWarsPlayer best = null;
            double bestTroops = 0;

            foreach (FrontWarsAttack attack in _player.IncomingAttacks)
            {
                FrontWarsPlayer attacker = attack.Attacker;

                if (_player.IsFriendly(attacker) || (skipBots && attacker.Type == EFrontWarsPlayerType.Bot))
                    continue;

                if (attack.Troops > bestTroops)
                {
                    bestTroops = attack.Troops;
                    best = attacker;
                }
            }

            return best;
        }

        /// <summary>병력 절반을 그냥 보낸다 (첫 공격) `[소스] forceSendAttack(e){`.</summary>
        public void ForceSendAttack(FrontWarsPlayer target)
        {
            _game.AddExecution(new FrontWarsAttackExecution(_player.Troops / ForceAttackDivisor, _player, target?.Id));
        }

        /// <summary>
        /// 강제가 아니면 「사람을 칠까」 부터. 플레이어면 국경이 닿으면 지상 · 아니면 수송선. 빈 땅이면 지상으로 닿으면 지상 · 아니면 가까운 빈 땅 수송선 `[소스] sendAttack(e,t=!1){`.
        /// </summary>
        private bool SendAttack(FrontWarsPlayer target, bool force = false)
        {
            if (force == false && ShouldAttack(target) == false)
                return false;

            if (target != null)
                return _player.SharesBorderWith(target) ? SendLandAttack(target) : SendBoatAttack(target);

            return HasLandBorderWithTerraNullius() ? SendLandAttack(null) : SendBoatAttackToNearbyTerraNullius();
        }

        private bool HasLandBorderWithTerraNullius()
        {
            FrontWarsTerrainMap map = _game.Map;
            int[] scratch = new int[4];

            foreach (int border in _player.BorderTiles.Items)
            {
                int n = map.NeighborsInto(border, scratch);

                for (int k = 0; k < n; k++)
                {
                    int tile = scratch[k];
                    if (map.IsLand(tile) && map.IsImpassable(tile) == false && _game.HasOwner(tile) == false)
                        return true;
                }
            }

            return false;
        }

        /// <summary>해안 칸 10개마다 한 칸씩 — 4방향 1칸이 물이고 5칸 앞이 낙진 없는 빈 땅이면 병력/5 로 수송선 `[소스] sendBoatAttackToNearbyTerraNullius(){`.</summary>
        private bool SendBoatAttackToNearbyTerraNullius()
        {
            if (IsTransportDisabledOrFull)
                return false;

            FrontWarsTerrainMap map = _game.Map;
            List<int> shores = MyShoreTiles();

            for (int i = 0; i < shores.Count; i += NeutralBoatShoreStep)
            {
                int x = map.X(shores[i]), y = map.Y(shores[i]);

                for (int d = 0; d < CardinalDx.Length; d++)
                {
                    int nx = x + CardinalDx[d], ny = y + CardinalDy[d];
                    if (map.IsValidCoord(nx, ny) == false || map.IsWater(map.Ref(nx, ny)) == false)
                        continue;

                    int fx = x + CardinalDx[d] * NeutralBoatReach, fy = y + CardinalDy[d] * NeutralBoatReach;
                    if (map.IsValidCoord(fx, fy) == false)
                        continue;

                    int tile = map.Ref(fx, fy);
                    if (map.IsLand(tile) == false || map.IsImpassable(tile) || _game.HasOwner(tile) || _game.HasFallout(tile)
                        || FrontWarsNationTiles.IsTruthyTile(FrontWarsTransportShipRules.CanBuildTransportShip(_game, _player, tile)) == false)
                        continue;

                    double troops = _player.Troops / BoatTroopsDivisor;
                    if (troops < 1)
                        return false;

                    _game.AddExecution(new FrontWarsTransportShipExecution(_player, null, tile, troops));
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 사람이고 배신자가 아니면 — 쉬움은 난수 0~3 이 0 일 때만 · 보통은 1/4 확률로 안 친다 `[소스] shouldAttack(e){`.
        /// <para>사람 대 네이션 팀전이면 난수 없이 늘 친다.</para>
        /// </summary>
        /// <param name="target">null = 주인 없는 땅 (늘 참).</param>
        public bool ShouldAttack(FrontWarsPlayer target)
        {
            // ⚠ 원본은 게임 방식을 안 보고 팀 나누기 값만 본다 `[소스] this.game.config().gameConfig().playerTeams===`HumansVsNations`)return!0`
            if (target == null || target.Type != EFrontWarsPlayerType.Human || target.IsTraitor || _game.Settings.PlayerTeams == FrontWarsGame.HumansVsNations)
                return true;

            switch (_game.Settings.Difficulty)
            {
                case EFrontWarsDifficulty.Easy:
                    return _random.NextInt(0, EasyHumanAttackRoll) == 0;
                case EFrontWarsDifficulty.Medium:
                    return _random.Chance(MediumHumanSkipChance) == false;
                default:
                    return true;
            }
        }

        /// <summary>팀전이 아니고 · 공격받는 중이 아니고 · 어려움/불가능이고 · 보낼 병력 &lt; 대상 병력×0.2 `[소스] isAttackTooWeak(e,t){`.</summary>
        private bool IsAttackTooWeak(double troops, FrontWarsPlayer target)
        {
            if (_game.Settings.IsTeamMode || _player.IncomingAttacks.Count > 0)
                return false;

            EFrontWarsDifficulty difficulty = _game.Settings.Difficulty;

            if (difficulty != EFrontWarsDifficulty.Hard && difficulty != EFrontWarsDifficulty.Impossible)
                return false;

            return troops < target.Troops * TooWeakShare;
        }

        /// <summary>
        /// 보내기 상한 `[소스] troopSendCap(){`. 팀전이거나 비율이 0(쉬움 · 보통)이면 무한.
        /// <para>비우호 사람 · 네이션 이웃 중 가장 큰 병력 M 이 있으면 max(0, 병력 − ceil(M×비율)). 받는 공격 합이 있으면 그것과 큰 쪽.</para>
        /// </summary>
        private double TroopSendCap()
        {
            if (_game.Settings.IsTeamMode)
                return double.PositiveInfinity;

            double share = _row.TroopSendCapShare;
            if (share <= 0)
                return double.PositiveInfinity;

            double strongest = 0;

            foreach (FrontWarsPlayer neighbor in _player.Neighbors())
            {
                if (neighbor == null || _player.IsFriendly(neighbor) || neighbor.Type == EFrontWarsPlayerType.Bot)
                    continue;

                if (neighbor.Troops > strongest)
                    strongest = neighbor.Troops;
            }

            double cap = double.PositiveInfinity;

            if (strongest > 0)
            {
                double keep = Math.Ceiling(strongest * share);
                cap = Math.Max(0, _player.Troops - keep);
            }

            double incoming = 0;
            foreach (FrontWarsAttack attack in _player.IncomingAttacks)
                incoming += attack.Troops;

            return incoming > 0 ? Math.Max(cap, incoming) : cap;
        }

        /// <summary>
        /// 보낼 병력. null = 안 보냄 `[소스] attackBudget(e,t){`.
        /// <para>남길 병력 = 최대 × (플레이어이고 구조물 가진 봇이 아니면 보유 비율, 아니면 확장 비율). 봇 · 지상 · 수송선 순으로 식이 갈린다.</para>
        /// </summary>
        private double? AttackBudget(FrontWarsPlayer target, bool land)
        {
            double max = _game.Rules.MaxTroops(_player);
            bool isBot = target != null && target.Type == EFrontWarsPlayerType.Bot;
            bool botWithStructures = isBot && HoldsStructures(target);
            double keep = max * (target != null && botWithStructures == false ? _reserveRatio : _expandRatio);
            double troops;

            if (isBot)
                troops = BotAttackBudget(target, _player.Troops - keep - _botAttackTroopsSent);
            else if (land)
                troops = _player.Troops - keep;
            else
                troops = _player.Troops / BoatTroopsDivisor;

            if (target != null)
                troops = Math.Min(troops, TroopSendCap());

            if (troops < 1 || (target != null && IsAttackTooWeak(troops, target)))
                return null;

            if (target != null)
                _emoji?.MaybeSendAttackEmoji(target);

            return troops;
        }

        /// <summary>쉬움은 가진 예산 그대로. 아니면 봇 병력×4 로 자르되 예산이 봇 병력×2 미만이면 0 `[소스] botAttackBudget(e,t){`.</summary>
        private double BotAttackBudget(FrontWarsPlayer bot, double available)
        {
            if (_game.Settings.Difficulty == EFrontWarsDifficulty.Easy)
            {
                _botAttackTroopsSent += available;
                return available;
            }

            double budget = bot.Troops * BotBudgetTroopsMul;

            if (budget > available)
                budget = available < bot.Troops * BotBudgetMinTroopsMul ? 0 : available;

            _botAttackTroopsSent += budget;
            return budget;
        }

        private bool SendLandAttack(FrontWarsPlayer target)
        {
            double? troops = AttackBudget(target, true);
            if (troops.HasValue == false)
                return false;

            _game.AddExecution(new FrontWarsAttackExecution(troops.Value, _player, target?.Id));
            return true;
        }

        /// <summary>내 해안과 그 해안 국경의 가장 가까운 칸 쌍으로 수송선 `[소스] sendBoatAttack(e){`.</summary>
        private bool SendBoatAttack(FrontWarsPlayer target)
        {
            if (_game.Rules.IsUnitDisabled(EFrontWarsUnit.TransportShip))
                return false;

            List<int> theirShores = ShoreBorderTiles(target);

            if (FrontWarsNationTiles.ClosestPair(_game.Map, MyShoreTiles(), theirShores, out _, out int landing) == false
                || FrontWarsNationTiles.IsTruthyTile(FrontWarsTransportShipRules.CanBuildTransportShip(_game, _player, landing)) == false)
                return false;

            double? troops = AttackBudget(target, false);
            if (troops.HasValue == false)
                return false;

            _game.AddExecution(new FrontWarsTransportShipExecution(_player, target.Id, landing, troops.Value));
            return true;
        }
    }
}
