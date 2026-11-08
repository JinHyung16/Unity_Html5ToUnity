using System;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 핵 · MIRV 포물선 경로 — 출발과 목표 사이 3차 곡선을 위로 띄워 칸 목록으로 쓴다 `[소스] Cq3gr8XO.js:var $d=class{`.
    ///
    /// <para>
    /// 조절점 = 출발→목표의 1/4 · 3/4 지점을 위로(y 감소) <c>max(거리/3, 50)</c> 만큼 올린 점(y 는 0 아래로 안 내려감).
    /// 띄우지 않기(<c>arc = false</c>)면 직선 곡선. 점 좌표를 내림해 칸으로 바꾼다.
    /// </para>
    /// </summary>
    public sealed class FrontWarsParabolaPathFinder
    {
        /// <summary>곡선 점 간격 기본값 `[소스] computeControlPoints(e,t,n=3,r=!0){`.</summary>
        public const double DefaultPointSpacing = 3;

        /// <summary>띄우는 높이 = max(거리 / 3, 50).</summary>
        private const double ArcHeightDivisor = 3;
        private const double MinArcHeight = 50;

        private readonly FrontWarsTerrainMap _map;
        private FrontWarsBezierCurve _curve;

        public FrontWarsParabolaPathFinder(FrontWarsTerrainMap map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
        }

        public void ComputeControlPoints(int src, int dst, double pointSpacing = DefaultPointSpacing, bool arc = true)
        {
            double sx = _map.X(src), sy = _map.Y(src);
            double dx = _map.X(dst), dy = _map.Y(dst);
            double ox = dx - sx, oy = dy - sy;
            double distance = Math.Sqrt(ox * ox + oy * oy);
            double lift = arc ? Math.Max(distance / ArcHeightDivisor, MinArcHeight) : 0;

            double c1X = sx + (dx - sx) / 4;
            double c1Y = Math.Max(sy + (dy - sy) / 4 - lift, 0);
            double c2X = sx + (dx - sx) * 3 / 4;
            double c2Y = Math.Max(sy + (dy - sy) * 3 / 4 - lift, 0);

            _curve = new FrontWarsBezierCurve(sx, sy, c1X, c1Y, c2X, c2Y, dx, dy, pointSpacing);
        }

        /// <summary>
        /// <paramref name="speed"/> 만큼 나아간 칸. 도착했으면 거짓(원본은 true 를 돌려준다) `[소스] nextTile(e){if(!this.curve)`.
        /// </summary>
        public bool TryNextTile(double speed, out int tile)
        {
            if (_curve == null)
                throw new InvalidOperationException("ParabolaPathFinder not initialized");

            int index = _curve.Increment(speed);
            if (index < 0)
            {
                tile = FrontWarsPathStep.NoTile;
                return false;
            }

            tile = ToTile(index);
            return true;
        }

        public int CurrentIndex()
        {
            return _curve == null ? 0 : _curve.CurrentIndex;
        }

        public int[] AllTiles()
        {
            if (_curve == null)
                return Array.Empty<int>();

            var tiles = new int[_curve.PointCount];
            for (int i = 0; i < tiles.Length; i++)
                tiles[i] = ToTile(i);

            return tiles;
        }

        private int ToTile(int index)
        {
            int x = (int)Math.Floor(_curve.PointX(index));
            int y = (int)Math.Floor(_curve.PointY(index));

            if (_map.IsValidCoord(x, y) == false)
                throw new ArgumentException($"Invalid coordinates: {x},{y}");

            return _map.Ref(x, y);
        }
    }
}
