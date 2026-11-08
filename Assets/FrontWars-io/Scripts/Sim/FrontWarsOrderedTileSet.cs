using System.Collections;
using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 넣은 순서를 지키는 칸 집합 — 원본 JS <c>Set</c> 과 같은 순회 순서를 낸다 (플레이어 칸 · 스냅숏).
    ///
    /// <para>
    /// JS <c>Set</c> 규칙: 새로 넣으면 끝에 붙고, 이미 있으면 자리를 안 바꾸고, 뺐다가 다시 넣으면 끝으로 간다.
    /// C# <c>HashSet</c> 은 순서를 보장하지 않아 같은 시드로 다른 판이 나온다 — 그래서 따로 둔다.
    /// </para>
    /// </summary>
    public sealed class FrontWarsOrderedTileSet : IEnumerable<int>
    {
        private readonly LinkedList<int> _order = new LinkedList<int>();
        private readonly Dictionary<int, LinkedListNode<int>> _nodes = new Dictionary<int, LinkedListNode<int>>();

        public int Count
        {
            get { return _nodes.Count; }
        }

        public bool Contains(int tile)
        {
            return _nodes.ContainsKey(tile);
        }

        /// <summary>새로 넣었으면 참.</summary>
        public bool Add(int tile)
        {
            if (_nodes.ContainsKey(tile))
                return false;

            _nodes.Add(tile, _order.AddLast(tile));
            return true;
        }

        public bool Remove(int tile)
        {
            if (_nodes.TryGetValue(tile, out LinkedListNode<int> node) == false)
                return false;

            _order.Remove(node);
            _nodes.Remove(tile);
            return true;
        }

        public void Clear()
        {
            _order.Clear();
            _nodes.Clear();
        }

        /// <summary>지금 순서 그대로의 복사본 — 원본 <c>tilesSnapshot()</c>.</summary>
        public int[] Snapshot()
        {
            var copy = new int[_nodes.Count];
            _order.CopyTo(copy, 0);
            return copy;
        }

        /// <summary>첫 칸 — 원본 <c>tilesSnapshot().values().next().value</c>. 비었으면 −1.</summary>
        public int First
        {
            get { return _order.First == null ? -1 : _order.First.Value; }
        }

        public IEnumerator<int> GetEnumerator()
        {
            return _order.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
