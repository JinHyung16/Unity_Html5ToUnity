namespace JinHyung.Core
{
    /// <summary>
    /// 게임 로직 매니저의 베이스.
    ///
    /// <para>
    /// <b>순수 C# 이다 — <c>MonoBehaviour</c> 가 아니다.</b>
    /// 매니저마다 컴포넌트를 붙이면 <c>Update</c> 호출 순서가 유니티 손에 넘어가
    /// 원본의 「한 루프 안에서 이 순서로」가 깨진다.
    /// 매 프레임 갱신이 필요하면 <see cref="IGameUpdate"/> 를 구현한다.
    /// </para>
    ///
    /// <para>
    /// 초기화는 <c>GameInitialize</c> → <c>BaseGameManager.Bootstrap</c> 이 부른다.
    /// <b>스스로 초기화하지 않는다.</b>
    /// </para>
    /// </summary>
    public abstract class BaseManager
    {
        /// <summary>초기화가 끝났는지. 두 번 초기화되지 않게 막는 값이다.</summary>
        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            if (IsInitialized)
                return;

            OnInitialize();
            IsInitialized = true;
        }

        public void Dispose()
        {
            if (IsInitialized == false)
                return;

            OnDispose();
            IsInitialized = false;
        }

        /// <summary>이벤트 구독·초기 상태 세팅은 여기서.</summary>
        protected virtual void OnInitialize()
        {
        }

        /// <summary>⚠ <c>OnInitialize</c> 에서 구독한 것을 <b>여기서 반드시 해제</b>한다.</summary>
        protected virtual void OnDispose()
        {
        }
    }
}
