using System;
using System.Collections.Generic;
using UnityEngine;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 플레이어 id → 영토 색 배정기 하나 `[소스] BMmXchgl.js:nm=class{constructor(e,t){this.assigned=new Map`.
    ///
    /// <para>
    /// 규칙 (다시 짬 · R11):
    /// ① 이미 받은 id 면 그 색. ② 남은 색이 없으면 (첫 목록 + 둘째 목록)으로 다시 채운다.
    /// ③ 배정된 색이 0개거나 50개 초과면 id 해시로 시드한 난수로 남은 색 중 하나 `[소스] this.assigned.size===0||this.assigned.size>50`.
    /// ④ 그 밖에는 남은 색마다 «배정된 색들과의 CIEDE2000 최소 거리»를 재서 가장 큰 색 (동점이면 앞쪽) `[소스] function rm(e,t){`.
    /// 고른 색은 남은 목록에서 뺀다.
    /// </para>
    /// </summary>
    public sealed class FrontWarsColorAllocator
    {
        /// <summary>이 수를 넘으면 거리 계산 대신 난수 `[소스] this.assigned.size>50`.</summary>
        public const int FarthestColorLimit = 50;

        private readonly Dictionary<string, Color32> _assigned = new Dictionary<string, Color32>();
        private readonly List<Color32> _assignedOrder = new List<Color32>();
        private readonly List<Color32> _available;
        private readonly Color32[] _fallback;
        private readonly Dictionary<int, double[]> _labCache = new Dictionary<int, double[]>();

        public FrontWarsColorAllocator(IReadOnlyList<Color32> primary, IReadOnlyList<Color32> secondary)
        {
            if (primary == null)
                throw new ArgumentNullException(nameof(primary));

            if (secondary == null)
                throw new ArgumentNullException(nameof(secondary));

            _available = new List<Color32>(primary);
            _fallback = new Color32[primary.Count + secondary.Count];

            for (int i = 0; i < primary.Count; i++)
                _fallback[i] = primary[i];

            for (int i = 0; i < secondary.Count; i++)
                _fallback[primary.Count + i] = secondary[i];
        }

        public int AssignedCount
        {
            get { return _assigned.Count; }
        }

        public int AvailableCount
        {
            get { return _available.Count; }
        }

        public bool TryGetAssigned(string id, out Color32 color)
        {
            return _assigned.TryGetValue(id, out color);
        }

        /// <summary>`[소스] assignColor(e){`</summary>
        public Color32 AssignColor(string id)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));

            if (_assigned.TryGetValue(id, out Color32 existing))
                return existing;

            if (_available.Count == 0)
                _available.AddRange(_fallback);

            int index;

            if (_assigned.Count == 0 || _assigned.Count > FarthestColorLimit)
                index = new FrontWarsRandom(FrontWarsHash.Of(id)).NextInt(0, _available.Count);
            else
                index = FarthestIndex();

            Color32 color = _available[index];
            _available.RemoveAt(index);
            _assigned.Add(id, color);
            _assignedOrder.Add(color);
            return color;
        }

        /// <summary>`[소스] function rm(e,t){…let a=e[t],o=im(om(a),n);o>r&&(r=o,i=t)}return i}`</summary>
        private int FarthestIndex()
        {
            double best = 0;
            int bestIndex = 0;

            for (int t = 0; t < _available.Count; t++)
            {
                double[] cand = Lab(_available[t]);
                double min = double.PositiveInfinity;

                // `[소스] function im(e,t){return t.reduce((t,n)=>Math.min(t,am(e,n)),1/0)}`
                for (int k = 0; k < _assignedOrder.Count; k++)
                {
                    double[] other = Lab(_assignedOrder[k]);
                    double d = FrontWarsColorMath.DeltaE2000(cand[0], cand[1], cand[2], other[0], other[1], other[2]);

                    if (d < min)
                        min = d;
                }

                if (min > best)
                {
                    best = min;
                    bestIndex = t;
                }
            }

            return bestIndex;
        }

        private double[] Lab(Color32 color)
        {
            int key = color.r | (color.g << 8) | (color.b << 16);

            if (_labCache.TryGetValue(key, out double[] lab))
                return lab;

            FrontWarsColorMath.ToLab(color, out double l, out double a, out double b);
            lab = new[] { l, a, b };
            _labCache.Add(key, lab);
            return lab;
        }
    }
}
