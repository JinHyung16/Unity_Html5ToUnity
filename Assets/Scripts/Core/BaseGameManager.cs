using System.Collections.Generic;
using UnityEngine;

namespace JinHyung.Core
{
    /// <summary>
    /// <see cref="BaseManager"/> 들을 소유하고 <c>Update</c> 를 <b>한 곳에서 분배</b>한다.
    ///
    /// <para>
    /// 게임이 상속해 <see cref="RegisterManagers"/> 에서 자기 매니저를 등록한다.
    /// <b>등록 순서 = 초기화 순서 = 매 프레임 호출 순서</b>다.
    /// 원본 메인 루프의 호출 순서를 그대로 옮기는 자리가 여기다.
    /// </para>
    /// </summary>
    public abstract class BaseGameManager<T> : MonoSingleton<T>
        where T : BaseGameManager<T>
    {
        /// <summary><see cref="Bootstrap"/> 이 끝났는지.</summary>
        public bool IsBootstrapped { get; private set; }

        private readonly List<BaseManager> _managers = new List<BaseManager>(8);
        private readonly List<IGameUpdate> _updatables = new List<IGameUpdate>(8);

        /// <summary>
        /// 매니저를 등록하고 순서대로 초기화한다.
        /// <c>GameInitialize</c> 가 <b>데이터 로드 뒤에</b> 부른다 —
        /// 데이터가 없는 상태로 초기화하면 매니저가 폴백값으로 시작한다.
        /// </summary>
        public void Bootstrap()
        {
            if (IsBootstrapped)
                return;

            RegisterManagers();

            for (int i = 0; i < _managers.Count; i++)
                _managers[i].Initialize();

            IsBootstrapped = true;
        }

        /// <summary>게임이 여기서 <see cref="Register{TManager}"/> 를 부른다. 이 순서가 유일한 출처다.</summary>
        protected abstract void RegisterManagers();

        protected TManager Register<TManager>(TManager manager)
            where TManager : BaseManager
        {
            if (manager == null)
                return null;

            _managers.Add(manager);

            if (manager is IGameUpdate updatable)
                _updatables.Add(updatable);

            return manager;
        }

        private void Update()
        {
            if (IsBootstrapped == false)
                return;

            // ⚠ dt 클램프를 여기 임의로 넣지 않는다.
            //    원본에 상한이 있으면 그 값을 이 한 곳에만 둔다.
            //    원본에 없는 상한을 넣으면 원본과 다른 값이 나온다.
            float deltaTime = Time.deltaTime;

            for (int i = 0; i < _updatables.Count; i++)
                _updatables[i].OnUpdate(deltaTime);
        }

        protected override void OnDestroy()
        {
            // 역순으로 정리한다 — 나중에 등록된 것이 앞의 것에 의존할 수 있다.
            for (int i = _managers.Count - 1; i >= 0; i--)
                _managers[i].Dispose();

            _managers.Clear();
            _updatables.Clear();
            IsBootstrapped = false;

            base.OnDestroy();
        }
    }
}
