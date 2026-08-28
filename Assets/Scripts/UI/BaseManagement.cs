using System.Collections.Generic;
using JinHyung.Core;
using UnityEngine;

namespace JinHyung.UI
{
    /// <summary>
    /// <b>창을 조종하는 쪽.</b> 게임 로직은 Manager 가 들고, 창을 열고 닫고 값을 넣는 일은 여기가 한다.
    ///
    /// <para>
    /// 호출 방향은 이렇다 — 하위 UI 는 위를 <b>직접 부르지 않는다.</b>
    /// </para>
    /// <code>
    /// Window.OnSomething  (event)
    ///    -> XxxManagement.HandleSomething
    ///       -> XxxManager.DoSomething
    /// </code>
    ///
    /// <para>
    /// ⚠ <b><c>Awake</c> 에서 자기초기화를 하지 않는다.</b>
    /// <c>GameInitialize</c> 가 Manager 를 다 세운 뒤 <see cref="Initialize"/> 를 부른다 —
    /// 순서를 유니티에 맡기면 Manager 가 없는 시점에 Management 가 먼저 깨어난다.
    /// </para>
    /// </summary>
    public abstract class BaseManagement : MonoBehaviour
    {
        private readonly List<IWindowKey> _windowKeys = new List<IWindowKey>(4);

        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            if (IsInitialized)
                return;

            AddWindows();
            OnInitialize();
            IsInitialized = true;
        }

        public void Dispose()
        {
            if (IsInitialized == false)
                return;

            OnDispose();
            _windowKeys.Clear();
            IsInitialized = false;
        }

        /// <summary>
        /// 이 Management 가 책임지는 창을 전부 등록한다.
        /// <b>등록하지 않은 창은 <see cref="CloseAllWindows"/> 에 안 걸린다</b> —
        /// 그래서 「닫아야 하는데 안 닫히는 창」이 생긴다.
        /// </summary>
        protected abstract void AddWindows();

        /// <summary>이벤트 구독은 여기서.</summary>
        protected virtual void OnInitialize()
        {
        }

        /// <summary>⚠ <see cref="OnInitialize"/> 에서 구독한 것을 여기서 반드시 해제한다.</summary>
        protected virtual void OnDispose()
        {
        }

        protected void RegisterWindow(IWindowKey key)
        {
            if (key == null)
            {
                Log.Error($"{GetType().Name}: null 키를 등록하려 했다.");
                return;
            }

            if (_windowKeys.Contains(key))
                return;

            _windowKeys.Add(key);
        }

        /// <summary>
        /// 창을 받는다. 없으면 만들어서 준다.
        /// <para>
        /// ⚠ <b><c>OpenWindow</c> 는 없다.</b> 받아서 <b>창 자신의 <c>Open()</c></b> 을 부른다 —
        /// 창마다 열 때 넘길 것이 다르기 때문이다.
        /// </para>
        /// </summary>
        protected T GetWindow<T>(WindowKey<T> key)
            where T : BaseWindow
        {
            return WindowManagement.Instance.GetWindow(key);
        }

        /// <summary>이미 만들어진 창만 닫는다. 없으면 아무것도 안 한다 (닫으려고 만들지 않는다).</summary>
        protected void CloseWindow(IWindowKey key)
        {
            var window = WindowManagement.Instance.FindCreated(key);

            if (window == null)
                return;

            window.Close();
        }

        protected bool IsWindowOpen(IWindowKey key)
        {
            var window = WindowManagement.Instance.FindCreated(key);

            if (window == null)
                return false;

            return window.IsOpen();
        }

        /// <summary>이 Management 가 등록한 창을 전부 닫는다. 화면 전이에서 쓴다.</summary>
        protected void CloseAllWindows()
        {
            for (int i = 0; i < _windowKeys.Count; i++)
                CloseWindow(_windowKeys[i]);
        }
    }
}
