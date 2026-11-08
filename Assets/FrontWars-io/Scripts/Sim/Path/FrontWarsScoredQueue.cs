using System;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 점수가 작은 노드부터 꺼내는 이진 힙 — 원본 양방향 A* 가 쓰는 FastPriorityQueue 규칙 그대로 `[소스] Cq3gr8XO.js:r.prototype._percolateDown=function(e){`.
    ///
    /// <para>
    /// 점수는 64비트 소수(double) 그대로 비교한다(원본이 일반 JS 숫자). <see cref="FrontWarsTileQueue"/> 와 규칙이 다르다:
    /// 올릴 때 「부모보다 작을 때만 올림」, 내릴 때 「오른쪽이 왼쪽보다 작을 때만 오른쪽」, 「자식이 더 작을 때만 내림」,
    /// 내리기 반복 한계는 <c>크기 / 2</c>. 같은 점수끼리의 순서가 여기서 갈리므로 바꾸지 않는다.
    /// </para>
    /// </summary>
    public sealed class FrontWarsScoredQueue<TNode>
    {
        private TNode[] _nodes;
        private double[] _scores;
        private int _size;

        public FrontWarsScoredQueue(int capacity = 64)
        {
            _nodes = new TNode[capacity];
            _scores = new double[capacity];
        }

        public int Count
        {
            get { return _size; }
        }

        public bool IsEmpty
        {
            get { return _size == 0; }
        }

        /// <summary>넣는다 `[소스] r.prototype.add=function(e){`.</summary>
        public void Add(TNode node, double score)
        {
            if (_size == _nodes.Length)
            {
                Array.Resize(ref _nodes, _size << 1);
                Array.Resize(ref _scores, _size << 1);
            }

            int i = _size++;

            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if ((score < _scores[parent]) == false)
                    break;

                _nodes[i] = _nodes[parent];
                _scores[i] = _scores[parent];
                i = parent;
            }

            _nodes[i] = node;
            _scores[i] = score;
        }

        /// <summary>가장 작은 노드를 꺼낸다. 비었으면 멈춘다 (원본은 undefined — 부르는 쪽이 비었는지 먼저 본다).</summary>
        public TNode Poll()
        {
            if (_size == 0)
                throw new InvalidOperationException("queue empty");

            TNode top = _nodes[0];

            if (_size > 1)
            {
                _size--;
                _nodes[0] = _nodes[_size];
                _scores[0] = _scores[_size];
                PercolateDown(0);
            }
            else
            {
                _size--;
            }

            return top;
        }

        private void PercolateDown(int i)
        {
            int size = _size;
            int half = size >> 1;
            TNode node = _nodes[i];
            double score = _scores[i];

            while (i < half)
            {
                int child = (i << 1) + 1;
                int right = child + 1;
                double childScore = _scores[child];

                if (right < size && _scores[right] < childScore)
                {
                    child = right;
                    childScore = _scores[right];
                }

                if ((childScore < score) == false)
                    break;

                _nodes[i] = _nodes[child];
                _scores[i] = childScore;
                i = child;
            }

            _nodes[i] = node;
            _scores[i] = score;
        }
    }
}
