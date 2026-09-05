namespace JinHyung.Data
{
    /// <summary>
    /// 블록 한 칸. 좌표는 <b>블록 «중심»</b> 의 원본 world px 다 (<c>spr_BlockSimple._x/_y</c>).
    ///
    /// <para>
    /// ★ <b>셀 인덱스로 정규화하지 않는다</b> (`04_UIUX규칙.md` 2-e 이관 지침).
    /// ① 같은 레벨 안에서 무리마다 격자 «위상 25(반 칸)»가 다를 수 있고
    /// ② <b>W1L1 의 +2 어긋남이 반올림돼 사라진다</b> — 원본 이상을 몰래 고치는 것이 되어
    /// 원장 등재 없는 결함이 된다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>이 표에는 원본 이상 2건이 «그대로» 들어 있다</b> (원장 「원본 이상 목록」 #3 · #4):
    /// W1L1 의 y 627~877 무리가 +2 어긋나 있고, W1L2 의 <c>(1225, 375)</c> 에 블록이 «둘» 있다.
    /// <b>고치면 결함이다.</b>
    /// </para>
    /// </summary>
    public class BlumgiBlockData : IData, IDataKey<int>
    {
        public int Id { get; set; }

        /// <summary><see cref="BlumgiLevelData.Code"/> 참조. 이 값으로 레벨별로 묶인다.</summary>
        public string LevelCode { get; set; }

        /// <summary>블록 중심 x (world).</summary>
        public double X { get; set; }

        /// <summary>블록 중심 y (world, 아래가 +). <b>W1L5 는 음수 행이 있다</b> — 잘라내면 벽이 사라진다.</summary>
        public double Y { get; set; }

        public int Key
        {
            get { return Id; }
        }
    }
}
