namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// HUD 에 넘기는 <b>표기값</b> 스냅샷.
    ///
    /// <para>
    /// ⚠ <b>내부 코드를 담지 않는다.</b> <c>W1L3</c> 같은 원본 레벨 id 는 로그·대조용이지 화면 문구가 아니다 —
    /// 담아 두면 둘 다 문자열이라 컴파일도 통과하고 화면에도 그대로 뜬다.
    /// </para>
    ///
    /// <para>
    /// 원본 HUD 실측 [`03_데이터.md` 문구 사전 · `04_UIUX규칙.md` 2-b]:
    /// 상단 <c>WORLD 1</c> · 레벨 도트 n/5 · 월드 선택 화면의 진행률 <c>20%</c>.
    /// ⚠ 원본은 <b>전부 대문자</b>로 그린다 (의도된 차이 #5 — 대체 서체가 올캡이 아니면 표시 직전 <c>ToUpper()</c>).
    /// </para>
    /// </summary>
    public struct BlumgiHudSnapshot
    {
        /// <summary>상단 문구. 원본 형식은 <c>"WORLD " + 숫자</c> 다.</summary>
        public string WorldText;

        /// <summary>레벨 도트에서 «채울» 개수 (1부터).</summary>
        public int LevelStep;

        /// <summary>레벨 도트 전체 개수. 월드당 5 [실측 — 진행률 20% 로 교차 확인].</summary>
        public int LevelStepCount;

        /// <summary>월드 선택 화면 진행률 문구. 원본 형식은 <c>정수 + "%"</c> 다.</summary>
        public string ProgressPercentText;
    }
}
