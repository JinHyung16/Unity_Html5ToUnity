using System;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 반경 제한 넓이 우선 탐색 — 시작 칸에서 맨해튼 반경 안, 조건 맞는 칸만 넓혀 목표 칸을 찾으면 참 `[소스] Cq3gr8XO.js:nh=class{`.
    /// <para>표시 배열을 도장 번호로 재사용해 매번 비우지 않는다. 이웃 순서는 위 · 아래 · 왼쪽 · 오른쪽.</para>
    /// </summary>
    public sealed class FrontWarsBoundedBfs
    {
        private readonly int _maxRadius;
        private readonly int _side;
        private readonly uint[] _visited;
        private readonly int[] _queue;
        private uint _stamp;

        public FrontWarsBoundedBfs(int maxRadius)
        {
            _maxRadius = maxRadius;
            _side = 2 * maxRadius + 1;
            _visited = new uint[_side * _side];
            _queue = new int[_side * _side];
        }

        public bool Search(FrontWarsTerrainMap map, int start, int radius, Func<int, bool> include, Func<int, bool> goal)
        {
            if (radius > _maxRadius)
                throw new ArgumentException($"BoundedBFS radius {radius} exceeds scratch size {_maxRadius}");

            _stamp++;
            if (_stamp == uint.MaxValue)
            {
                Array.Clear(_visited, 0, _visited.Length);
                _stamp = 1;
            }

            int width = map.Width, height = map.Height;
            int sx = map.X(start), sy = map.Y(start);
            int originX = sx - _maxRadius, originY = sy - _maxRadius;

            if (include(start) == false)
                return false;

            _visited[(sy - originY) * _side + (sx - originX)] = _stamp;
            int head = 0, tail = 0;
            _queue[tail++] = start;

            while (head < tail)
            {
                int tile = _queue[head++];

                if (goal(tile))
                    return true;

                int x = tile % width, y = tile / width;

                for (int dir = 0; dir < 4; dir++)
                {
                    int nx = x, ny = y;

                    if (dir == 0) { if (y == 0) continue; ny = y - 1; }
                    else if (dir == 1) { if (y == height - 1) continue; ny = y + 1; }
                    else if (dir == 2) { if (x == 0) continue; nx = x - 1; }
                    else { if (x == width - 1) continue; nx = x + 1; }

                    if (Math.Abs(nx - sx) + Math.Abs(ny - sy) > radius)
                        continue;

                    int slot = (ny - originY) * _side + (nx - originX);
                    if (_visited[slot] == _stamp)
                        continue;

                    int next = ny * width + nx;
                    if (include(next))
                    {
                        _visited[slot] = _stamp;
                        _queue[tail++] = next;
                    }
                }
            }

            return false;
        }
    }
}
