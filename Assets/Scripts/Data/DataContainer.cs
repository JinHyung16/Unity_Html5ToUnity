using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace JinHyung.Data
{
    /// <summary>
    /// 컨테이너의 로드 골격. 파생 클래스는 <b>자료구조를 어떻게 담을지</b>만 정한다.
    ///
    /// <para>
    /// ★ <b>라이브러리 접점 ②.</b> <see cref="Deserialize"/> 의 한 줄이
    /// 이 프로젝트에서 <c>Newtonsoft</c> 를 부르는 유일한 호출부다
    /// (설정은 <see cref="JsonSettings"/>).
    /// </para>
    ///
    /// <para>
    /// <b>Main / Sub 두 갈래 훅</b>이 핵심이다.
    /// Main 은 기본 키 자료구조, Sub 는 그 밖의 컬럼으로 만드는 색인이다.
    /// 매 로드마다 Sub 를 <b>새로 만들므로</b> 재로드 시 중복이 누적되지 않는다.
    /// </para>
    /// </summary>
    public abstract class DataContainer<TKey, TValue> : IDataContainer
        where TValue : class, IDataKey<TKey>, IData
    {
        public abstract string Name { get; }

        public bool Loaded { get; private set; }

        public void LoadJson(string text)
        {
            List<TValue> list = Deserialize(text);
            int count = list == null ? 0 : list.Count;

            MainCollectionConstructor(count);
            SubCollectionConstructor(count);

            for (int i = 0; i < count; i++)
            {
                TValue item = list[i];

                if (item == null)
                    continue;

                MainCollectionAdd(item.Key, item);
                SubCollectionAdd(item.Key, item);
            }

            Loaded = true;
            OnLoadCompleted();
        }

        public void Clear()
        {
            MainCollectionConstructor(0);
            SubCollectionConstructor(0);
            Loaded = false;
        }

        /// <summary>
        /// ⚠ <b>첫 불량 행에서 멈추지 않는다.</b> 행마다 한 줄씩 다 모아 마지막에 한 번 넘긴다 —
        /// 조기 반환하면 행 30개가 틀렸을 때 「고치고 다시 돌리기」를 30번 하게 된다.
        /// </summary>
        public virtual bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (Loaded == false)
                sb.AppendLine("로드되지 않았다");

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }

        public virtual void AfterAllTableLoaded()
        {
        }

        /// <summary>★ 라이브러리를 부르는 유일한 곳.</summary>
        protected List<TValue> Deserialize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            return JsonConvert.DeserializeObject<List<TValue>>(text, JsonSettings.Default);
        }

        /// <summary>기본 키 자료구조를 만든다. 매 로드마다 새로 만든다.</summary>
        protected abstract void MainCollectionConstructor(int count);

        /// <summary>행 하나를 기본 자료구조에 넣는다. <b>중복 키는 조용히 덮지 않는다.</b></summary>
        protected abstract void MainCollectionAdd(TKey key, TValue value);

        /// <summary>
        /// 보조 색인을 만든다. <c>Id</c> 가 아닌 컬럼으로 조회해야 할 때만 재정의한다.
        /// <b>LINQ 로 매번 훑지 않는다</b> — 조회마다 전수 순회가 된다.
        /// </summary>
        protected virtual void SubCollectionConstructor(int count)
        {
        }

        /// <summary>행 하나를 보조 색인에 넣는다.</summary>
        protected virtual void SubCollectionAdd(TKey key, TValue value)
        {
        }

        /// <summary>로드가 끝난 뒤. 캐시 계산 등이 필요하면 여기서.</summary>
        protected virtual void OnLoadCompleted()
        {
        }
    }
}
