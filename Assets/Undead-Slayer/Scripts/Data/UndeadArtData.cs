namespace JinHyung.Data
{
    /// <summary>
    /// 아트 한 장의 <b>실측 렌더 파라미터</b> — 시트 격자 · 재생 · 표시 배율 · 피벗 · 색.
    ///
    /// <para>
    /// ★ <b>애니메이션 표를 따로 두지 않는다.</b> 격자를 실측한 7종은 이 22행의 «부분집합»이라
    /// 표를 나누면 <c>Cols</c>·<c>Rows</c> 가 두 벌이 된다 (<c>SKILL.md</c> 「행 수가 상위 표와
    /// 같으면 컬럼으로 접는다」의 같은 처방).
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>여기 값은 «원본을 잰 것»이고, 아트 파일 자체는 재제작이다</b>(상용 · 저작권).
    /// 크기·배율·피벗이 같아야 화면이 원본과 같아진다.
    /// </para>
    /// </summary>
    public class UndeadArtData : IData, IDataKey<string>
    {
        public int Id { get; set; }

        /// <summary>원본 아틀라스 프레임 이름. <b>파일명이자 어드레서블 키</b>다.</summary>
        public string Code { get; set; }

        /// <summary>시트 전체 크기 (원본 px) [실측 · 아틀라스].</summary>
        public int SheetWidth { get; set; }

        public int SheetHeight { get; set; }

        /// <summary>한 컷 크기 (원본 px). 격자를 못 쟀으면 시트 크기와 같다.</summary>
        public int FrameWidth { get; set; }

        public int FrameHeight { get; set; }

        /// <summary>시트 격자 (열 × 행). 못 쟀으면 1×1 이다 — <see cref="GridMeasured"/> 로 가른다.</summary>
        public int Cols { get; set; }

        public int Rows { get; set; }

        public bool GridMeasured { get; set; }

        /// <summary>
        /// ★ <b>실제로 «도는» 컷 수</b> [실측]. <see cref="Cols"/> 와 다를 수 있다.
        /// <para>
        /// 히어로 시트는 6열인데 대기·이동 모두 <b>0~4 컷만</b> 돌았다 — 6번째 열은 관측되지 않았다.
        /// 그 열까지 순환에 넣으면 <b>원본에 없는 빈 컷이 한 프레임 깜빡인다</b>.
        /// </para>
        /// <para>⚠ 관측 못 한 컷을 «있다»고 세지 않는다. 나중에 쓰이는 것이 확인되면 그때 올린다.</para>
        /// </summary>
        public int UsedCols { get; set; }

        /// <summary>초당 프레임. <see cref="FpsMeasured"/> 가 <c>false</c> 면 0 이다 — 추정치를 넣지 않는다.</summary>
        public double Fps { get; set; }

        public bool FpsMeasured { get; set; }

        /// <summary>
        /// ★ <b>60fps 틱 몇 번마다 다음 컷으로 가나</b> [실측].
        /// <para>
        /// 원본의 컷 전환 간격이 <b>전부 60fps 의 정수 분주</b>였다 —
        /// 히어로 5틱(83.3ms) · 적 10틱(166.7) · 투사체 2틱(33.3) · 보석 7틱(116.7) ·
        /// 불 8틱(133.3) · 메테오 3틱(50.0). 즉 <b>fps 가 아니라 «틱 수»가 저작값</b>이다.
        /// </para>
        /// <para>⚠ <see cref="Fps"/> 는 <c>60 / FrameTicks60</c> 이다 — 둘이 어긋나면 컨테이너가 잡는다.</para>
        /// </summary>
        public int FrameTicks60 { get; set; }

        public bool Loop { get; set; }

        /// <summary>
        /// 표시 배율 [실측]. 캐릭터 3.0 · 보석 2.0 · 투사체 1.2 처럼 <b>개체마다 다르다</b> —
        /// PPU 하나로 통일하면 전부 어긋난다.
        /// </summary>
        public double DisplayScaleX { get; set; }

        public double DisplayScaleY { get; set; }

        /// <summary>
        /// 좌우 반전으로 방향을 내는가 [실측 — 원본 <c>enemy1</c> 의 <c>scale.x</c> 가 <b>−3</b> 이었다].
        /// <b>왼쪽용 그림을 따로 굽지 않는다.</b>
        /// </summary>
        public bool FlipsHorizontally { get; set; }

        /// <summary>
        /// 유니티 스프라이트 피벗 (0~1). ⚠ <b>y 가 원본과 뒤집혀 있다</b> —
        /// 원본(Pixi) 앵커 y 는 «위에서», 유니티 피벗 y 는 «아래에서» 잰다.
        /// 원본 <c>(0.5, 1)</c>(발밑)은 여기서 <c>(0.5, 0)</c> 이다.
        /// </summary>
        public double PivotX { get; set; }

        public double PivotY { get; set; }

        public bool PivotMeasured { get; set; }

        /// <summary>
        /// 런타임 틴트 (6자리 hex). <b>원본은 거의 전부 <c>ffffff</c></b> 다 —
        /// 즉 <b>흰색으로 굽고 색을 입히는 구조가 아니다</b>. 원색 그대로 굽는다.
        /// </summary>
        public string TintHex { get; set; }

        public double Alpha { get; set; }

        /// <summary>나인슬라이스로 늘어나는가 [실측 — <c>skill_bg</c> 6×6 이 957×20 으로 늘어난다].</summary>
        public bool NineSlice { get; set; }

        /// <summary><c>Game</c> (월드 · PPU 24) 또는 <c>Ui</c> (캔버스 · PPU 100).</summary>
        public string Category { get; set; }

        public string Key
        {
            get { return Code; }
        }
    }
}
