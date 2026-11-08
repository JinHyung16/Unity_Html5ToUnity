using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 승리 판정 — 10틱마다 `[소스] Cq3gr8XO.js:Dp=class{`. 스폰 단계에는 안 돈다.
    ///
    /// <list type="number">
    /// <item>제한 시간이 켜져 있고 (스폰 끝부터) 시간이 다 됐으면 — 개인전: 칸이 가장 많은 플레이어, 팀전: 칸 합이 가장 많은 팀(봇 팀 제외)이 이긴다.</item>
    /// <item>아니면 개인전: 칸이 가장 많은 플레이어의 칸 ÷ (땅 − 낙진) × 100 이 승리 퍼센트(80)를 «넘으면» 이긴다.</item>
    /// <item>팀전: 팀마다 칸을 더해(봇 팀 포함) 가장 큰 팀이 승리 퍼센트(95)를 «넘으면» 이긴다. 그 팀이 봇 팀이면 아무도 안 이긴다.</item>
    /// </list>
    /// 이기면 끝난다.
    /// </summary>
    public sealed class FrontWarsWinCheckExecution : IFrontWarsExecution
    {
        private const int CheckInterval = 10;
        private const int TicksPerMinute = 60 * 10;

        private FrontWarsGame _game;

        public bool IsActive { get; private set; } = true;

        public bool ActiveDuringSpawnPhase
        {
            get { return false; }
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _game = game;
        }

        public void Tick(int tick)
        {
            if (tick % CheckInterval != 0)
                return;

            if (CheckTimeLimit(tick))
                return;

            if (_game.Settings.IsTeamMode)
                CheckWinnerTeam();
            else
                CheckWinnerFfa();
        }

        private bool CheckTimeLimit(int tick)
        {
            int? minutes = _game.Settings.GameLengthMinutes;
            if (minutes.HasValue == false)
                return false;

            if (tick - _game.Settings.SpawnPhaseTurns < minutes.Value * TicksPerMinute)
                return false;

            if (_game.Settings.IsTeamMode == false)
            {
                FrontWarsPlayer leader = Leader(_game.Players());
                if (leader == null)
                    return false;

                _game.SetWinner(leader);
                IsActive = false;
                return true;
            }

            // 시간 끝 팀 집계는 봇 팀을 뺀다 `[소스] t!==null&&t!==_.Bot&&r.set(t,`
            string team = LeadingTeam(TeamTiles(false), out _);
            if (team == null)
                return false;

            _game.SetWinnerTeam(team);
            IsActive = false;
            return true;
        }

        private void CheckWinnerFfa()
        {
            FrontWarsPlayer leader = Leader(_game.Players());
            if (leader == null)
                return;

            double land = _game.NumLandTiles - _game.NumTilesWithFallout;
            if (leader.NumTilesOwned / land * 100 > _game.Settings.WinPercent)
            {
                _game.SetWinner(leader);
                IsActive = false;
            }
        }

        /// <summary>`[소스] checkWinnerTeam(){` — 봇 팀도 집계에 넣고, 1위가 봇 팀이면 그냥 넘어간다.</summary>
        private void CheckWinnerTeam()
        {
            string team = LeadingTeam(TeamTiles(true), out int tiles);
            if (team == null)
                return;

            double land = _game.NumLandTiles - _game.NumTilesWithFallout;
            if (tiles / land * 100 > _game.Settings.WinPercent)
            {
                if (team == FrontWarsGame.BotTeam)
                    return;

                _game.SetWinnerTeam(team);
                IsActive = false;
            }
        }

        /// <summary>살아 있는 플레이어의 팀별 칸 합 — 처음 나온 팀 순서 (원본 Map 순서).</summary>
        private List<(string Team, int Tiles)> TeamTiles(bool includeBotTeam)
        {
            var result = new List<(string, int)>();

            foreach (FrontWarsPlayer p in _game.Players())
            {
                string team = p.Team;
                if (team == null || (includeBotTeam == false && team == FrontWarsGame.BotTeam))
                    continue;

                int index = result.FindIndex(e => e.Item1 == team);
                if (index < 0)
                    result.Add((team, p.NumTilesOwned));
                else
                    result[index] = (team, result[index].Item2 + p.NumTilesOwned);
            }

            return result;
        }

        /// <summary>칸 합이 가장 큰 팀 — 같으면 먼저 나온 쪽. 합이 −1 보다 커야 한다 `[소스] function Ep(e){let t=null,n=-1;for(let[r,i]of e)i>n&&(t=r,n=i)`.</summary>
        private static string LeadingTeam(List<(string Team, int Tiles)> teams, out int tiles)
        {
            string best = null;
            tiles = -1;

            foreach ((string team, int n) in teams)
            {
                if (n > tiles)
                {
                    best = team;
                    tiles = n;
                }
            }

            return best;
        }

        /// <summary>칸이 가장 많은 플레이어 — 같으면 먼저 나온 쪽 `[소스] function Tp(e){`.</summary>
        private static FrontWarsPlayer Leader(List<FrontWarsPlayer> players)
        {
            FrontWarsPlayer best = null;

            foreach (FrontWarsPlayer p in players)
            {
                if (best == null || p.NumTilesOwned > best.NumTilesOwned)
                    best = p;
            }

            return best;
        }
    }
}
