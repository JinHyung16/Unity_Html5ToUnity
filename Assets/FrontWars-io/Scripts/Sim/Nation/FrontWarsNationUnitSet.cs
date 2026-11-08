using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 넣은 순서를 지키는 유닛 집합 — 원본 JS <c>Set</c> 대신 쓴다 (전함 도우미의 추적 목록).
    /// <para>
    /// 원본은 순회 중에 지금 보는 항목만 지운다. 그래서 순회는 <see cref="Snapshot"/> 로 복사본을 돌고,
    /// 도는 중에 지운 항목은 부르는 쪽이 <see cref="Contains"/> 로 건너뛰지 않아도 결과가 같다.
    /// </para>
    /// </summary>
    public sealed class FrontWarsNationUnitSet
    {
        private readonly List<FrontWarsUnit> _items = new List<FrontWarsUnit>();
        private readonly HashSet<FrontWarsUnit> _lookup = new HashSet<FrontWarsUnit>();

        public int Count
        {
            get { return _items.Count; }
        }

        public void Add(FrontWarsUnit unit)
        {
            if (_lookup.Add(unit))
                _items.Add(unit);
        }

        public bool Contains(FrontWarsUnit unit)
        {
            return _lookup.Contains(unit);
        }

        public void Remove(FrontWarsUnit unit)
        {
            if (_lookup.Remove(unit))
                _items.Remove(unit);
        }

        /// <summary>넣은 순서 복사본.</summary>
        public List<FrontWarsUnit> Snapshot()
        {
            return new List<FrontWarsUnit>(_items);
        }
    }
}
