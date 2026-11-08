using System;
using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>이름을 놓을 칸 좌표 · 글자 크기(칸 단위).</summary>
    public readonly struct FrontWarsNameLocation
    {
        public readonly int X;
        public readonly int Y;
        public readonly double Size;

        public FrontWarsNameLocation(int x, int y, double size)
        {
            X = x;
            Y = y;
            Size = size;
        }
    }

    /// <summary>
    /// 지도 위 나라 이름 자리 — 가장 큰 조각 범위 안에서 내 땅(호수 포함)으로 이루어진 가장 큰 직사각형 가운데 `[소스] Cq3gr8XO.js:function Kt(e,t){ · function qt( · function Jt( · function Yt(`.
    /// <para>범위가 크면 칸을 1 · 2 · 4 · 8 · 16 · 32 로 성기게 본다. 글자 크기 = min(폭/이름길이×2, 높이/3), 세로는 크기의 1/3 만큼 올린다.</para>
    /// </summary>
    public static class FrontWarsNamePlacement
    {
        public static FrontWarsNameLocation Compute(FrontWarsGame game, FrontWarsPlayer player)
        {
            FrontWarsTileBounds bounds = player.LargestClusterBounds ?? FrontWarsTileBounds.Of(game.Map, player.BorderTiles.Items);
            int w = bounds.MaxX - bounds.MinX, h = bounds.MaxY - bounds.MinY;
            int small = Math.Min(w, h);
            int step = small < 25 ? 1 : small < 50 ? 2 : small < 100 ? 4 : small < 250 ? 8 : small < 500 ? 16 : 32;

            int x0 = (int)Math.Floor(bounds.MinX / (double)step);
            int y0 = (int)Math.Floor(bounds.MinY / (double)step);
            int x1 = (int)Math.Floor(bounds.MaxX / (double)step);
            int y1 = (int)Math.Floor(bounds.MaxY / (double)step);
            int cols = x1 - x0 + 1, rows = y1 - y0 + 1;
            var grid = new byte[cols * rows];
            FrontWarsTerrainMap map = game.Map;

            for (int gx = x0; gx <= x1; gx++)
            {
                int column = (gx - x0) * rows;
                int tx = gx * step;

                for (int gy = y0; gy <= y1; gy++)
                {
                    int ty = gy * step;
                    byte v = 0;

                    if (tx >= 0 && tx < map.Width && ty >= 0 && ty < map.Height)
                    {
                        int tile = map.Ref(tx, ty);
                        if (map.IsLake(tile) || game.OwnerSmallId(tile) == player.SmallId)
                            v = 1;
                    }

                    grid[column + (gy - y0)] = v;
                }
            }

            LargestRectangle(grid, cols, rows, out double rx, out double ry, out double rw, out double rh);
            rx *= step;
            ry *= step;
            rw *= step;
            rh *= step;

            int cxTile = (int)Math.Floor(rx + rw / 2 + bounds.MinX);
            int cyTile = (int)Math.Floor(ry + rh / 2 + bounds.MinY);
            double size = Math.Min(rw / player.Name.Length * 2, rh / 3);

            return new FrontWarsNameLocation((int)Math.Ceiling((double)cxTile), (int)Math.Ceiling(cyTile - size / 3), size);
        }

        /// <summary>
        /// 1 로 채워진 가장 큰 직사각형 — 행마다 기둥 높이를 쌓고 스택으로 푼다 `[소스] function qt(e,t,n){ · function Jt(e,t,n,r){`.
        /// <para>⚠ 원본은 넓이가 «더 클 때만» 바꾸고, 격자는 열 우선(열×행)이다.</para>
        /// </summary>
        private static void LargestRectangle(byte[] grid, int cols, int rows, out double x, out double y, out double width, out double height)
        {
            var heights = new int[cols];
            var stack = new List<int>();
            x = 0;
            y = 0;
            width = 0;
            height = 0;

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                    heights[col] = grid[col * rows + row] == 0 ? 0 : heights[col] + 1;

                stack.Clear();
                double area = width * height;

                for (int col = 0; col <= cols; col++)
                {
                    int current = col == cols ? 0 : heights[col];

                    while (stack.Count > 0 && current < heights[stack[stack.Count - 1]])
                    {
                        int h = heights[stack[stack.Count - 1]];
                        stack.RemoveAt(stack.Count - 1);
                        int w = stack.Count == 0 ? col : col - stack[stack.Count - 1] - 1;

                        if (h * w > area)
                        {
                            area = h * w;
                            x = stack.Count == 0 ? 0 : stack[stack.Count - 1] + 1;
                            y = row - h + 1;
                            width = w;
                            height = h;
                        }
                    }

                    stack.Add(col);
                }
            }
        }
    }
}
