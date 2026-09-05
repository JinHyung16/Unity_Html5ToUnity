using JinHyung.Core;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 이 게임의 매니저 소유자. <b>등록 순서 = 초기화 순서 = 매 틱 호출 순서</b>다.
    ///
    /// <para>
    /// 원본은 메인 루프가 하나뿐이고 그 안에서 도는 것도 «공 하나»라 매니저가 하나다.
    /// CLAUDE.md 「그 게임이 실제로 부르는 것만 만든다」 — 안 쓰는 매니저를 미리 세우지 않는다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>원본 메인 루프에 «정지 게이트»가 없다</b> [실측 `02_시스템_홀드샷.md` §7] —
    /// 홀드 중에도 공이 계속 낙하한다. 여기에 「창이 열려 있으면 멈춘다」를 넣으면 원본과 갈린다.
    /// </para>
    /// </summary>
    public class BlumgiGameRoot : BaseGameManager<BlumgiGameRoot>
    {
        /// <summary>화면 전이는 <b>여기 하나</b>를 지난다. 흩어지면 플로우 diff 를 못 돌린다.</summary>
        public GameFlow<EBlumgiScreenType> GameFlow { get; private set; }

        public BlumgiGameManager Game { get; private set; }

        protected override void RegisterManagers()
        {
            GameFlow = new GameFlow<EBlumgiScreenType>();
            Game = Register(new BlumgiGameManager());
        }

        /// <summary>
        /// 골인 → <b>입력 없이 자동으로 다음 레벨</b> [실측 전이 그래프].
        ///
        /// <para>
        /// ★★ <b>패스 ③ 이 «부르는 시점»을 든다.</b> 패스 ① 은 골인 즉시 불렀는데,
        /// 원본은 골인과 다음 레벨 표시 사이에 <b>≈ 2 620 ms 의 연출 구간</b>이 있다
        /// (플래시 420 + <c>YES!</c> 1 550 + 흰색 페이드 280 [실측 05_연출 §2]).
        /// 즉시 부르면 <b>클리어 연출이 다음 레벨 위에서 돈다</b> — 원본은 «흰색이 덮은 동안» 갈아 끼운다.
        /// ⇒ <c>BlumgiClearOverlay.FadeCovered</c> 에서 부른다 (<c>BlumgiManagement</c>).
        /// </para>
        ///
        /// <para>
        /// 월드1 의 마지막(W1L5)을 깨면 원본은 W2L1 로 간다. <b>월드2 는 관측 범위 밖</b>이라
        /// 데이터가 없다 — <b>여기서는 아무 데도 안 가고 «거짓»을 돌린다.</b>
        /// 그 자리를 채우는 것은 <b>의도된 차이 #7</b>(WELCOME 복귀)이고, <b>부르는 쪽</b>이 든다
        /// (<c>BlumgiManagement.HandleFadeCovered</c>) — 화면 전이는 그쪽 소관이라서다.
        /// </para>
        /// </summary>
        /// <returns>다음 레벨로 갔으면 참. 월드1 의 끝이면 거짓이다.</returns>
        public bool AdvanceToNextLevel()
        {
            if (Game == null)
                return false;

            string next = Game.GetNextLevelCode();

            if (next == null)
            {
                Log.Success("월드1 완주 — 다음 레벨 데이터가 없다 (월드2 는 관측 범위 밖). 의도된 차이 #7 로 WELCOME 복귀");
                return false;
            }

            // 진행은 «들어간 레벨»을 남긴다 — 🏠 로 나갔다 와도 · 새로고침해도 여기서 시작한다 [실측].
            BlumgiProgress.SaveLevelCode(next);
            Game.LoadLevel(next);
            return true;
        }

        protected override void OnDestroy()
        {
            GameFlow?.Clear();
            GameFlow = null;

            base.OnDestroy();
        }
    }
}
