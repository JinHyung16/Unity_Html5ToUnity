using System;
using System.Collections.Generic;

namespace JinHyung.Core
{
    /// <summary>
    /// 화면 전이를 <b>한 곳으로 모은다.</b> 원본의 화면 전환 함수에 해당한다.
    ///
    /// <para>
    /// 전이가 Management 마다 흩어지면 「원본에 없는 창이 한 겹 더 뜨는」 결함이 생겨도
    /// 아무도 못 본다 — 창별 프리팹 점수는 전부 높기 때문이다.
    /// 전이가 여기 하나를 지나면 플로우 diff 를 돌릴 수 있다.
    /// </para>
    ///
    /// <para>
    /// 화면 종류 enum 은 <b>게임이 정의한다.</b> 게임마다 화면이 다르므로 공용에 두지 않는다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>원본에 없는 전이를 추가하지 않는다.</b>
    /// 전이표는 학습 산출물의 게임 플로우 문서 그대로 만든다.
    /// </para>
    /// </summary>
    public class GameFlow<TScreen>
        where TScreen : struct, Enum
    {
        /// <summary>지금 열려 있는 화면.</summary>
        public TScreen Current { get; private set; }

        /// <summary>
        /// <b>화면에 «한 번이라도» 들어갔는가.</b> 첫 진입을 가리는 데 쓴다 — 아래 <c>ChangeScreen</c> 참고.
        /// </summary>
        public bool HasEntered { get; private set; }

        /// <summary>화면이 바뀌면 (이전, 다음) 으로 알린다. 각 Management 가 구독해 자기 창을 열고 닫는다.</summary>
        public event Action<TScreen, TScreen> OnScreenChanged;

        /// <summary>
        /// 화면을 바꾼다. 같은 화면으로의 재진입은 «무시»한다.
        ///
        /// <para>
        /// ★★ <b>단 «첫 진입»만은 같아도 알린다.</b> <c>Current</c> 의 초기값은 <c>default(TScreen)</c> —
        /// 즉 <b>enum 의 0 번 값</b>이다. 첫 화면이 하필 0 번이면 (게임 대부분이 그렇다 — 로비·대기가
        /// 맨 위에 온다) 「바뀐 게 없다」로 되돌아 나가 <b>첫 화면 이벤트가 통째로 안 뜬다</b>.
        /// </para>
        ///
        /// <para>
        /// ⚠ 그 증상은 <b>「첫 화면 창만 안 열린다」</b>로 나타난다 — <c>Current</c> 는 맞게 보이고
        /// 다른 화면 전이는 전부 정상이라, 창 하나가 없다는 사실 말고는 아무 단서가 없다.
        /// <b>enum 순서를 바꿔 피하지 않는다</b> (화면 목록은 원본 플로우 그대로다). 여기서 막는다.
        /// </para>
        /// </summary>
        public void ChangeScreen(TScreen next)
        {
            if (HasEntered && EqualityComparer<TScreen>.Default.Equals(Current, next))
                return;

            HasEntered = true;

            var prev = Current;
            Current = next;
            OnScreenChanged?.Invoke(prev, next);
        }

        /// <summary>⚠ 구독을 전부 끊는다. 소유자가 정리될 때 반드시 부른다.</summary>
        public void Clear()
        {
            OnScreenChanged = null;
        }
    }
}
