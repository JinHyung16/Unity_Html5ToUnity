using System;
using System.Collections.Generic;
using System.Linq;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 네이션 도우미들이 같이 쓰는 칸 계산 — 땅 표본 · 비복원 표본 · 가장 가까운 칸 쌍 · 영토 중심 · 핵 반경.
    /// <para>난수를 쓰는 함수는 부르는 쪽 난수를 받는다. 굴리는 횟수가 원본과 같아야 한다.</para>
    /// </summary>
    public static class FrontWarsNationTiles
    {
        public const int NoTile = -1;

        /// <summary>경계 상자 안에서 내 땅을 찾는 시도 수 `[소스] function Hf(e,t,n,r=null){…for(let r=0;r<100;r++)`.</summary>
        private const int OwnedTileRolls = 100;

        /// <summary>시도가 다 실패했을 때 땅이 이 수 이하면 내 칸 중 아무거나 `[소스] n.numTilesOwned()<=100`.</summary>
        private const int SmallTerritoryTiles = 100;

        // 핵 반경 `[소스] nukeMagnitudes(e){switch(e){case S.MIRVWarhead:return{inner:12,outer:18};case S.AtomBomb:return{inner:12,outer:30};case S.HydrogenBomb:return{inner:80,outer:100}}`
        // 표 FrontWarsNukeTable 에 같은 값이 있지만 시뮬 설정(FrontWarsGameSettings)이 그 표를 들고 있지 않다 — 설정에 붙으면 거기서 읽도록 바꾼다
        private const int AtomInnerRadius = 12;
        private const int AtomOuterRadius = 30;
        private const int HydrogenInnerRadius = 80;
        private const int HydrogenOuterRadius = 100;
        private const int WarheadInnerRadius = 12;
        private const int WarheadOuterRadius = 18;

        /// <summary>
        /// 원본 JS 참 거짓 — 칸 번호 0 은 거짓이다. 원본이 <c>if(tile)</c> · <c>!canBuild(…)</c> 처럼 칸을 참 거짓으로 볼 때 쓴다.
        /// <para><c>===!1</c> 로 엄격히 비교하는 곳은 <c>tile &gt;= 0</c> 을 쓴다.</para>
        /// </summary>
        public static bool IsTruthyTile(int tile)
        {
            return tile > 0;
        }

        public static int NukeInnerRadius(EFrontWarsUnit type)
        {
            switch (type)
            {
                case EFrontWarsUnit.AtomBomb: return AtomInnerRadius;
                case EFrontWarsUnit.HydrogenBomb: return HydrogenInnerRadius;
                case EFrontWarsUnit.MIRVWarhead: return WarheadInnerRadius;
                default: throw new InvalidOperationException($"Unknown nuke type: {type}");
            }
        }

        public static int NukeOuterRadius(EFrontWarsUnit type)
        {
            switch (type)
            {
                case EFrontWarsUnit.AtomBomb: return AtomOuterRadius;
                case EFrontWarsUnit.HydrogenBomb: return HydrogenOuterRadius;
                case EFrontWarsUnit.MIRVWarhead: return WarheadOuterRadius;
                default: throw new InvalidOperationException($"Unknown nuke type: {type}");
            }
        }

        /// <summary>
        /// 그 플레이어 땅 표본 count 번 `[소스] function Vf(e,t,n,r){`. 한 번마다 <see cref="RollOwnedTile"/> 이고, 못 찾은 번은 빠진다.
        /// </summary>
        public static List<int> SampleOwnedTiles(FrontWarsRandom random, FrontWarsGame game, FrontWarsPlayer player, int count)
        {
            bool hasBounds = TryBorderBounds(game, player, out FrontWarsTileBounds bounds);
            var result = new List<int>(count);

            for (int i = 0; i < count; i++)
            {
                int tile = RollOwnedTile(random, game, player, hasBounds, bounds);
                if (tile >= 0)
                    result.Add(tile);
            }

            return result;
        }

        /// <summary>
        /// 국경 경계 상자 안 난수 칸을 100번까지 굴려 그 플레이어 땅인 첫 칸 `[소스] function Hf(e,t,n,r=null){`.
        /// <para>못 찾으면 땅이 1~100칸일 때만 그 칸들 중 아무거나, 아니면 없음.</para>
        /// <para>⚠ 원본 nextInt 의 끝은 제외라 상자의 오른쪽 · 아래 끝 줄은 뽑히지 않는다 — 그대로 둔다.</para>
        /// </summary>
        private static int RollOwnedTile(FrontWarsRandom random, FrontWarsGame game, FrontWarsPlayer player, bool hasBounds, FrontWarsTileBounds bounds)
        {
            FrontWarsTerrainMap map = game.Map;

            for (int attempt = 0; attempt < OwnedTileRolls; attempt++)
            {
                if (hasBounds == false)
                {
                    // 국경이 비면 원본 상자는 ±무한대라 좌표가 NaN — 난수 두 번만 쓰고 넘어간다
                    random.Next();
                    random.Next();
                    continue;
                }

                int x = random.NextInt(bounds.MinX, bounds.MaxX);
                int y = random.NextInt(bounds.MinY, bounds.MaxY);

                if (map.IsValidCoord(x, y) == false)
                    continue;

                int tile = map.Ref(x, y);
                if (game.OwnerSmallId(tile) == player.SmallId)
                    return tile;
            }

            if (player.NumTilesOwned > 0 && player.NumTilesOwned <= SmallTerritoryTiles)
                return random.RandElement(player.Tiles.Snapshot());

            return NoTile;
        }

        /// <summary>국경 칸 경계 상자 `[소스] function Mt(e,t){`. 국경이 비면 false.</summary>
        public static bool TryBorderBounds(FrontWarsGame game, FrontWarsPlayer player, out FrontWarsTileBounds bounds)
        {
            IReadOnlyList<int> border = player.BorderTiles.Items;

            if (border.Count == 0)
            {
                bounds = default;
                return false;
            }

            bounds = FrontWarsTileBounds.Of(game.Map, border);
            return true;
        }

        /// <summary>
        /// 비복원 표본 `[소스] function*Uf(e,t,n=50){`. 개수 이하면 전부(순서 그대로).
        /// <para>넘으면 count 번: 남은 칸 수 안 난수 → 이미 뽑은 번호(오름차순)를 건너뛰며 실제 번호로 옮기고 → 정렬 자리에 끼운다. 뽑은 순서대로 돌려준다.</para>
        /// </summary>
        public static List<int> SampleWithoutReplacement(FrontWarsRandom random, IReadOnlyList<int> items, int count)
        {
            if (items.Count <= count)
                return new List<int>(items);

            var picked = new List<int>(count);
            var result = new List<int>(count);

            for (int n = count; n > 0; n--)
            {
                int k = random.NextInt(0, items.Count - picked.Count);

                foreach (int p in picked)
                {
                    if (p <= k)
                        k++;
                    else
                        break;
                }

                int at = picked.Count;
                while (at > 0 && picked[at - 1] > k)
                    at--;

                picked.Insert(at, k);
                result.Add(items[k]);
            }

            return result;
        }

        /// <summary>
        /// 두 칸 목록에서 맨해튼 거리가 가장 가까운 쌍 (근사) `[소스] function cd(e,t,n){`.
        /// <para>둘 다 x 로 안정 정렬한 뒤 두 포인터로 한 번 훑는다 — 한쪽이 끝이면 다른 쪽을, 아니면 x 가 작은 쪽을 민다.</para>
        /// </summary>
        public static bool ClosestPair(FrontWarsTerrainMap map, IEnumerable<int> from, IEnumerable<int> to, out int fromTile, out int toTile)
        {
            List<int> a = from.OrderBy(map.X).ToList();
            List<int> b = to.OrderBy(map.X).ToList();
            fromTile = NoTile;
            toTile = NoTile;

            if (a.Count == 0 || b.Count == 0)
                return false;

            int i = 0, j = 0;
            double best = double.PositiveInfinity;
            fromTile = a[0];
            toTile = b[0];

            while (i < a.Count && j < b.Count)
            {
                int s = a[i], t = b[j];
                int d = Math.Abs(map.X(s) - map.X(t)) + Math.Abs(map.Y(s) - map.Y(t));

                if (d < best)
                {
                    best = d;
                    fromTile = s;
                    toTile = t;
                }

                if (i == a.Count - 1)
                    j++;
                else if (j == b.Count - 1 || map.X(s) < map.X(t))
                    i++;
                else
                    j++;
            }

            return true;
        }

        /// <summary>
        /// 영토 중심 좌표 — 가장 큰 조각 경계 상자 중심, 없으면 국경 경계 상자 중심 `[소스] playerCenterTile(e){ · territoryCenter(e){ · function Nt(e,t){`.
        /// <para>살아 있는 플레이어는 국경이 늘 있다. 조각도 국경도 없으면 false.</para>
        /// </summary>
        public static bool TryTerritoryCenter(FrontWarsGame game, FrontWarsPlayer player, out int x, out int y)
        {
            FrontWarsTileBounds bounds;

            if (player.LargestClusterBounds.HasValue)
                bounds = player.LargestClusterBounds.Value;
            else if (TryBorderBounds(game, player, out bounds) == false)
            {
                x = 0;
                y = 0;
                return false;
            }

            x = bounds.MinX + (bounds.MaxX - bounds.MinX) / 2;
            y = bounds.MinY + (bounds.MaxY - bounds.MinY) / 2;
            return true;
        }
    }
}
