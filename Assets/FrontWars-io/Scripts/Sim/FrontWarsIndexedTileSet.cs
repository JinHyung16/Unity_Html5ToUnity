using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 국경 칸 집합 — 칸마다 배열 위치를 기억해 넣기 · 빼기가 O(1) 이다 `[소스] Cq3gr8XO.js:xm=class{`.
    ///
    /// <para>
    /// ⚠ <b>빼면 마지막 칸이 그 자리로 온다.</b> 그래서 순회 순서가 넣은 순서가 아니다 —
    /// 공격이 국경 칸을 이 순서로 큐에 넣으므로, 같은 시드로 같은 판을 내려면 이 규칙을 그대로 따라야 한다.
    /// </para>
    ///
    /// <para>위치 배열은 게임 전체가 하나를 같이 쓴다(원본 <c>borderPosIndex</c>). 한 칸은 한 플레이어의 국경에만 있기 때문이다.</para>
    /// </summary>
    public sealed class FrontWarsIndexedTileSet
    {
        private readonly int[] _position;
        private readonly List<int> _tiles = new List<int>(256);

        public FrontWarsIndexedTileSet(int[] sharedPositionIndex)
        {
            _position = sharedPositionIndex;
        }

        public int Count
        {
            get { return _tiles.Count; }
        }

        /// <summary>순회용 — 원본 <c>borderTiles()</c> 가 배열 자체를 돌려준다.</summary>
        public IReadOnlyList<int> Items
        {
            get { return _tiles; }
        }

        public bool Contains(int tile)
        {
            int p = _position[tile];
            return p < _tiles.Count && _tiles[p] == tile;
        }

        public void Add(int tile)
        {
            if (Contains(tile))
                return;

            _position[tile] = _tiles.Count;
            _tiles.Add(tile);
        }

        public void Remove(int tile)
        {
            int p = _position[tile];

            if (p >= _tiles.Count || _tiles[p] != tile)
                return;

            int last = _tiles[_tiles.Count - 1];
            _tiles.RemoveAt(_tiles.Count - 1);

            if (last != tile)
            {
                _tiles[p] = last;
                _position[last] = p;
            }
        }

        public void Clear()
        {
            _tiles.Clear();
        }
    }
}
