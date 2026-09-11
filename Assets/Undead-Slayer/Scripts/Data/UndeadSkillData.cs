namespace JinHyung.Data
{
    /// <summary>
    /// 액티브 스킬 하나 [소스 <c>Yh</c> 정의 배열].
    ///
    /// <para>
    /// ★ <b>쿨다운·키·아이콘은 여기가 정본이다.</b> 코드 <c>const</c> 로 박지 않는다 —
    /// 밸런스로 갈리는 값이고, 사람이 표에서 만진다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>동작 자체는 표에 없다.</b> 「무엇을 하는가」는 시뮬의 갈래고,
    /// 표는 <b>「어느 슬롯에 · 얼마나 자주 · 어느 키로」</b>만 든다.
    /// </para>
    /// </summary>
    public class UndeadSkillData : IData, IDataKey<string>
    {
        public int Id { get; set; }

        /// <summary>원본 스킬 id (<c>dash</c> · <c>blazing_trail</c> · <c>winter_pulse</c> · <c>flash_move</c>).</summary>
        public string Code { get; set; }

        /// <summary>HUD 슬롯 순서 [소스 — 정의 배열 순서].</summary>
        public int Order { get; set; }

        /// <summary>이름 문구 키 — <c>UndeadTextTable</c> 의 <c>Code</c> 다.</summary>
        public string NameKey { get; set; }

        /// <summary>아이콘 어드레서블 주소 [소스 <c>iconResourceAlias</c>].</summary>
        public string IconAddress { get; set; }

        /// <summary>재사용 대기 (초) [소스 <c>cooldownMs</c>].</summary>
        public double CooldownSeconds { get; set; }

        /// <summary>데스크톱 단축키 [소스 <c>desktopKeyCode</c>] — <c>Space</c> · <c>Q</c> · <c>E</c> · <c>F</c>.</summary>
        public string DesktopKey { get; set; }

        /// <summary>발동 후 «효과가 도는» 시간 (초) [소스 <c>activeDurationMs</c>].</summary>
        public double ActiveSeconds { get; set; }

        public string Key
        {
            get { return Code; }
        }
    }
}
