using System;
using System.Collections.Generic;
using System.Linq;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 수송선 출발 · 도착 칸 고르기 `[소스] Cq3gr8XO.js:function Df( · function kf( · function Af( · function jf( · function Mf( · function Nf( · function Of(`.
    /// </summary>
    public static class FrontWarsTransportShipRules
    {
        private const int WaterSearchRadius = 300;
        private const int NearShoreRadius = 100;
        private const int FarShoreRadius = 200;
        private const int NeutralShoreRadius1 = 50;
        private const int NeutralShoreRadius2 = 100;
        private const int NeutralShoreRadius3 = 200;
        private const int RouteMaxTries = 50000;
        private const int RouteX = 1;
        private const int SampleMinStep = 10;
        private const int SampleDivisor = 50;

        /// <summary>
        /// 그 칸으로 배를 보낼 수 있으면 출발 칸, 아니면 −1 `[소스] function Df(e,t,n){`.
        /// <list type="number">
        /// <item>배가 최대(3척)면 안 된다. 내 땅 · 우호 땅이면 안 된다.</item>
        /// <item>도착 칸을 고른다 (<see cref="TargetTransportTile"/>).</item>
        /// <item>도착 칸이 바다 해안이면 — 나도 바다 해안이 있어야 하고, 대상이 빈 땅이거나 대상도 바다 해안이 있어야 한다.</item>
        /// <item>아니면(호수 쪽) — 도착 칸에서 300칸 안 호수 · 해안을 넓혀 가까운 내 칸에서 출발한다.</item>
        /// </list>
        /// </summary>
        public static int CanBuildTransportShip(FrontWarsGame game, FrontWarsPlayer player, int tile)
        {
            if (player.UnitCount(EFrontWarsUnit.TransportShip) >= game.Settings.Config.BoatMaxNumber)
                return -1;

            FrontWarsPlayer owner = game.Owner(tile);

            if (owner == player || (owner != null && player.IsFriendly(owner)))
                return -1;

            int dst = TargetTransportTile(game, tile);
            if (dst < 0)
                return -1;

            FrontWarsTerrainMap map = game.Map;

            if (map.IsOceanShore(dst))
            {
                bool mine = player.BorderTiles.Items.Any(map.IsOceanShore) || (player.NumTilesOwned > 0 && player.Tiles.Any(map.IsOceanShore));

                bool theirs;
                if (game.HasOwner(tile) == false)
                    theirs = true;
                else
                    theirs = owner.BorderTiles.Items.Any(map.IsOceanShore) || (owner.NumTilesOwned > 0 && owner.Tiles.Any(map.IsOceanShore));

                return mine && theirs ? ShoreSpawn(game, player, dst) : -1;
            }

            List<int> water = game.Bfs(dst, t => map.ManhattanDistance(dst, t) <= WaterSearchRadius && (map.IsLake(t) || map.IsShore(t)))
                .OrderBy(t => map.ManhattanDistance(dst, t))
                .ToList();

            foreach (int t in water)
            {
                if (game.OwnerSmallId(t) == player.SmallId)
                    return ShoreSpawn(game, player, t);
            }

            return -1;
        }

        /// <summary>해안 칸이면 그 칸에 가장 가까운 내 출발 해안 `[소스] function Of(e,t,n){`.</summary>
        private static int ShoreSpawn(FrontWarsGame game, FrontWarsPlayer player, int tile)
        {
            if (game.Map.IsShore(tile) == false)
                return -1;

            // 원본 `r!==null&&r` — 칸 0 이면 0 을 돌려주고, 부르는 쪽 `===!1` 은 성공으로 본다
            return ClosestShore(game, player, tile);
        }

        /// <summary>
        /// 배가 내릴 칸 `[소스] function kf(e,t){`. 물로 바뀐 칸이면 없음.
        /// <para>남의 땅이면 그 주인의 가장 가까운 해안 → 없으면 100 · 200칸 안 바다 해안 → 100칸 안 해안. 빈 땅이면 50 · 100 · 200칸 안 빈 해안.</para>
        /// </summary>
        public static int TargetTransportTile(FrontWarsGame game, int tile)
        {
            if (game.Map.IsConvertedToWater(tile))
                return -1;

            FrontWarsPlayer owner = game.Owner(tile);
            FrontWarsTerrainMap map = game.Map;

            if (owner != null)
            {
                int r = ClosestShore(game, owner, tile);

                if (r < 0 && owner.NumTilesOwned > 0)
                {
                    List<int> candidates = ShoresWithin(game, owner, tile, NearShoreRadius, map.IsOceanShore);

                    if (candidates.Count == 0)
                        candidates = ShoresWithin(game, owner, tile, FarShoreRadius, map.IsOceanShore);

                    if (candidates.Count == 0)
                        candidates = ShoresWithin(game, owner, tile, NearShoreRadius, map.IsShore);

                    if (candidates.Count > 0)
                        r = candidates[0];
                }

                return r;
            }

            int neutral = ClosestNeutralShore(game, tile, NeutralShoreRadius1);
            if (neutral < 0)
                neutral = ClosestNeutralShore(game, tile, NeutralShoreRadius2);
            if (neutral < 0)
                neutral = ClosestNeutralShore(game, tile, NeutralShoreRadius3);

            return neutral;
        }

        private static List<int> ShoresWithin(FrontWarsGame game, FrontWarsPlayer owner, int tile, int radius, Func<int, bool> shore)
        {
            FrontWarsTerrainMap map = game.Map;
            return owner.Tiles.Where(t => map.ManhattanDistance(tile, t) <= radius && shore(t)).OrderBy(t => map.ManhattanDistance(tile, t)).ToList();
        }

        /// <summary>
        /// 그 플레이어의 해안 중 그 칸에 가장 가까운 칸 (같으면 앞) `[소스] function Af(e,t,n){`.
        /// <para>후보: 국경의 바다 해안 → 전 칸의 바다 해안 → 국경의 해안 → 전 칸의 해안.</para>
        /// </summary>
        public static int ClosestShore(FrontWarsGame game, FrontWarsPlayer player, int tile)
        {
            FrontWarsTerrainMap map = game.Map;
            List<int> candidates = player.BorderTiles.Items.Where(map.IsOceanShore).ToList();

            if (candidates.Count == 0 && player.NumTilesOwned > 0)
                candidates = player.Tiles.Where(map.IsOceanShore).ToList();

            if (candidates.Count == 0 && player.NumTilesOwned > 0)
            {
                candidates = player.BorderTiles.Items.Where(map.IsShore).ToList();

                if (candidates.Count == 0)
                    candidates = player.Tiles.Where(map.IsShore).ToList();
            }

            if (candidates.Count == 0)
                return -1;

            int best = candidates[0];
            int bestDist = map.ManhattanDistance(tile, best);

            for (int i = 1; i < candidates.Count; i++)
            {
                int d = map.ManhattanDistance(tile, candidates[i]);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = candidates[i];
                }
            }

            return best;
        }

        /// <summary>빈 땅으로 반경 안 넓혀 가장 가까운 해안 `[소스] function Nf(e,t,n){`.</summary>
        private static int ClosestNeutralShore(FrontWarsGame game, int tile, int radius)
        {
            FrontWarsTerrainMap map = game.Map;
            int best = -1;
            double bestDist = double.PositiveInfinity;

            foreach (int t in game.Bfs(tile, n => game.HasOwner(n) == false && map.ManhattanDistance(tile, n) <= radius))
            {
                if (map.IsShore(t) == false)
                    continue;

                int d = map.ManhattanDistance(tile, t);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = t;
                }
            }

            return best;
        }

        /// <summary>
        /// 그 칸에 가장 좋은 출발 해안 — 해안 후보 여럿에서 물길을 계산해 경로가 시작한 칸 옆의 내 해안 `[소스] function jf(e,t,n){`.
        /// <para>계산이 완성되지 않으면 가장 가까운 해안.</para>
        /// </summary>
        public static int BestTransportShipSpawn(FrontWarsGame game, FrontWarsPlayer player, int tile)
        {
            int dst = TargetTransportTile(game, tile);
            if (dst < 0)
                return -1;

            List<int> sources = CandidateShores(game, player, dst);

            if (game.Paths.ComputeRoute(sources, dst, RouteMaxTries, RouteX, out int start) == false)
            {
                int fallback = ClosestShore(game, player, dst);
                return fallback;
            }

            if (start < 0)
                return -1;

            int[] scratch = new int[4];
            int n = game.Map.NeighborsInto(start, scratch);

            for (int i = 0; i < n; i++)
            {
                if (game.Map.IsShore(scratch[i]) && game.OwnerSmallId(scratch[i]) == player.SmallId)
                    return scratch[i];
            }

            return -1;
        }

        /// <summary>
        /// 출발 후보 — 가장 가까운 해안 · 최소 x · 최소 y · 최대 x · 최대 y 해안 + 고르게 뽑은 해안 `[소스] function Mf(e,t,n){`.
        /// <para>⚠ 원본의 최소/최대 갱신은 else-if 사슬이라 한 칸이 한 극값만 바꾼다. 칸 0 · 없음은 빠진다 (filter(Boolean)).</para>
        /// </summary>
        private static List<int> CandidateShores(FrontWarsGame game, FrontWarsPlayer player, int dst)
        {
            FrontWarsTerrainMap map = game.Map;
            List<int> shores = player.BorderTiles.Items.Where(map.IsShore).ToList();

            if (shores.Count == 0 && player.NumTilesOwned > 0)
                shores = player.Tiles.Where(map.IsShore).ToList();

            double best = double.PositiveInfinity, minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            int closest = -1, minXTile = -1, minYTile = -1, maxXTile = -1, maxYTile = -1;

            foreach (int t in shores)
            {
                int d = map.ManhattanDistance(t, dst);
                int x = map.X(t), y = map.Y(t);

                if (d < best)
                {
                    best = d;
                    closest = t;
                }

                if (x < minX)
                {
                    minX = x;
                    minXTile = t;
                }
                else if (y < minY)
                {
                    minY = y;
                    minYTile = t;
                }
                else if (x > maxX)
                {
                    maxX = x;
                    maxXTile = t;
                }
                else if (y > maxY)
                {
                    maxY = y;
                    maxYTile = t;
                }
            }

            int step = Math.Max(SampleMinStep, (int)Math.Ceiling(shores.Count / (double)SampleDivisor));
            var result = new List<int> { closest, minXTile, minYTile, maxXTile, maxYTile };

            for (int i = 0; i < shores.Count; i++)
            {
                if (i % step == 0)
                    result.Add(shores[i]);
            }

            return result.Where(t => t > 0).ToList();
        }
    }
}
