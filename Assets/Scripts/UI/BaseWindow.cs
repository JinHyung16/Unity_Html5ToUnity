using UnityEngine;

namespace JinHyung.UI
{
    /// <summary>
    /// 화면 한 장. <c>Window</c> 는 전체를 덮고 <c>Popup</c> 은 그 위에 뜬다.
    ///
    /// <para>
    /// <b>Window 스크립트에는 게임 로직이 없다.</b> 자식 패널을 <c>[SerializeField]</c> 로 물고
    /// 값을 받아 표기하며, 위로 올릴 일은 <c>event</c> 로만 올린다.
    /// 하위 UI 가 Manager·Management 를 직접 부르면 되돌린다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>밴드(<see cref="EWindowType"/>)는 프리팹이 든다.</b> 등록할 때 따로 넘기지 않는다 —
    /// 같은 값이 프리팹과 코드 두 곳에 있으면 화면마다 읽는 쪽이 갈리고 한쪽이 반드시 낡는다.
    /// </para>
    /// </summary>
    public abstract class BaseWindow : BaseComponent
    {
        [Header("Window")]
        [SerializeField] protected EWindowType _windowType = EWindowType.Normal;

        /// <summary>이 창이 뜨는 대역. <b>프리팹이 진실 소스다.</b></summary>
        public EWindowType WindowType
        {
            get { return _windowType; }
        }

        public EWindowState State { get; private set; }

        public bool IsOpen()
        {
            return State == EWindowState.Opened;
        }

        /// <summary>
        /// 창을 연다.
        ///
        /// <para>
        /// ★ <b>콜백보다 먼저 켠다.</b> 창은 생성 시 비활성으로 나오므로,
        /// 늦게 켜면 <see cref="OnOpened"/> 안의 <c>StartCoroutine</c> 같은 것이
        /// <b>비활성 GameObject 위에서 돌아 조용히 실패</b>한다.
        /// </para>
        /// </summary>
        public void Open()
        {
            if (State == EWindowState.Opened)
                return;

            SetEnable(true);
            State = EWindowState.Opened;

            WindowManagement.Instance.BringToFront(this);

            OnOpened();
        }

        public void Close()
        {
            if (State == EWindowState.Closed)
                return;

            OnClosing();

            SetEnable(false);
            State = EWindowState.Closed;
        }

        /// <summary>열린 직후. 값 주입·구독은 여기서.</summary>
        protected virtual void OnOpened()
        {
        }

        /// <summary>
        /// 닫히기 직전.
        /// ⚠ <b><see cref="OnOpened"/> 에서 구독한 것을 여기서 반드시 해제한다.</b>
        /// 창은 파괴되지 않고 캐시되므로, 남은 구독은 다음에 열 때 두 번 불린다.
        /// </summary>
        protected virtual void OnClosing()
        {
        }
    }
}
