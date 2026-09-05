namespace JinHyung.Data
{
    /// <summary>
    /// 이 게임이 쓰는 컨테이너 목록.
    ///
    /// <para>
    /// ⚠ 여기 등록하지 않으면 <b>JSON 이 있어도 아무도 안 읽는다</b> — 그런데 에러도 안 난다.
    /// </para>
    ///
    /// <para>
    /// 등록 순서가 곧 로드·검증 순서다. 블록이 레벨 코드를 참조하므로 레벨을 먼저 등록한다
    /// (참조 검사는 <c>AfterAllTableLoaded</c> 라 순서에 안 묶이지만, 읽는 사람에게 의존을 보인다).
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>파워 곡선 표(<c>BlumgiPowerCurveTable</c>)는 3회차 이후 «없다».</b> 파워는 전역 기전이라
    /// <see cref="BlumgiConfigData"/> 의 상수 넷이 정본이다 — 표를 되살리지 마라.
    /// </para>
    /// </summary>
    public static class BlumgiContainerRegister
    {
        public static void RegisterAll()
        {
            DataManager.Instance.Register(new BlumgiConfigDataContainer());
            DataManager.Instance.Register(new BlumgiLevelDataContainer());
            DataManager.Instance.Register(new BlumgiBlockDataContainer());
        }
    }
}
