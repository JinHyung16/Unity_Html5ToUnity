using UnityEngine;

namespace JinHyung.Core
{
    /// <summary>
    /// MonoBehaviour 싱글턴 베이스.
    ///
    /// <para>
    /// ⚠ <b>이 베이스는 <c>Awake</c> 에서 자기초기화를 하지 않는다.</b>
    /// Manager·Management 의 생성·초기화 순서는 <c>GameInitialize</c> 한 곳만 정한다 —
    /// <c>Awake</c> 에 맡기면 순서가 유니티 손에 넘어가 회차마다 다른 버그가 난다.
    /// </para>
    /// </summary>
    public abstract class MonoSingleton<T> : MonoBehaviour
        where T : MonoSingleton<T>
    {
        private static T _instance;

        /// <summary>없으면 만들어서 준다. 만드는 시점을 통제하려면 <c>HasInstance</c> 를 먼저 본다.</summary>
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject(typeof(T).Name);
                    _instance = go.AddComponent<T>();

                    // ⚠ DontDestroyOnLoad 는 재생 중에만 쓸 수 있다.
                    //    에디터에서 부르면 예외가 나고, 그러면 «검증 하네스가 이 코드를 못 쓴다».
                    //    재생을 눌러야만 서는 코드는 골든 대조·플로우 diff 에서 쓸 수 없다.
                    if (Application.isPlaying)
                        DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        /// <summary>이미 만들어져 있으면 참. <c>Instance</c> 는 없으면 만들어 버리므로 검사에는 이쪽을 쓴다.</summary>
        public static bool HasInstance
        {
            get { return _instance != null; }
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
