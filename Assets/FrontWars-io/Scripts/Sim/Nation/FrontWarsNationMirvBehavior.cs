using System.Collections.Generic;
using JinHyung.Data;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 네이션 MIRV 도우미 `[소스] Cq3gr8XO.js:zf=class{`.
    ///
    /// <para>
    /// 사일로 · 골드가 있고 망설임 난수를 넘으면, 위협 점수가 가장 높은 플레이어(최근 300틱 안에 어느 네이션이든 MIRV 를 쏜 대상 제외)의
    /// 영토 중심에 MIRV 를 쏜다. 교리 값은 난이도 표 Mirv* 열 `[소스] Rf={`.
    /// </para>
    /// <para>목표를 고를 때마다 팀 집계(팀별 칸 합 · 팀에서 칸이 가장 많은 플레이어)를 다시 한다 — 개인전이면 비어 있다.</para>
    /// </summary>
    public sealed class FrontWarsNationMirvBehavior
    {
        /// <summary>같은 대상을 다시 노리기까지 `[소스] e-i<300`.</summary>
        private const int RecentTargetTicks = 300;

        /// <summary>위협 점수 층 `[소스] return 300+e.numTilesOwned() · 200+t · 100+n`.</summary>
        private const double AimedAtUsScore = 300;
        private const double DenialScore = 200;
        private const double SteamrollScore = 100;

        private const int PercentRoll = 100;

        private readonly FrontWarsRandom _random;
        private readonly FrontWarsGame _game;
        private readonly FrontWarsPlayer _player;
        private readonly FrontWarsNationSharedState _shared;
        private readonly FrontWarsDifficultyData _doctrine;
        private readonly FrontWarsNationEmojiBehavior _emoji;

        /// <summary>팀 집계 `[소스] this.teamTiles=new Map,this.teamLeader=new Map`.</summary>
        private readonly Dictionary<string, int> _teamTiles = new Dictionary<string, int>();
        private readonly Dictionary<string, FrontWarsPlayer> _teamLeader = new Dictionary<string, FrontWarsPlayer>();

        public FrontWarsNationMirvBehavior(FrontWarsRandom random, FrontWarsGame game, FrontWarsPlayer player, FrontWarsNationSharedState shared, FrontWarsNationEmojiBehavior emoji)
        {
            _random = random;
            _game = game;
            _player = player;
            _shared = shared;
            _emoji = emoji;
            _doctrine = game.Settings.DifficultyRow;
        }

        /// <summary>MIRV 허용 · 사일로 있음 · 골드 충분 · 난수 0~99 ≥ 망설임이면 목표를 골라 쏜다 `[소스] considerMIRV(){`.</summary>
        public bool ConsiderMirv()
        {
            if (_game.Rules.IsUnitDisabled(EFrontWarsUnit.MIRV)
                || _player.Units(EFrontWarsUnit.MissileSilo).Count == 0
                || _player.Gold < _game.Rules.UnitCost(EFrontWarsUnit.MIRV, _player)
                || _random.NextInt(0, PercentRoll) < _doctrine.MirvHesitation)
                return false;

            FrontWarsPlayer target = PickTarget();
            if (target == null)
                return false;

            LaunchAt(target);
            return true;
        }

        /// <summary>점수가 0 보다 큰 가장 높은 대상 (같으면 먼저 나온 쪽) `[소스] pickTarget(){`.</summary>
        private FrontWarsPlayer PickTarget()
        {
            int ticks = _game.Ticks;
            TakeTeamCensus();
            FrontWarsPlayer best = null;
            double bestScore = 0;

            foreach (FrontWarsPlayer p in _game.Players())
            {
                if (IsEligibleTarget(p) == false)
                    continue;

                if (_shared.RecentMirvTargets.TryGetValue(p.Id, out int firedAt) && ticks - firedAt < RecentTargetTicks)
                    continue;

                double score = ThreatScore(p);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }

            return best;
        }

        private bool IsEligibleTarget(FrontWarsPlayer p)
        {
            return p != _player && p.Type != EFrontWarsPlayerType.Bot && _player.IsOnSameTeam(p) == false;
        }

        private double ThreatScore(FrontWarsPlayer p)
        {
            if (HasMirvAimedAtUs(p))
                return AimedAtUsScore + p.NumTilesOwned;

            double denial = DenialSeverity(p);
            if (denial > 0)
                return DenialScore + denial;

            int steamroll = SteamrollSeverity(p);
            return steamroll > 0 ? SteamrollScore + steamroll : 0;
        }

        private bool HasMirvAimedAtUs(FrontWarsPlayer p)
        {
            foreach (FrontWarsUnit mirv in p.Units(EFrontWarsUnit.MIRV))
            {
                int target = mirv.TargetTile;
                if (target >= 0 && _game.OwnerSmallId(target) == _player.SmallId)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 땅 / 맵 땅 이 혼자 차지 문턱 이상이면 그 비율 `[소스] denialSeverity(e){`.
        /// <para>팀이 있으면 그 팀에서 칸이 가장 많은 플레이어만 — 팀 칸 합 / 맵 땅 이 팀 차지 문턱 이상이면 그 비율.</para>
        /// </summary>
        private double DenialSeverity(FrontWarsPlayer p)
        {
            int land = _game.NumLandTiles;
            if (land == 0)
                return 0;

            if (p.Team != null)
            {
                if (_teamLeader.TryGetValue(p.Team, out FrontWarsPlayer leader) == false || leader != p)
                    return 0;

                _teamTiles.TryGetValue(p.Team, out int teamTiles);
                double teamShare = teamTiles / (double)land;
                return teamShare >= _doctrine.MirvTeamDenialShare ? teamShare : 0;
            }

            double share = p.NumTilesOwned / (double)land;
            return share >= _doctrine.MirvSoloDenialShare ? share : 0;
        }

        /// <summary>살아 있는 플레이어의 팀별 칸 합 · 칸이 가장 많은 사람(같으면 먼저) `[소스] takeTeamCensus(){`.</summary>
        private void TakeTeamCensus()
        {
            _teamTiles.Clear();
            _teamLeader.Clear();

            foreach (FrontWarsPlayer p in _game.Players())
            {
                string team = p.Team;
                if (team == null)
                    continue;

                int tiles = p.NumTilesOwned;
                _teamTiles.TryGetValue(team, out int sum);
                _teamTiles[team] = sum + tiles;

                if (_teamLeader.TryGetValue(team, out FrontWarsPlayer leader) == false || tiles > leader.NumTilesOwned)
                    _teamLeader[team] = p;
            }
        }

        /// <summary>도시 레벨 합이 바닥을 넘고 · 모두보다 많고 · 2위 × 격차 이상이면 (내 도시 − 2위 도시) `[소스] steamrollSeverity(e){`.</summary>
        private int SteamrollSeverity(FrontWarsPlayer p)
        {
            int cities = p.UnitCount(EFrontWarsUnit.City);
            if (cities <= _doctrine.MirvCityFloor)
                return 0;

            int second = 0;

            foreach (FrontWarsPlayer other in _game.Players())
            {
                if (other == p)
                    continue;

                int c = other.UnitCount(EFrontWarsUnit.City);
                if (c > second)
                    second = c;

                if (c >= cities)
                    return 0;
            }

            return cities >= second * _doctrine.MirvCityGap ? cities - second : 0;
        }

        /// <summary>공격 이모지 → 영토 중심에 MIRV → 공유 기록 → 모두에게 핵 이모지 → MIRV 대응 이모지 `[소스] launchAt(e){`.</summary>
        private void LaunchAt(FrontWarsPlayer target)
        {
            _emoji?.MaybeSendAttackEmoji(target);

            FrontWarsTerrainMap map = _game.Map;

            if (FrontWarsNationTiles.TryTerritoryCenter(_game, target, out int x, out int y) == false || map.IsValidCoord(x, y) == false)
                return;

            int tile = map.Ref(x, y);

            // 원본 `!canBuild(…)` — 사일로 칸 0 도 거짓
            if (FrontWarsNationTiles.IsTruthyTile(_player.CanBuild(EFrontWarsUnit.MIRV, tile)) == false)
                return;

            _game.AddExecution(new FrontWarsMirvExecution(_player, tile));
            _shared.RecentMirvTargets[target.Id] = _game.Ticks;
            _emoji?.SendNukeEmojiToAll();
            _emoji?.RespondToMirv(target);
        }
    }
}
