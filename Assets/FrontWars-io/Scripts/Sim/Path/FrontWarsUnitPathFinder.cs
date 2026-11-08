using System;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 유닛(배) 한 척의 길찾기 — 매 틱 현재 칸과 목표 칸을 주면 다음 칸을 준다 `[소스] Cq3gr8XO.js:var rf=class e{`.
    ///
    /// <para>
    /// 목표가 크게 움직였을 때만 다시 찾는다: 현재→목표 거리가 50 초과면 목표가 10칸 넘게, 25 초과면 5칸 넘게, 그 밖엔 1칸이라도
    /// 움직였을 때. 길을 다 쓰면 한 번 처음부터 다시 찾고, 그래도 없으면 PathNotFound.
    /// </para>
    ///
    /// <list type="bullet">
    /// <item><see cref="Mini"/> — 여러 틱에 나눠 찾는다(전함 · 상륙선: 반복 2000 · 시도 500).</item>
    /// <item><see cref="MiniWaterSync"/> — 한 번에 끝까지 찾고 게임 공용 캐시에 담는다(무역선: 가중치 4).</item>
    /// </list>
    /// </summary>
    public sealed class FrontWarsUnitPathFinder
    {
        /// <summary>
        /// 목표 이동 허용치를 가르는 거리 `[소스] r=n>50?10:n>25?5:0`.
        /// </summary>
        private const int FarDistance = 50;
        private const int MidDistance = 25;
        private const int FarTolerance = 10;
        private const int MidTolerance = 5;
        private const int NearTolerance = 0;

        private const int NoTile = FrontWarsPathStep.NoTile;

        private readonly FrontWarsPathMap _map;
        private readonly Func<int, int, FrontWarsMiniPathSearch> _newSearch;
        private readonly FrontWarsRouteCache _routeCache;
        private readonly Func<int, int, int[]> _computePathSync;

        private int _curr = NoTile;
        private int _dst = NoTile;
        private int[] _path;
        private int _pathIndex;
        private FrontWarsMiniPathSearch _search;
        private bool _computeFinished = true;

        private FrontWarsUnitPathFinder(FrontWarsPathMap map, Func<int, int, FrontWarsMiniPathSearch> newSearch, FrontWarsRouteCache routeCache, Func<int, int, int[]> computePathSync)
        {
            _map = map;
            _newSearch = newSearch;
            _routeCache = routeCache;
            _computePathSync = computePathSync;
        }

        /// <summary>
        /// 작은 지도 양방향 A* 를 틱마다 반복 예산만큼 돌린다 `[소스] static Mini(t,n,r=!0,i=20){`.
        /// </summary>
        /// <param name="iterations">한 틱 반복 예산 (전함 · 상륙선 2000)</param>
        /// <param name="waterPath">물길이면 참</param>
        /// <param name="maxTries">예산을 다 쓴 틱이 이 횟수에 닿으면 PathNotFound (전함 · 상륙선 500)</param>
        public static FrontWarsUnitPathFinder Mini(FrontWarsPathCache cache, int iterations, bool waterPath = true, int maxTries = 20)
        {
            if (cache == null)
                throw new ArgumentNullException(nameof(cache));

            return new FrontWarsUnitPathFinder(
                cache.Map,
                (src, dst) => new FrontWarsMiniPathSearch(cache, src, dst, iterations, maxTries, waterPath),
                null,
                null);
        }

        /// <summary>
        /// 한 번에 끝까지 찾는 물길 `[소스] static MiniWaterSync(t,n=!0,r=4,i=25e4){`.
        /// </summary>
        /// <param name="useRouteCache">게임 공용 길 캐시를 쓸지 (무역선 참)</param>
        /// <param name="heuristicWeight">휴리스틱 가중치 (무역선 4)</param>
        /// <param name="maxIterations">반복 한도</param>
        public static FrontWarsUnitPathFinder MiniWaterSync(FrontWarsPathCache cache, bool useRouteCache = true, double heuristicWeight = 4, int maxIterations = 250_000)
        {
            if (cache == null)
                throw new ArgumentNullException(nameof(cache));

            FrontWarsWaterSyncPathFinder water = cache.WaterSync;

            return new FrontWarsUnitPathFinder(
                cache.Map,
                (src, dst) => throw new InvalidOperationException("MiniWaterSync has no incremental search"),
                useRouteCache ? cache.RouteCache : null,
                (src, dst) => water.FindPath(src, dst, heuristicWeight, maxIterations));
        }

        public void Reset()
        {
            _curr = NoTile;
            _dst = NoTile;
            _path = null;
            _pathIndex = 0;
            _search = null;
            _computeFinished = true;
        }

        /// <summary>
        /// 다음 칸 `[소스] nextTile(e,t,n=1,r=!1){`.
        /// </summary>
        /// <param name="curr">현재 칸. 음수면 원본의 null (PathNotFound)</param>
        /// <param name="dst">목표 칸. 음수면 원본의 null (PathNotFound)</param>
        /// <param name="dist">맨해튼 거리가 이 값보다 작으면 Completed(칸 = curr)</param>
        /// <param name="noRetry">참이면 길을 다 썼을 때 다시 찾지 않고 PathNotFound</param>
        public FrontWarsPathStep NextTile(int curr, int dst, int dist = 1, bool noRetry = false)
        {
            if (curr < 0 || dst < 0)
                return new FrontWarsPathStep(EFrontWarsPathStatus.PathNotFound);

            if (_map.ManhattanDistance(curr, dst) < dist)
                return new FrontWarsPathStep(EFrontWarsPathStatus.Completed, curr);

            if (_computeFinished)
            {
                if (ShouldRecompute(curr, dst))
                {
                    _curr = curr;
                    _dst = dst;
                    _path = null;
                    _pathIndex = 0;

                    if (_routeCache != null && _routeCache.TryGet(curr, dst, out int[] cached))
                    {
                        _path = cached;
                        _pathIndex = 1;
                        return NextTile(curr, dst, dist, noRetry);
                    }

                    if (_computePathSync != null)
                    {
                        int[] found = _computePathSync(curr, dst);
                        if (found == null || found.Length == 0)
                            return new FrontWarsPathStep(EFrontWarsPathStatus.PathNotFound);

                        _path = found;
                        _pathIndex = 1;
                        StoreInRouteCache();
                        return NextTile(curr, dst, dist, noRetry);
                    }

                    _search = _newSearch(curr, dst);
                    _computeFinished = false;
                    return NextTile(curr, dst, dist, noRetry);
                }

                if (_path == null || _pathIndex >= _path.Length)
                {
                    if (noRetry)
                        return new FrontWarsPathStep(EFrontWarsPathStatus.PathNotFound);

                    Reset();
                    return NextTile(curr, dst, dist, true);
                }

                return new FrontWarsPathStep(EFrontWarsPathStatus.NextTile, _path[_pathIndex++]);
            }

            if (_search == null)
                throw new InvalidOperationException("aStar not initialized");

            switch (_search.Compute())
            {
                case EFrontWarsPathStatus.Completed:
                {
                    _computeFinished = true;
                    int[] path = _search.ReconstructPath();
                    _search = null;

                    if (path.Length == 0)
                    {
                        _path = null;
                        return new FrontWarsPathStep(EFrontWarsPathStatus.PathNotFound);
                    }

                    _path = path;
                    _pathIndex = 1;
                    StoreInRouteCache();
                    return NextTile(curr, dst, dist, noRetry);
                }
                case EFrontWarsPathStatus.Pending:
                    return new FrontWarsPathStep(EFrontWarsPathStatus.Pending);
                case EFrontWarsPathStatus.PathNotFound:
                    // 원본 그대로 — 탐색을 버리지 않는다. 다음 호출이 이어서 찾는다.
                    return new FrontWarsPathStep(EFrontWarsPathStatus.PathNotFound);
                default:
                    throw new InvalidOperationException("unexpected compute result");
            }
        }

        private void StoreInRouteCache()
        {
            if (_routeCache == null || _path == null || _curr == NoTile || _dst == NoTile)
                return;

            _routeCache.Store(_curr, _dst, _path);
        }

        private bool ShouldRecompute(int curr, int dst)
        {
            if (_path == null || _curr == NoTile || _dst == NoTile)
                return true;

            int distance = _map.ManhattanDistance(curr, dst);
            int tolerance = distance > FarDistance ? FarTolerance : distance > MidDistance ? MidTolerance : NearTolerance;
            return _map.ManhattanDistance(_dst, dst) > tolerance;
        }
    }
}
