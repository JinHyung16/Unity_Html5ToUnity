using System.Collections.Generic;
using JinHyung.Core;

namespace JinHyung.Data
{
    /// <summary>
    /// 키가 유일한 테이블. <c>&lt;Key, Value&gt;</c> 로 담는다.
    ///
    /// <para>
    /// 같은 키에 여러 행이 붙는 테이블은 이걸 쓰지 않는다 —
    /// 그건 <c>&lt;Key, List&lt;Value&gt;&gt;</c> 컨테이너가 필요하고, 그 게임이 오면 그때 만든다.
    /// </para>
    /// </summary>
    public abstract class DictionaryContainer<TKey, TValue> : DataContainer<TKey, TValue>
        where TValue : class, IDataKey<TKey>, IData
    {
        private Dictionary<TKey, TValue> _dictionary = new Dictionary<TKey, TValue>();
        private List<TValue> _allValues = new List<TValue>();

        /// <summary>행 수. <b>원본 행 수와 같은지가 게이트 통과 증명이다.</b></summary>
        public int Count
        {
            get { return _allValues.Count; }
        }

        /// <summary>전 행. 순서는 JSON 순서 그대로다 — <b>순서가 곧 우선순위인 표가 있다.</b></summary>
        public IReadOnlyList<TValue> AllValues
        {
            get { return _allValues; }
        }

        /// <summary>
        /// 없으면 <c>null</c>.
        /// ⚠ <b>호출부에 폴백을 깔지 않는다.</b> 있어야 할 것이 없으면 그건 오류다 —
        /// 폴백은 어서트를 무력화하고, 그 자리는 영원히 안 고쳐진다.
        /// </summary>
        public TValue Get(TKey key)
        {
            if (key == null)
                return null;

            _dictionary.TryGetValue(key, out var value);
            return value;
        }

        public bool TryGet(TKey key, out TValue value)
        {
            if (key == null)
            {
                value = null;
                return false;
            }

            return _dictionary.TryGetValue(key, out value);
        }

        public bool ContainsKey(TKey key)
        {
            if (key == null)
                return false;

            return _dictionary.ContainsKey(key);
        }

        protected override void MainCollectionConstructor(int count)
        {
            _dictionary = new Dictionary<TKey, TValue>(count);
            _allValues = new List<TValue>(count);
        }

        protected override void MainCollectionAdd(TKey key, TValue value)
        {
            if (key == null)
            {
                Log.Error($"{Name}: 키가 null 인 행이 있다.");
                return;
            }

            // ⚠ 중복 키를 조용히 덮지 않는다. 덮으면 「행 수는 맞는데 값이 다른」 상태가 된다.
            if (_dictionary.ContainsKey(key))
            {
                Log.Error($"{Name}: 키가 중복이다 — {key}. 뒤 행을 버린다.");
                return;
            }

            _dictionary.Add(key, value);
            _allValues.Add(value);
        }
    }
}
