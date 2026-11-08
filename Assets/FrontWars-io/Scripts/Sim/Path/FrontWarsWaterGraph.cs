using System;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 작은 지도 칸 단위 물길 그래프 — 작은 지도 칸마다 「물 비용」과 「옆 칸으로 건널 수 있나」 비트를 둔다 `[소스] Cq3gr8XO.js:var Dd=class{`.
    ///
    /// <para>
    /// 작은 칸 하나 = 큰 지도 scale×scale 블록(같은 크기면 1, 아니면 2).
    /// 물 비용 = 블록 안 물 칸 비용 중 가장 작은 값(물이 없으면 0).
    /// 건넘 비트 = 두 블록이 맞닿은 경계 줄에서 큰 지도 물 칸이 마주 붙은 곳이 한 군데라도 있으면 켠다.
    /// </para>
    ///
    /// <para>
    /// 비트: 1 위 · 2 아래 · 4 왼쪽 · 8 오른쪽. 물 변환 버전이 바뀌면 통째로 다시 만들고,
    /// 변환이 한 칸씩 들어오면 <see cref="ApplyConversion"/> 로 그 둘레만 고친다.
    /// </para>
    /// </summary>
    public sealed class FrontWarsWaterGraph
    {
        public const byte EdgeUp = 1;
        public const byte EdgeDown = 2;
        public const byte EdgeLeft = 4;
        public const byte EdgeRight = 8;

        private const int NotBuilt = -1;

        private readonly FrontWarsPathMap _map;
        private readonly int _miniWidth;
        private readonly int _miniHeight;
        private readonly int _scale;
        private readonly byte[] _edges;
        private readonly byte[] _cost;

        private int _builtForVersion = NotBuilt;
        private bool _dirty;

        public FrontWarsWaterGraph(FrontWarsPathMap map, FrontWarsPathMap miniMap)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            if (miniMap == null)
                throw new ArgumentNullException(nameof(miniMap));

            _miniWidth = miniMap.Width;
            _miniHeight = miniMap.Height;
            _scale = map.Width == miniMap.Width ? 1 : 2;

            int count = _miniWidth * _miniHeight;
            _edges = new byte[count];
            _cost = new byte[count];
        }

        public int Scale
        {
            get { return _scale; }
        }

        /// <summary>마지막으로 맞춘 물 변환 버전. 아직 안 만들었으면 -1 `[소스] version(){return this.builtForVersion}`.</summary>
        public int Version
        {
            get { return _builtForVersion; }
        }

        /// <summary>작은 칸별 건넘 비트. 읽기 전용으로 쓴다.</summary>
        public byte[] Edges
        {
            get { return _edges; }
        }

        /// <summary>작은 칸별 물 비용(0 = 물 없음). 읽기 전용으로 쓴다.</summary>
        public byte[] Costs
        {
            get { return _cost; }
        }

        /// <summary>버전이 바뀌었거나 지도 밖 변환이 있었으면 다시 만든다 `[소스] ensureFresh(){`.</summary>
        public void EnsureFresh()
        {
            int version = _map.WaterVersion;
            if (version == _builtForVersion && _dirty == false)
                return;

            _builtForVersion = version;
            _dirty = false;
            Rebuild();
        }

        public bool CanCross(int miniTile, int edgeBit)
        {
            return (_edges[miniTile] & edgeBit) != 0;
        }

        private void Rebuild()
        {
            int w = _map.Width, h = _map.Height, mw = _miniWidth, mh = _miniHeight, s = _scale;
            Array.Clear(_edges, 0, _edges.Length);

            // 큰 지도 칸별 물 비용 (땅 = 0)
            var water = new byte[w * h];
            for (int t = 0, n = w * h; t < n; t++)
                water[t] = _map.IsWater(t) ? (byte)_map.Cost(t) : (byte)0;

            // ① 블록 안 가장 작은 물 비용
            for (int my = 0; my < mh; my++)
            {
                int y0 = my * s;
                int rows = Math.Min(s, h - y0);

                for (int mx = 0; mx < mw; mx++)
                {
                    int x0 = mx * s;
                    int cols = Math.Min(s, w - x0);
                    int best = 0;

                    for (int dy = 0; dy < rows; dy++)
                    {
                        int rowStart = (y0 + dy) * w;
                        for (int dx = 0; dx < cols; dx++)
                        {
                            int c = water[rowStart + x0 + dx];
                            if (c != 0 && (best == 0 || c < best))
                                best = c;
                        }
                    }

                    _cost[my * mw + mx] = (byte)best;
                }
            }

            // ② 오른쪽 · 아래 경계에서 물이 마주 붙은 곳이 있나
            for (int my = 0; my < mh; my++)
            {
                int y0 = my * s;
                int rows = Math.Min(s, h - y0);

                for (int mx = 0; mx < mw; mx++)
                {
                    int x0 = mx * s;
                    int cols = Math.Min(s, w - x0);
                    int p = my * mw + mx;
                    int lastCol = x0 + s - 1;

                    if (mx + 1 < mw && lastCol + 1 < w)
                    {
                        for (int dy = 0; dy < rows; dy++)
                        {
                            int rowStart = (y0 + dy) * w;
                            if (water[rowStart + lastCol] != 0 && water[rowStart + lastCol + 1] != 0)
                            {
                                _edges[p] |= EdgeRight;
                                _edges[p + 1] |= EdgeLeft;
                                break;
                            }
                        }
                    }

                    int lastRow = y0 + s - 1;
                    if (my + 1 < mh && lastRow + 1 < h)
                    {
                        int top = lastRow * w, bottom = top + w;
                        for (int dx = 0; dx < cols; dx++)
                        {
                            int x = x0 + dx;
                            if (water[top + x] != 0 && water[bottom + x] != 0)
                            {
                                _edges[p] |= EdgeDown;
                                _edges[p + mw] |= EdgeUp;
                                break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 큰 지도 한 칸이 물로 바뀐 뒤 그 작은 칸의 비용과 네 경계만 다시 계산한다 `[소스] applyConversion(e){`.
        /// <para>아직 한 번도 안 만들었으면 무시한다(다음 <see cref="EnsureFresh"/> 가 통째로 만든다). 작은 지도 밖이면 다음에 통째로 다시 만든다.</para>
        /// </summary>
        public void ApplyConversion(int tile)
        {
            if (_builtForVersion == NotBuilt)
                return;

            int mx = _map.X(tile) / _scale, my = _map.Y(tile) / _scale;
            if (mx >= _miniWidth || my >= _miniHeight)
            {
                _dirty = true;
                return;
            }

            RecomputeNode(mx, my);
            if (mx > 0)
                RecomputeEdgeHorizontal(mx - 1, my);
            RecomputeEdgeHorizontal(mx, my);
            if (my > 0)
                RecomputeEdgeVertical(mx, my - 1);
            RecomputeEdgeVertical(mx, my);

            _builtForVersion = _map.WaterVersion;
        }

        private int WaterCostAt(int tile)
        {
            return _map.IsWater(tile) ? _map.Cost(tile) : 0;
        }

        private void RecomputeNode(int mx, int my)
        {
            int w = _map.Width, h = _map.Height, s = _scale, x0 = mx * s, y0 = my * s, best = 0;

            for (int dy = 0; dy < s && y0 + dy < h; dy++)
            {
                int rowStart = (y0 + dy) * w;
                for (int dx = 0; dx < s && x0 + dx < w; dx++)
                {
                    int c = WaterCostAt(rowStart + x0 + dx);
                    if (c != 0 && (best == 0 || c < best))
                        best = c;
                }
            }

            _cost[my * _miniWidth + mx] = (byte)best;
        }

        /// <summary>(mx, my) 와 오른쪽 칸 사이 경계 `[소스] recomputeEdgeH(e,t){`.</summary>
        private void RecomputeEdgeHorizontal(int mx, int my)
        {
            if (mx + 1 >= _miniWidth)
                return;

            int w = _map.Width, h = _map.Height, s = _scale, p = my * _miniWidth + mx, lastCol = mx * s + s - 1;
            bool open = false;

            if (lastCol + 1 < w)
            {
                int y0 = my * s;
                for (int dy = 0; dy < s && y0 + dy < h; dy++)
                {
                    int rowStart = (y0 + dy) * w;
                    if (WaterCostAt(rowStart + lastCol) != 0 && WaterCostAt(rowStart + lastCol + 1) != 0)
                    {
                        open = true;
                        break;
                    }
                }
            }

            if (open)
            {
                _edges[p] |= EdgeRight;
                _edges[p + 1] |= EdgeLeft;
            }
            else
            {
                _edges[p] &= unchecked((byte)~EdgeRight);
                _edges[p + 1] &= unchecked((byte)~EdgeLeft);
            }
        }

        /// <summary>(mx, my) 와 아래 칸 사이 경계 `[소스] recomputeEdgeV(e,t){`.</summary>
        private void RecomputeEdgeVertical(int mx, int my)
        {
            if (my + 1 >= _miniHeight)
                return;

            int w = _map.Width, h = _map.Height, s = _scale, p = my * _miniWidth + mx, lastRow = my * s + s - 1;
            bool open = false;

            if (lastRow + 1 < h)
            {
                int top = lastRow * w, bottom = top + w, x0 = mx * s;
                for (int dx = 0; dx < s && x0 + dx < w; dx++)
                {
                    if (WaterCostAt(top + x0 + dx) != 0 && WaterCostAt(bottom + x0 + dx) != 0)
                    {
                        open = true;
                        break;
                    }
                }
            }

            if (open)
            {
                _edges[p] |= EdgeDown;
                _edges[p + _miniWidth] |= EdgeUp;
            }
            else
            {
                _edges[p] &= unchecked((byte)~EdgeDown);
                _edges[p + _miniWidth] &= unchecked((byte)~EdgeUp);
            }
        }
    }
}
