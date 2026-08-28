namespace JinHyung.Data
{
    /// <summary>
    /// 이 게임이 쓰는 컨테이너 목록.
    ///
    /// <para>
    /// ⚠ <b>리플렉션 스캔을 쓰지 않는다.</b> 사람이 목록을 들고 있어야
    /// 「무엇이 로드되는가」가 <b>파일 하나로 보인다.</b>
    /// 여기 등록하지 않으면 <b>JSON 이 있어도 아무도 안 읽는다</b> — 그런데 에러도 안 난다.
    /// </para>
    /// </summary>
    public static class ContainerRegister
    {
        public static void RegisterAll()
        {
            DataManager.Instance.Register(new CandyDataContainer());
            DataManager.Instance.Register(new GameConfigDataContainer());
        }
    }
}
