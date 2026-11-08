using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>배 · 전함이 한 틱마다 다음 칸을 묻는 길 찾기 — 결과 타입은 <c>Sim/Path/FrontWarsPathStep</c> `[소스] Cq3gr8XO.js:rf=class e{ … nextTile(e,t,n=1,r=!1)`.</summary>
    public interface IFrontWarsUnitPathFinder
    {
        FrontWarsPathStep NextTile(int from, int to, int arriveDistance = 1);

        void Reset();
    }

    /// <summary>핵 포물선 경로 `[소스] Cq3gr8XO.js:$d=class{`.</summary>
    public interface IFrontWarsParabolaPath
    {
        void ComputeControlPoints(int src, int dst, int speed = 3, bool distanceBasedHeight = true);

        /// <summary>speed 칸 나아간 칸. 끝에 닿았으면 arrived 참.</summary>
        int NextTile(int speed, out bool arrived);

        int CurrentIndex { get; }

        IReadOnlyList<int> AllTiles();
    }

    /// <summary>
    /// 길 찾기 묶음 — 원본 미니 맵 A* · 물 연결 요소 · 포물선 · 역 경로를 한 곳에서 만든다.
    /// <para>구현은 <c>Sim/Path/</c> (원본 <c>rf · Hd · Dd · Yd · $d · zd</c> 이식) 가 맡는다. 게임이 <see cref="FrontWarsGame.Paths"/> 로 들고 있다.</para>
    /// </summary>
    public interface IFrontWarsPathFactory
    {
        /// <summary>미니 맵 계층 A* 길 찾기 `[소스] rf.Mini(game, maxTries, waterPath, x)`.</summary>
        IFrontWarsUnitPathFinder Mini(int maxTries, bool waterPath = true, int x = 20);

        /// <summary>물 연결 요소 동기 길 찾기 `[소스] rf.MiniWaterSync(game, useCache, x, maxNodes)`.</summary>
        IFrontWarsUnitPathFinder MiniWaterSync(bool useCache = true, int x = 4, int maxNodes = 250000);

        IFrontWarsParabolaPath Parabola();

        /// <summary>
        /// 여러 출발 칸 → 도착 칸 한 번 계산 `[소스] new Hd(e,e.miniMap(),i,r,5e4,1).compute() · routeStartTile()`.
        /// <para>완성이 아니면 false. 완성이면 경로가 시작한 칸을 준다 (없으면 −1).</para>
        /// </summary>
        bool ComputeRoute(IReadOnlyList<int> sources, int destination, int maxTries, int x, out int routeStartTile);

        /// <summary>철도 칸 경로 `[소스] new Hd(map,miniMap,e,t,5e3,20,!1,3)`. 못 찾으면 빈 목록.</summary>
        List<int> FindRailTilePath(int from, int to);

        /// <summary>역 경로 `[소스] new zd(e,t,5e3,20,new gd(game))`. 못 찾으면 빈 목록.</summary>
        List<FrontWarsTrainStation> FindStationsPath(FrontWarsTrainStation from, FrontWarsTrainStation to);

        /// <summary>물 칸으로 바뀐 칸을 길 찾기 캐시에 알린다 `[소스] Ad(this._map,e)`.</summary>
        void ApplyWaterConversion(int tile);
    }
}
