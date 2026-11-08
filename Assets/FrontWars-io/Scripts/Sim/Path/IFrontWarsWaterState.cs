namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 칸 상태 중 길찾기가 읽는 물 관련 비트 — 물 핵(waterNukes)이 땅을 물로 바꿀 때만 달라진다.
    ///
    /// <para>
    /// 원본은 칸 상태 16비트에 들어 있다 `[소스] Cq3gr8XO.js:static{this.WATER_OVERRIDE_BIT=15}` ·
    /// `SHORELINE_OVERRIDE_BIT=12` · `waterVersion(){return this.waterVersion_}`.
    /// 핵 실행을 옮기는 쪽이 이 인터페이스를 채운다. 물 핵이 없으면 null 로 두면 된다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 큰 지도와 작은 지도는 상태가 따로다. 원본은 큰 지도 칸을 물로 바꿀 때 작은 지도 칸(x/2, y/2)도 물로 바꾸고
    /// (`syncMiniMapWater(e){`) 작은 지도에는 해안 덮어쓰기를 하지 않는다. 버전은 큰 지도 것만 읽는다.
    /// </para>
    /// </summary>
    public interface IFrontWarsWaterState
    {
        /// <summary>땅→물 변환이 일어날 때마다 1 오른다 `[소스] convertToWater(t){ … this.waterVersion_++`.</summary>
        int WaterVersion { get; }

        /// <summary>물로 바뀐 땅인가 `[소스] isConvertedToWater(t){`.</summary>
        bool IsConvertedToWater(int tile);

        /// <summary>물로 바뀐 칸 옆이라 해안으로 표시된 땅인가 `[소스] markShoreline(t){`.</summary>
        bool IsShorelineOverride(int tile);
    }
}
