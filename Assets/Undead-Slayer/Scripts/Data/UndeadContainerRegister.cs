namespace JinHyung.Data
{
    /// <summary>
    /// 이 게임이 쓰는 컨테이너 목록.
    ///
    /// <para>
    /// ⚠ 여기 등록하지 않으면 <b>JSON 이 있어도 아무도 안 읽는다</b> — 그런데 <b>에러도 안 난다</b>.
    /// </para>
    ///
    /// <para>
    /// 등록 순서가 곧 로드·검증 순서다. <b>문구 표를 먼저</b> 등록한다 —
    /// 업그레이드 표가 <c>AfterAllTableLoaded</c> 에서 문구 키를 대조하기 때문이다
    /// (그 훅은 순서에 안 묶이지만, 읽는 사람에게 의존을 보인다).
    /// </para>
    /// </summary>
    public static class UndeadContainerRegister
    {
        public static void RegisterAll()
        {
            DataManager.Instance.Register(new UndeadConfigDataContainer());
            DataManager.Instance.Register(new UndeadTextDataContainer());
            DataManager.Instance.Register(new UndeadLevelDataContainer());
            DataManager.Instance.Register(new UndeadEnemyDataContainer());
            DataManager.Instance.Register(new UndeadUpgradeDataContainer());
            DataManager.Instance.Register(new UndeadArtDataContainer());
            DataManager.Instance.Register(new UndeadPopupDataContainer());
        }
    }
}
