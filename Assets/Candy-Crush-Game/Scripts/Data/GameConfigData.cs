namespace JinHyung.Data
{
    /// <summary>
    /// 원본 JS 상수 중 <b>기획이 표로 정하는 값</b>들.
    ///
    /// <para>
    /// 「이 값을 누가 눈으로 맞추나」로 갈랐다 — 여기 있는 것은 전부 <b>기획이 만지고 싶어질 값</b>이다.
    /// 판정 루프 상한(60·40·62·48)은 <c>BoardWidth</c> 에서 <b>파생</b>되므로 여기 없다 —
    /// 값으로 넣으면 폭을 바꿨을 때 서로 어긋난다.
    /// </para>
    /// </summary>
    public class GameConfigData : IData, IDataKey<int>
    {
        public int Id { get; set; }

        /// <summary>원본 <c>const width = 8</c> (<c>script.js:16</c>).</summary>
        public int BoardWidth { get; set; }

        /// <summary>3매치 점수. 원본 <c>score += 3</c> (<c>script.js:157</c> · <c>170</c>).</summary>
        public int ScoreMatchThree { get; set; }

        /// <summary>4매치 점수. 원본 <c>score += 4</c> (<c>script.js:130</c> · <c>143</c>).</summary>
        public int ScoreMatchFour { get; set; }

        /// <summary>Timed 모드 제한 시간(초). 원본 <c>timeLeft = 120</c> (<c>script.js:198</c>).</summary>
        public int TimedSeconds { get; set; }

        /// <summary>
        /// 게임 루프 간격(ms). 원본 <c>setInterval(gameLoop, 100)</c> (<c>script.js:195</c>).
        /// <b>이 값이 연쇄 낙하의 체감 속도 전체를 정한다</b> — 그래서 코드가 아니라 데이터다.
        /// </summary>
        public int TickIntervalMs { get; set; }

        public int Key
        {
            get { return Id; }
        }
    }
}
