using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 스폰 한 번 — 고른 칸 둘레 원 안의 빈 땅을 가진다 `[소스] Cq3gr8XO.js:ld=class{ · function sd(e,t){`.
    ///
    /// <list type="number">
    /// <item>한 번만 돈다. 스폰 단계가 아니면 아무것도 안 한다.</item>
    /// <item>없던 플레이어면 만든다. 이미 있으면 가진 칸을 전부 내놓는다 (다시 고르기).</item>
    /// <item>반지름 4 원(칸 중심을 −0.5 옮김) → 없으면 6 → 없으면 그 칸 · 4방향 이웃 하나.</item>
    /// <item>처음 나온 플레이어면 플레이어 실행(경제)을 붙인다. 봇이면 봇 AI 도 붙인다 (단위 9).</item>
    /// </list>
    /// </summary>
    public sealed class FrontWarsSpawnExecution : IFrontWarsExecution
    {
        private const int SpawnRadius = 4;
        private const int SpawnRadiusFallback = 6;

        /// <summary>원 중심을 칸 모서리 쪽으로 옮기는 값 `[소스] t.x(e)-.5`.</summary>
        private const double CircleCenterOffset = 0.5;

        private readonly FrontWarsPlayerInfo _info;
        private readonly int _tile;
        private FrontWarsGame _game;

        public bool IsActive { get; private set; } = true;

        public bool ActiveDuringSpawnPhase
        {
            get { return true; }
        }

        public string PlayerId
        {
            get { return _info.Id; }
        }

        public FrontWarsSpawnExecution(FrontWarsPlayerInfo info, int tile)
        {
            _info = info;
            _tile = tile;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _game = game;
        }

        public void Tick(int tick)
        {
            IsActive = false;

            if (_game.Map.IsValidRef(_tile) == false || _game.InSpawnPhase == false)
                return;

            FrontWarsPlayer player = _game.HasPlayer(_info.Id) ? _game.Player(_info.Id) : _game.AddPlayer(_info);

            foreach (int t in player.Tiles.Snapshot())
                player.Relinquish(t);

            List<int> tiles = SpawnTiles(_game, _tile);

            foreach (int t in tiles)
                player.Conquer(t);

            if (player.HasSpawned == false)
            {
                _game.AddExecution(new FrontWarsPlayerExecution(player));

                if (player.Type == EFrontWarsPlayerType.Bot)
                    _game.AddExecution(new FrontWarsBotExecution(player));
            }

            player.HasSpawned = true;
        }

        /// <summary>원 안 빈 땅 → 없으면 넓은 원 → 없으면 그 칸 · 이웃 하나.</summary>
        public static List<int> SpawnTiles(FrontWarsGame game, int center)
        {
            List<int> tiles = FreeLandInCircle(game, center, SpawnRadius);

            if (tiles.Count == 0)
                tiles = FreeLandInCircle(game, center, SpawnRadiusFallback);

            if (tiles.Count > 0)
                return tiles;

            if (IsFreeLand(game, center))
                return new List<int> { center };

            int[] scratch = new int[4];
            int n = game.Map.NeighborsInto(center, scratch);

            for (int i = 0; i < n; i++)
            {
                if (IsFreeLand(game, scratch[i]))
                    return new List<int> { scratch[i] };
            }

            return tiles;
        }

        private static List<int> FreeLandInCircle(FrontWarsGame game, int center, int radius)
        {
            FrontWarsTerrainMap map = game.Map;
            double cx = map.X(center) - CircleCenterOffset;
            double cy = map.Y(center) - CircleCenterOffset;
            double r2 = radius * radius;

            List<int> area = game.Bfs(center, t =>
            {
                double dx = map.X(t) - cx, dy = map.Y(t) - cy;
                return dx * dx + dy * dy <= r2;
            });

            return area.FindAll(t => IsFreeLand(game, t));
        }

        private static bool IsFreeLand(FrontWarsGame game, int tile)
        {
            return game.HasOwner(tile) == false && game.Map.IsLand(tile) && game.Map.IsImpassable(tile) == false;
        }
    }
}
