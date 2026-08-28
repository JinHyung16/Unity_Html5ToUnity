namespace JinHyung.CandyCrush
{
    /// <summary>
    /// 게임 모드. 원본은 <c>"endless"</c> / <c>"timed"</c> <b>문자열</b>이었다
    /// (<c>script.js:243~244</c>) — 판별 어휘라 enum 으로 옮겼다.
    ///
    /// <para>
    /// 세이브 키가 아니므로(원본에 저장 자체가 없다) 전환에 마이그레이션 문제가 없다.
    /// </para>
    /// </summary>
    public enum EGameModeType
    {
        None = 0,
        Endless = 1, // 원본 "endless" — 제한 시간도 종료 조건도 없다
        Timed = 2,   // 원본 "timed"   — 120초
    }
}
