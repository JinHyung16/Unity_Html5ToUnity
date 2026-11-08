using UnityEngine;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 칸 좌표 ↔ 월드 좌표 — 모든 월드 뷰 · 카메라 · 입력이 이 한 곳을 쓴다.
    /// <para>1칸 = 월드 1. 맵 왼쪽 위 모서리가 (0,0), x 는 오른쪽, 칸 y 는 아래로 늘고 월드 y 는 위로 늘어 부호가 반대다. 칸 (x,y) 의 중심은 (x+0.5, −(y+0.5)).</para>
    /// </summary>
    public static class FrontWarsWorld
    {
        public static Vector3 TileCenter(int x, int y)
        {
            return new Vector3(x + 0.5f, -(y + 0.5f), 0f);
        }

        public static Vector3 TileCenter(FrontWarsTerrainMap map, int tile)
        {
            return TileCenter(map.X(tile), map.Y(tile));
        }

        /// <summary>칸 좌표(소수 가능) → 월드. 원본 좌표계 점(칸 모서리 기준)을 옮길 때 쓴다.</summary>
        public static Vector3 FromTileCoords(double x, double y)
        {
            return new Vector3((float)x, -(float)y, 0f);
        }

        /// <summary>월드 → 칸 좌표(소수). 칸 번호는 floor.</summary>
        public static Vector2 ToTileCoords(Vector3 world)
        {
            return new Vector2(world.x, -world.y);
        }

        /// <summary>월드 → 칸. 맵 밖이면 −1.</summary>
        public static int TileAt(FrontWarsTerrainMap map, Vector3 world)
        {
            int x = Mathf.FloorToInt(world.x);
            int y = Mathf.FloorToInt(-world.y);
            return map.IsValidCoord(x, y) ? map.Ref(x, y) : -1;
        }
    }
}
