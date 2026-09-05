using System.Collections.Generic;
using JinHyung.Core;
using JinHyung.Extensions;

namespace JinHyung.Data
{
    /// <summary>
    /// <b>한 키에 여러 행이 붙는 테이블</b>(1:N)용 컨테이너.
    /// 이 게임의 블록 표(레벨당 34~187행)와 파워 곡선 표가 그 모양이다.
    ///
    /// <para>
    /// ⚠ <b>공용 <c>Assets/Scripts/Data/</c> 에 올리지 않았다.</b> 지금 이걸 쓰는 게임은 하나뿐이라
    /// CLAUDE.md 「프레임워크 구축 규칙 — 두 번째 게임이 같은 것을 요구하면 공용으로 승격」에 따른다.
    /// (<c>DataFramework.md</c> 는 <c>DictionaryGroupContainer</c> 라는 이름으로 이걸 공용 Base 로 적어 두었는데
    /// 저장소에는 아직 없다 — 승격 판단은 PD 몫이라 여기에 게임 로컬로 둔다.)
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>중복 키를 버리지 않는다.</b> <see cref="DictionaryContainer{TKey,TValue}"/> 는 중복을 오류로 보는데,
    /// 이 게임은 «같은 자리에 블록 2개»(원본 이상 #4)와 «같은 홀드의 반복 실측»이 정상 데이터다.
    /// </para>
    /// </summary>
    public abstract class BlumgiGroupContainer<TValue> : DataContainer<int, TValue>
        where TValue : class, IDataKey<int>, IData
    {
        private List<TValue> _allValues = new List<TValue>();
        private Dictionary<string, List<TValue>> _groups = new Dictionary<string, List<TValue>>();

        /// <summary>행 수. <b>원본 행 수와 같은지가 게이트 통과 증명이다.</b></summary>
        public int Count
        {
            get { return _allValues.Count; }
        }

        /// <summary>전 행. 순서는 JSON 순서 그대로다.</summary>
        public IReadOnlyList<TValue> AllValues
        {
            get { return _allValues; }
        }

        /// <summary>그룹 하나. 없으면 <c>null</c> — <b>빈 목록 폴백을 주지 않는다.</b></summary>
        public IReadOnlyList<TValue> GetGroup(string groupKey)
        {
            if (groupKey.IsNullOrEmpty())
                return null;

            _groups.TryGetValue(groupKey, out var list);
            return list;
        }

        public int GetGroupCount(string groupKey)
        {
            IReadOnlyList<TValue> list = GetGroup(groupKey);
            return list == null ? 0 : list.Count;
        }

        public IReadOnlyCollection<string> GroupKeys
        {
            get { return _groups.Keys; }
        }

        /// <summary>행이 어느 그룹에 속하는지. 파생 클래스가 컬럼 하나를 골라 준다.</summary>
        protected abstract string GetGroupKey(TValue value);

        protected override void MainCollectionConstructor(int count)
        {
            _allValues = new List<TValue>(count);
        }

        protected override void MainCollectionAdd(int key, TValue value)
        {
            _allValues.Add(value);
        }

        protected override void SubCollectionConstructor(int count)
        {
            _groups = new Dictionary<string, List<TValue>>(8);
        }

        protected override void SubCollectionAdd(int key, TValue value)
        {
            string groupKey = GetGroupKey(value);

            if (groupKey.IsNullOrEmpty())
            {
                Log.Error($"{Name}: 그룹 키가 빈 행이 있다 — Id {key}");
                return;
            }

            if (_groups.TryGetValue(groupKey, out var list) == false)
            {
                list = new List<TValue>();
                _groups.Add(groupKey, list);
            }

            list.Add(value);
        }
    }
}
