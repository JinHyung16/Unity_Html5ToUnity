using System;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 자식 4개짜리 최소 힙 — 물길 동기 A* 전용 `[소스] Cq3gr8XO.js:var Kd=class{`.
    ///
    /// <para>
    /// ⚠ 우선순위를 32비트 소수(float)로 담는다(원본 Float32Array · Math.fround).
    /// 올릴 때 「부모 ≤ 나」면 멈춤. 내릴 때 자식 4개 중 가장 작은 것(같으면 앞의 것), 「그 자식 ≥ 마지막 값」이면 멈춤.
    /// </para>
    /// </summary>
    public sealed class FrontWarsQuaternaryHeap
    {
        private const int MinCapacity = 4;
        private const int Arity = 4;

        private float[] _priority;
        private int[] _nodes;
        private int _count;

        public FrontWarsQuaternaryHeap(int capacity)
        {
            int size = Math.Max(MinCapacity, capacity);
            _priority = new float[size];
            _nodes = new int[size];
        }

        public bool IsEmpty
        {
            get { return _count == 0; }
        }

        public void Clear()
        {
            _count = 0;
        }

        public void Push(int node, double priority)
        {
            if (_count == _priority.Length)
            {
                Array.Resize(ref _priority, _priority.Length * 2);
                Array.Resize(ref _nodes, _nodes.Length * 2);
            }

            float p = (float)priority;
            int i = _count++;

            while (i > 0)
            {
                int parent = (i - 1) >> 2;
                float parentPriority = _priority[parent];
                if (parentPriority <= p)
                    break;

                _priority[i] = parentPriority;
                _nodes[i] = _nodes[parent];
                i = parent;
            }

            _priority[i] = p;
            _nodes[i] = node;
        }

        /// <summary>가장 작은 노드를 꺼낸다. 부르는 쪽이 비었는지 먼저 본다(원본도 검사 없음).</summary>
        public int Pop()
        {
            int top = _nodes[0];
            int last = --_count;

            if (last > 0)
            {
                float lastPriority = _priority[last];
                int lastNode = _nodes[last];
                int i = 0;

                while (true)
                {
                    int first = i * Arity + 1;
                    if (first >= last)
                        break;

                    int end = first + Arity < last ? first + Arity : last;
                    int best = first;
                    float bestPriority = _priority[first];

                    for (int c = first + 1; c < end; c++)
                    {
                        if (_priority[c] < bestPriority)
                        {
                            best = c;
                            bestPriority = _priority[c];
                        }
                    }

                    if (bestPriority >= lastPriority)
                        break;

                    _priority[i] = bestPriority;
                    _nodes[i] = _nodes[best];
                    i = best;
                }

                _priority[i] = lastPriority;
                _nodes[i] = lastNode;
            }

            return top;
        }
    }
}
