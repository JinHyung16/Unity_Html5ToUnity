using System;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 다시 칠할 칸 대기열 — 우선순위(작을수록 먼저) 이진 힙 + 칸마다 한 번만 넣는 비트 표.
    ///
    /// <para>
    /// 원본 `[소스] BMmXchgl.js:eo=class{constructor(e=1024){this.len=0,this.pri=new Float32Array(e)` · `to=class{#e=new eo(4096);`.
    /// 우선순위는 32비트 소수 배열에 담긴다 — 넣을 때 비교는 담기 전 값(double)으로, 꺼낼 때는 담긴 값으로 한다. 같은 순서가 나오게 그대로 둔다.
    /// </para>
    /// </summary>
    public sealed class FrontWarsTileRepaintQueue
    {
        private readonly byte[] _scheduled;
        private float[] _pri;
        private int[] _tiles;
        private int _len;

        public FrontWarsTileRepaintQueue(int tileCount, int capacity = 4096)
        {
            _scheduled = new byte[(tileCount + 7) >> 3];
            _pri = new float[Math.Max(1, capacity)];
            _tiles = new int[_pri.Length];
        }

        public int Count
        {
            get { return _len; }
        }

        /// <summary>이미 들어 있으면 무시 `[소스] schedule(e,t){let n=e>>3,r=1<<(e&7);(this.#t[n]&r)===0&&(…enqueue(e,t))}`.</summary>
        public void Schedule(int tile, double priority)
        {
            int n = tile >> 3;
            int bit = 1 << (tile & 7);

            if ((_scheduled[n] & bit) != 0)
                return;

            _scheduled[n] = (byte)(_scheduled[n] | bit);
            Enqueue(tile, priority);
        }

        /// <summary>가장 작은 우선순위의 칸을 꺼내고 표에서 지운다 `[소스] process(e,t){…dequeueTile();this.#t[e>>3]&=~(1<<(e&7))`.</summary>
        public int Dequeue()
        {
            if (_len == 0)
                throw new InvalidOperationException("heap empty");

            int top = _tiles[0];
            float last = _pri[--_len];
            int lastTile = _tiles[_len];
            int i = 0;

            for (;;)
            {
                int left = (i << 1) + 1;
                if (left >= _len)
                    break;

                int right = left + 1;
                int child = right < _len && _pri[right] < _pri[left] ? right : left;

                if (last <= _pri[child])
                    break;

                _pri[i] = _pri[child];
                _tiles[i] = _tiles[child];
                i = child;
            }

            _pri[i] = last;
            _tiles[i] = lastTile;
            _scheduled[top >> 3] = (byte)(_scheduled[top >> 3] & ~(1 << (top & 7)));
            return top;
        }

        public void Clear()
        {
            _len = 0;
            Array.Clear(_scheduled, 0, _scheduled.Length);
        }

        private void Enqueue(int tile, double priority)
        {
            if (_len == _pri.Length)
            {
                Array.Resize(ref _pri, _pri.Length << 1);
                Array.Resize(ref _tiles, _tiles.Length << 1);
            }

            int n = _len++;

            while (n > 0)
            {
                int parent = (n - 1) >> 1;
                if (priority >= _pri[parent])
                    break;

                _pri[n] = _pri[parent];
                _tiles[n] = _tiles[parent];
                n = parent;
            }

            _pri[n] = (float)priority;
            _tiles[n] = tile;
        }
    }
}
