using System;
using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 양방향 A* — 출발(여럿 가능)과 목표에서 번갈아 한 노드씩 넓히다가 한쪽이 꺼낸 노드를 다른 쪽이 이미 본 적 있으면 끝 `[소스] Cq3gr8XO.js:zd=class{`.
    ///
    /// <para>
    /// 한 번 <see cref="Compute"/> 에 반복 예산만큼만 돈다. 예산이 떨어지면 남은 시도 횟수가 있으면 Pending, 없으면 PathNotFound.
    /// 원본처럼 닫힌 집합이 없다 — 같은 노드를 여러 번 꺼내 다시 넓힐 수 있다. PathNotFound 뒤에 다시 부르면 이어서 찾는다.
    /// </para>
    ///
    /// <para>
    /// 휴리스틱 = 2 × 맨해튼 거리. 방향 전환 벌점이 있으면 「이 노드로 들어온 방향 ≠ 나갈 방향」일 때 더한다.
    /// ⚠ 원본은 들어온 노드 · 만난 노드를 참/거짓으로 검사한다 — 칸 번호 0 은 「없음」으로 취급된다. 그대로 따른다.
    /// </para>
    /// </summary>
    public sealed class FrontWarsBidirectionalAStar<TNode>
    {
        private const double HeuristicWeight = 2;

        private static readonly EqualityComparer<TNode> NodeComparer = EqualityComparer<TNode>.Default;

        private readonly TNode _dst;
        private readonly int _iterations;
        private readonly IFrontWarsAStarGraph<TNode> _graph;
        private readonly int _directionChangePenalty;
        private readonly TNode[] _sources;
        private readonly TNode _closestSource;

        private readonly Dictionary<TNode, TNode> _fwdCameFrom = new Dictionary<TNode, TNode>();
        private readonly Dictionary<TNode, TNode> _bwdCameFrom = new Dictionary<TNode, TNode>();
        private readonly Dictionary<TNode, double> _fwdGScore = new Dictionary<TNode, double>();
        private readonly Dictionary<TNode, double> _bwdGScore = new Dictionary<TNode, double>();
        private readonly FrontWarsScoredQueue<TNode> _fwdOpen = new FrontWarsScoredQueue<TNode>();
        private readonly FrontWarsScoredQueue<TNode> _bwdOpen = new FrontWarsScoredQueue<TNode>();
        private readonly List<TNode> _neighborScratch = new List<TNode>(8);

        private int _maxTries;
        private TNode _meetingPoint;
        private bool _completed;

        public FrontWarsBidirectionalAStar(TNode source, TNode dst, int iterations, int maxTries, IFrontWarsAStarGraph<TNode> graph, int directionChangePenalty = 0)
            : this(new[] { source }, dst, iterations, maxTries, graph, directionChangePenalty)
        {
        }

        public FrontWarsBidirectionalAStar(IReadOnlyList<TNode> sources, TNode dst, int iterations, int maxTries, IFrontWarsAStarGraph<TNode> graph, int directionChangePenalty = 0)
        {
            if (sources == null || sources.Count == 0)
                throw new ArgumentException("sources must not be empty", nameof(sources));

            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _dst = dst;
            _iterations = iterations;
            _maxTries = maxTries;
            _directionChangePenalty = directionChangePenalty;

            _sources = new TNode[sources.Count];
            for (int i = 0; i < sources.Count; i++)
                _sources[i] = sources[i];

            _closestSource = FindClosestSource(dst);

            foreach (TNode source in _sources)
            {
                _fwdGScore[source] = 0;
                _fwdOpen.Add(source, Heuristic(source, dst));
            }

            _bwdGScore[dst] = 0;
            _bwdOpen.Add(dst, Heuristic(dst, FindClosestSource(dst)));
        }

        /// <summary>목표에 휴리스틱이 가장 가까운 출발 — 같으면 앞의 것 `[소스] findClosestSource(e){`.</summary>
        private TNode FindClosestSource(TNode target)
        {
            TNode best = _sources[0];
            for (int i = 1; i < _sources.Length; i++)
            {
                if (Heuristic(target, _sources[i]) < Heuristic(target, best))
                    best = _sources[i];
            }

            return best;
        }

        /// <summary>반복 예산만큼 탐색한다 `[소스] compute(){if(this.completed)return Y.Completed`.</summary>
        public EFrontWarsPathStatus Compute()
        {
            if (_completed)
                return EFrontWarsPathStatus.Completed;

            _maxTries--;
            int budget = _iterations;

            while (_fwdOpen.IsEmpty == false && _bwdOpen.IsEmpty == false)
            {
                budget--;
                if (budget <= 0)
                    return _maxTries <= 0 ? EFrontWarsPathStatus.PathNotFound : EFrontWarsPathStatus.Pending;

                TNode fwd = _fwdOpen.Poll();
                if (_bwdGScore.ContainsKey(fwd))
                {
                    _meetingPoint = fwd;
                    _completed = true;
                    return EFrontWarsPathStatus.Completed;
                }

                ExpandNode(fwd, true);

                TNode bwd = _bwdOpen.Poll();
                if (_fwdGScore.ContainsKey(bwd))
                {
                    _meetingPoint = bwd;
                    _completed = true;
                    return EFrontWarsPathStatus.Completed;
                }

                ExpandNode(bwd, false);
            }

            return _completed ? EFrontWarsPathStatus.Completed : EFrontWarsPathStatus.PathNotFound;
        }

        private void ExpandNode(TNode node, bool forward)
        {
            int count = _graph.NeighborsInto(node, _neighborScratch);
            Dictionary<TNode, double> gScore = forward ? _fwdGScore : _bwdGScore;
            FrontWarsScoredQueue<TNode> open = forward ? _fwdOpen : _bwdOpen;
            Dictionary<TNode, TNode> cameFrom = forward ? _fwdCameFrom : _bwdCameFrom;
            TNode target = forward ? _dst : _closestSource;
            double current = gScore[node];

            for (int i = 0; i < count; i++)
            {
                TNode next = _neighborScratch[i];

                if (NodeComparer.Equals(next, target) == false && _graph.IsTraversable(node, next) == false)
                    continue;

                double tentative = current + _graph.Cost(next);
                double penalty = 0;

                if (_directionChangePenalty > 0
                    && cameFrom.TryGetValue(node, out TNode previous)
                    && IsPresent(previous)
                    && Direction(previous, node) != Direction(node, next))
                {
                    penalty = _directionChangePenalty;
                }

                double score = tentative + penalty;

                if (gScore.TryGetValue(next, out double known) == false || score < known)
                {
                    cameFrom[next] = node;
                    gScore[next] = score;
                    open.Add(next, score + Heuristic(next, target));
                }
            }
        }

        private double Heuristic(TNode a, TNode b)
        {
            return HeuristicWeight * (Math.Abs(_graph.PosX(a) - _graph.PosX(b)) + Math.Abs(_graph.PosY(a) - _graph.PosY(b)));
        }

        /// <summary>방향 번호 = sign(dx)×3 + sign(dy) `[소스] getDirection(e,t){`.</summary>
        private int Direction(TNode from, TNode to)
        {
            int dx = _graph.PosX(to) - _graph.PosX(from);
            int dy = _graph.PosY(to) - _graph.PosY(from);
            return Math.Sign(dx) * 3 + Math.Sign(dy);
        }

        /// <summary>원본의 참/거짓 검사 — 칸 번호 0 · null 은 「없음」.</summary>
        private static bool IsPresent(TNode node)
        {
            return NodeComparer.Equals(node, default(TNode)) == false;
        }

        /// <summary>
        /// 만난 노드에서 출발 쪽으로 거슬러 올라간 첫 노드 `[소스] firstNode(){`. 아직 못 만났으면 거짓.
        /// </summary>
        public bool TryGetFirstNode(out TNode node)
        {
            node = default(TNode);
            if (IsPresent(_meetingPoint) == false)
                return false;

            node = _meetingPoint;
            while (_fwdCameFrom.TryGetValue(node, out TNode previous))
                node = previous;

            return true;
        }

        /// <summary>출발 → 만난 노드 → 목표 순서의 노드 목록. 못 만났으면 빈 목록 `[소스] reconstructPath(){if(!this.meetingPoint)return[]`.</summary>
        public List<TNode> ReconstructPath()
        {
            var path = new List<TNode>();
            if (IsPresent(_meetingPoint) == false)
                return path;

            TNode node = _meetingPoint;
            path.Add(node);

            while (_fwdCameFrom.TryGetValue(node, out TNode previous))
            {
                node = previous;
                path.Add(node);
            }

            path.Reverse();

            node = _meetingPoint;
            while (_bwdCameFrom.TryGetValue(node, out TNode next))
            {
                node = next;
                path.Add(node);
            }

            return path;
        }
    }
}
