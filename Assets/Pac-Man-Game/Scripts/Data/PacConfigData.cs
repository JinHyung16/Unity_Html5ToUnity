namespace JinHyung.Data
{
    /// <summary>
    /// 게임 상수 한 벌. 원본이 <c>script.js</c> 최상위에 흩어 둔 값들을 모았다.
    ///
    /// <para>
    /// ⚠ <b><c>GhostSpeed</c> 는 원본에서 «함수 안»에 박혀 있었다</b>
    /// (<c>moveGhost</c> 281: <c>const speed = 6 * deltaSeconds</c>).
    /// 그대로 두면 밸런스를 기획이 못 만진다 — 데이터로 올렸다.
    /// </para>
    /// </summary>
    public class PacConfigData : IData, IDataKey<int>
    {
        public int Id { get; set; }

        /// <summary>격자 열 수. 원본 <c>cols</c> = 28.</summary>
        public int Cols { get; set; }

        /// <summary>격자 행 수. 원본 <c>rows</c> = 31.</summary>
        public int Rows { get; set; }

        /// <summary>벽 한 변. 원본 <c>tileSize</c> = 16 — <b>칸보다 작다</b>.</summary>
        public int TileSize { get; set; }

        /// <summary>통로 배율. 원본 <c>corridorScale</c> = 1.25 → 칸은 20px 이다.</summary>
        public float CorridorScale { get; set; }

        /// <summary>팩맨 속도(칸/초). 원본 <c>pacman.speed</c> = 8.</summary>
        public float PacSpeed { get; set; }

        /// <summary>유령 속도(칸/초). 원본 <c>moveGhost</c> 안의 6.</summary>
        public float GhostSpeed { get; set; }

        /// <summary>방향 전환 허용 창. 원본 <c>handleInput</c> 의 0.35 — <b>조작감을 만드는 값</b>.</summary>
        public float AlignWindow { get; set; }

        /// <summary>충돌 거리(유클리드). 원본 <c>checkCollisions</c> 의 0.7.</summary>
        public float CollideDistance { get; set; }

        public int PelletScore { get; set; }
        public int PowerPelletScore { get; set; }
        public int StartLives { get; set; }

        public int PacStartCol { get; set; }
        public int PacStartRow { get; set; }

        /// <summary>입 벌림 속도. 원본 <c>drawPacman</c> 의 <c>chompSpeed</c> = 18.</summary>
        public float ChompSpeed { get; set; }

        public int Key
        {
            get { return Id; }
        }
    }
}
