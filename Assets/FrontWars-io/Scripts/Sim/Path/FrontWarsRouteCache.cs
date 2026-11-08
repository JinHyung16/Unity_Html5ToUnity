using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 게임 하나가 공유하는 배 길 캐시 — 「출발 칸:목표 칸」 → 칸 길 `[소스] Cq3gr8XO.js:function nf(e){` · `storeInRouteCache(){`.
    ///
    /// <para>
    /// 최대 300개. 꽉 차면 가장 먼저 넣은 것부터 지운다. 이미 있는 키는 덮어쓰지 않는다(순서도 그대로).
    /// 원본 Map 은 앞에서만 지우고 뒤에만 넣으므로 줄(Queue)과 같은 순서가 된다.
    /// </para>
    /// <para>저장한 배열은 여러 배가 같이 읽는다 — 고치지 않는다.</para>
    /// </summary>
    public sealed class FrontWarsRouteCache
    {
        public const int MaxEntries = 300;

        private readonly Dictionary<long, int[]> _routes = new Dictionary<long, int[]>();
        private readonly Queue<long> _order = new Queue<long>();

        public int Count
        {
            get { return _routes.Count; }
        }

        private static long Key(int src, int dst)
        {
            return ((long)src << 32) | (uint)dst;
        }

        public bool TryGet(int src, int dst, out int[] path)
        {
            return _routes.TryGetValue(Key(src, dst), out path);
        }

        public void Store(int src, int dst, int[] path)
        {
            long key = Key(src, dst);
            if (_routes.ContainsKey(key))
                return;

            if (_routes.Count >= MaxEntries)
                _routes.Remove(_order.Dequeue());

            _routes[key] = path;
            _order.Enqueue(key);
        }
    }
}
