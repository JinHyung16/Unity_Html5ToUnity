using System;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 공격이 먹을 칸의 우선순위 큐 — 작은 값이 먼저 `[소스] Cq3gr8XO.js:gu=class{`.
    ///
    /// <para>
    /// ⚠ <b>우선순위를 32비트 소수(float)로 담는다.</b> 원본이 <c>Float32Array</c> 라서
    /// 넣을 때 값이 반올림되고, 같아진 값끼리의 순서는 힙 규칙이 정한다. double 로 담으면 먹는 순서가 갈린다.
    /// </para>
    ///
    /// <para>힙 규칙도 원본 그대로다 — 올릴 때 「부모 이상이면 멈춤」, 내릴 때 「왼쪽·오른쪽 중 작은 쪽(같으면 왼쪽)」, 「그 자식 이상이면 멈춤」.</para>
    /// </summary>
    public sealed class FrontWarsTileQueue
    {
        private float[] _priority;
        private int[] _tiles;
        private int _length;

        public FrontWarsTileQueue(int capacity = 1024)
        {
            _priority = new float[capacity];
            _tiles = new int[capacity];
        }

        public int Count
        {
            get { return _length; }
        }

        public void Clear()
        {
            _length = 0;
        }

        public void Enqueue(int tile, double priority)
        {
            if (_length == _priority.Length)
                Grow();

            float p = (float)priority;
            int n = _length++;

            while (n > 0)
            {
                int parent = (n - 1) >> 1;

                if (p >= _priority[parent])
                    break;

                _priority[n] = _priority[parent];
                _tiles[n] = _tiles[parent];
                n = parent;
            }

            _priority[n] = p;
            _tiles[n] = tile;
        }

        /// <summary>가장 작은 칸을 꺼낸다. 비었으면 멈춘다 (원본 `heap empty`).</summary>
        public int Dequeue(out float priority)
        {
            if (_length == 0)
                throw new InvalidOperationException("heap empty");

            int top = _tiles[0];
            priority = _priority[0];

            float lastPriority = _priority[--_length];
            int lastTile = _tiles[_length];
            int i = 0;

            while (true)
            {
                int left = (i << 1) + 1;

                if (left >= _length)
                    break;

                int right = left + 1;
                int child = right < _length && _priority[right] < _priority[left] ? right : left;

                if (lastPriority <= _priority[child])
                    break;

                _priority[i] = _priority[child];
                _tiles[i] = _tiles[child];
                i = child;
            }

            _priority[i] = lastPriority;
            _tiles[i] = lastTile;
            return top;
        }

        public int Dequeue()
        {
            return Dequeue(out _);
        }

        private void Grow()
        {
            int size = _priority.Length << 1;
            Array.Resize(ref _priority, size);
            Array.Resize(ref _tiles, size);
        }
    }
}
