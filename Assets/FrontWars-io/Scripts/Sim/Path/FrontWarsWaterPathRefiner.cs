using System;
using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 작은 지도 물길을 큰 지도 칸 길로 다듬는다 `[소스] Cq3gr8XO.js:var Pd=class{` · `function Ld(e,t,n,r,i,a){`.
    ///
    /// <para>
    /// 작은 칸 길 둘레를 반경 2 → 12 로 칠해 그 안에서만 넓이 우선 탐색을 하고, 둘 다 실패하면
    /// 길 전체를 감싼 상자(여백 48, 넓이 600만 칸 이하)로 한 번 더 찾는다. 모두 실패하면 null.
    /// </para>
    ///
    /// <para>
    /// 탐색 규칙: 이웃 순서 위 · 아래 · 왼쪽 · 오른쪽. 칠한 칸만 본다. 목표 칸은 땅이어도 바로 도착으로 친다.
    /// 목표가 아닌 땅 칸은 막힘으로 표시하고 넘어간다. 출발 · 목표 칸은 칠해져 있지 않아도 들어갈 수 있다.
    /// </para>
    /// </summary>
    public sealed class FrontWarsWaterPathRefiner
    {
        /// <summary>작은 칸 길 둘레를 칠하는 반경(큰 지도 칸) — 좁게 먼저 `[소스] const Md=[2,12]`.</summary>
        private static readonly int[] PaintRadii = { 2, 12 };

        private const int InitialQueueSize = 16384;
        private const int QueueShrinkThreshold = 262144;
        private const int BoxMargin = 48;
        private const int MaxBoxArea = 6_000_000;

        // 표시 값 — 0 안 칠함 · 1 칠함(아직 안 감) · 2 출발 · 3~6 들어온 방향 · 7 막힘
        private const byte MarkNone = 0;
        private const byte MarkOpen = 1;
        private const byte MarkStart = 2;
        private const byte MarkMovedDown = 3;
        private const byte MarkMovedUp = 4;
        private const byte MarkMovedRight = 5;
        private const byte MarkMovedLeft = 6;
        private const byte MarkBlocked = 7;

        private readonly FrontWarsPathMap _map;
        private readonly byte[] _mark;
        private int[] _queue;

        public FrontWarsWaterPathRefiner(FrontWarsPathMap map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _mark = new byte[map.Width * map.Height];
            _queue = new int[InitialQueueSize];
        }

        private struct Box
        {
            public int X0;
            public int Y0;
            public int X1;
            public int Y1;
        }

        /// <summary>
        /// 작은 칸 길 <paramref name="miniPath"/> 를 따라 큰 지도 칸 길(출발 · 목표 포함)을 찾는다. 못 찾으면 null.
        /// </summary>
        public int[] Refine(IReadOnlyList<int> miniPath, FrontWarsPathMap miniMap, int scale, int src, int dst)
        {
            if (miniPath.Count == 0)
                return null;

            try
            {
                foreach (int radius in PaintRadii)
                {
                    Paint(miniPath, miniMap, scale, radius, MarkOpen);
                    int[] found;

                    try
                    {
                        found = Bfs(src, dst);
                    }
                    finally
                    {
                        Paint(miniPath, miniMap, scale, radius, MarkNone);
                        ClearEndpoints(src, dst);
                    }

                    if (found != null)
                        return found;
                }

                if (TryBoxOf(miniPath, miniMap, scale, src, dst, out Box box) == false)
                    return null;

                PaintBox(box, MarkOpen);

                try
                {
                    return Bfs(src, dst);
                }
                finally
                {
                    PaintBox(box, MarkNone);
                    ClearEndpoints(src, dst);
                }
            }
            finally
            {
                if (_queue.Length > QueueShrinkThreshold)
                    _queue = new int[InitialQueueSize];
            }
        }

        private void ClearEndpoints(int src, int dst)
        {
            _mark[src] = MarkNone;
            _mark[dst] = MarkNone;
        }

        private bool TryBoxOf(IReadOnlyList<int> miniPath, FrontWarsPathMap miniMap, int scale, int src, int dst, out Box box)
        {
            int mw = miniMap.Width;
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;

            for (int i = 0; i < miniPath.Count; i++)
            {
                int node = miniPath[i];
                int bx = node % mw * scale;
                int by = node / mw * scale;

                if (bx < x0)
                    x0 = bx;
                if (by < y0)
                    y0 = by;
                if (bx + scale - 1 > x1)
                    x1 = bx + scale - 1;
                if (by + scale - 1 > y1)
                    y1 = by + scale - 1;
            }

            x0 = Math.Min(x0, Math.Min(_map.X(src), _map.X(dst)));
            y0 = Math.Min(y0, Math.Min(_map.Y(src), _map.Y(dst)));
            x1 = Math.Max(x1, Math.Max(_map.X(src), _map.X(dst)));
            y1 = Math.Max(y1, Math.Max(_map.Y(src), _map.Y(dst)));

            x0 = Math.Max(0, x0 - BoxMargin);
            y0 = Math.Max(0, y0 - BoxMargin);
            x1 = Math.Min(_map.Width - 1, x1 + BoxMargin);
            y1 = Math.Min(_map.Height - 1, y1 + BoxMargin);

            box = new Box { X0 = x0, Y0 = y0, X1 = x1, Y1 = y1 };
            return (x1 - x0 + 1) * (y1 - y0 + 1) <= MaxBoxArea;
        }

        private void PaintBox(Box box, byte value)
        {
            int w = _map.Width;
            for (int y = box.Y0; y <= box.Y1; y++)
                Array.Fill(_mark, value, y * w + box.X0, box.X1 - box.X0 + 1);
        }

        private void Paint(IReadOnlyList<int> miniPath, FrontWarsPathMap miniMap, int scale, int radius, byte value)
        {
            int w = _map.Width, h = _map.Height, mw = miniMap.Width;

            for (int i = 0; i < miniPath.Count; i++)
            {
                int node = miniPath[i];
                int bx = node % mw * scale;
                int by = node / mw * scale;
                int x0 = Math.Max(0, bx - radius);
                int x1 = Math.Min(w - 1, bx + scale - 1 + radius);
                int y0 = Math.Max(0, by - radius);
                int y1 = Math.Min(h - 1, by + scale - 1 + radius);

                for (int y = y0; y <= y1; y++)
                    Array.Fill(_mark, value, y * w + x0, x1 - x0 + 1);
            }
        }

        private int[] Bfs(int src, int dst)
        {
            int w = _map.Width;
            int lastRowStart = w * _map.Height - w;
            byte[] mark = _mark;

            if (mark[src] == MarkNone)
                mark[src] = MarkOpen;
            if (mark[dst] == MarkNone)
                mark[dst] = MarkOpen;
            if (src == dst)
                return new[] { src };

            int[] queue = _queue;
            int head = 0, tail = 0;
            queue[tail++] = src;
            mark[src] = MarkStart;

            while (head < tail)
            {
                int tile = queue[head++];

                if (tail + 4 > queue.Length)
                {
                    var bigger = new int[queue.Length * 2];
                    Array.Copy(queue, bigger, queue.Length);
                    _queue = queue = bigger;
                }

                int x = tile % w;

                if (tile >= w && mark[tile - w] == MarkOpen)
                {
                    int next = tile - w;
                    if (Visit(next, dst, MarkMovedUp, queue, ref tail))
                        return BuildPath(src, dst);
                }

                if (tile < lastRowStart && mark[tile + w] == MarkOpen)
                {
                    int next = tile + w;
                    if (Visit(next, dst, MarkMovedDown, queue, ref tail))
                        return BuildPath(src, dst);
                }

                if (x != 0 && mark[tile - 1] == MarkOpen)
                {
                    int next = tile - 1;
                    if (Visit(next, dst, MarkMovedLeft, queue, ref tail))
                        return BuildPath(src, dst);
                }

                if (x != w - 1 && mark[tile + 1] == MarkOpen)
                {
                    int next = tile + 1;
                    if (Visit(next, dst, MarkMovedRight, queue, ref tail))
                        return BuildPath(src, dst);
                }
            }

            return null;
        }

        /// <summary>칠한 이웃 한 칸을 처리한다. 목표면 참(방향만 적고 끝). 물이면 줄에 넣고, 땅이면 막힘.</summary>
        private bool Visit(int next, int dst, byte direction, int[] queue, ref int tail)
        {
            if (next == dst)
            {
                _mark[next] = direction;
                return true;
            }

            if (_map.IsWater(next))
            {
                _mark[next] = direction;
                queue[tail++] = next;
            }
            else
            {
                _mark[next] = MarkBlocked;
            }

            return false;
        }

        private int[] BuildPath(int src, int dst)
        {
            int w = _map.Width;
            var path = new List<int>();
            int tile = dst;

            for (int guard = 0; guard <= _mark.Length; guard++)
            {
                path.Add(tile);
                if (tile == src)
                {
                    path.Reverse();
                    return path.ToArray();
                }

                switch (_mark[tile])
                {
                    case MarkMovedDown:
                        tile -= w;
                        break;
                    case MarkMovedUp:
                        tile += w;
                        break;
                    case MarkMovedRight:
                        tile -= 1;
                        break;
                    case MarkMovedLeft:
                        tile += 1;
                        break;
                    default:
                        throw new InvalidOperationException("refineWaterPath: broken came-from chain");
                }
            }

            throw new InvalidOperationException("refineWaterPath: came-from walk did not terminate");
        }
    }
}
