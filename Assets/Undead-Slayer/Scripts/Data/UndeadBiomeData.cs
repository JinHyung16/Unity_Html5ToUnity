namespace JinHyung.Data
{
    /// <summary>
    /// 바이옴 하나 [소스 <c>jl = [1, 2]</c> · <c>vu</c> 씬 설정].
    ///
    /// <para>
    /// ★★ <b>두 바이옴은 «같은 세계»다.</b> 지형 노이즈도 시드도 규칙도 하나뿐이고
    /// [소스 <c>noiseBiome = () =&gt; staticBiome</c>], 갈리는 것은 <b>어느 칸 그림을 쓰나 ·
    /// 중형 오브젝트가 무엇이나 · 어디서 시작하나</b> 셋뿐이다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>시작 자리를 설정 표에 두지 않는다</b> — 바이옴마다 다른 값이라
    /// 설정 표에 두면 「하나뿐인 시작 자리」가 되어 바이옴 2 가 조용히 바이옴 1 자리에서 시작한다.
    /// </para>
    /// </summary>
    public class UndeadBiomeData : IData, IDataKey<int>
    {
        /// <summary>바이옴 번호 — <b>1 묘지 · 2 겨울 황무지</b>. 원본이 이 «숫자»로 장면을 연다.</summary>
        public int Id { get; set; }

        public string Code { get; set; }

        /// <summary>이름 문구 키 — 로비 포털 말풍선이 쓰는 것과 <b>같은 키</b>다.</summary>
        public string NameKey { get; set; }

        /// <summary>히어로 시작 자리 [소스 <c>i.x = 1===t ? 1620 : 640</c> · <c>i.y = 1===t ? 1010 : 610</c>].</summary>
        public double HeroStartX { get; set; }

        public double HeroStartY { get; set; }

        /// <summary>지형 타일셋 코드 — 그림만 갈린다(규칙은 같다).</summary>
        public string TilesetCode { get; set; }

        /// <summary>
        /// 중형 오브젝트 코드 [소스 <c>1===biome ? "graveyard_evil_tree" : "snowman"</c>].
        /// <para>상태 셋(<c>_activated</c>·<c>_inactive</c>)은 여기에 접미사를 붙인 이름이다.</para>
        /// </summary>
        public string MediumObjectCode { get; set; }

        public int Key
        {
            get { return Id; }
        }
    }
}
