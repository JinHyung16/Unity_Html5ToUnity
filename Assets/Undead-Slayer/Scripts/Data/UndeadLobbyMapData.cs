using System.Collections.Generic;

namespace JinHyung.Data
{
    /// <summary>
    /// 로비 맵 — 원본 <b>TMX 월드</b>를 그대로 옮긴 것이다 [소스 <c>Ju</c>].
    ///
    /// <para>
    /// ★ <b>타일 번호는 «다시 매겨져» 있다.</b> 원본 타일셋은 1615칸인데 로비가 실제로 쓰는 것은 65칸뿐이라,
    /// 그 65칸만 한 줄 시트로 굽고 <c>0~64</c> 로 번호를 새로 줬다 (<see cref="SourceTiles"/> 가 대응표).
    /// </para>
    ///
    /// <para>
    /// ⚠ <c>$colliders</c> 레이어만 <b>번호를 안 바꾼다</b> — 그건 그림이 아니라 «막힘 표시»라
    /// 값이 무엇이든 «있으면 막힌다»는 뜻이다.
    /// </para>
    /// </summary>
    public class UndeadLobbyMapData : IData, IDataKey<int>
    {
        public int Id { get; set; }

        public int Key
        {
            get { return Id; }
        }

        /// <summary>가로 타일 수.</summary>
        public int Width { get; set; }

        /// <summary>세로 타일 수.</summary>
        public int Height { get; set; }

        public int TileWidth { get; set; }

        public int TileHeight { get; set; }

        /// <summary>원본 타일셋의 열 수 — 대응표를 되짚을 때만 쓴다.</summary>
        public int AtlasColumns { get; set; }

        /// <summary>새 번호 → 원본 타일 번호. <b>재제작이 원본과 같은지 대조할 때</b> 쓴다.</summary>
        public List<int> SourceTiles { get; set; }

        public List<UndeadLobbyLayer> Layers { get; set; }

        /// <summary>이름 붙은 자리 — 히어로 시작점 · NPC 자리 · 포털 자리.</summary>
        public Dictionary<string, UndeadLobbyPoint> Objects { get; set; }

        /// <summary>막힘 레이어 이름 [소스 — TMX 규약].</summary>
        public const string ColliderLayer = "$colliders";

        /// <summary>바닥 레이어 이름 — 원본은 <b>정확히 하나</b>를 요구한다 [소스].</summary>
        public const string GroundLayer = "$ground";
    }

    /// <summary>
    /// 레이어 하나. <c>Cells</c> 의 키는 <b>«칸 번호»</b>(<c>y × 가로 + x</c>)를 문자열로 적은 것이다.
    /// <para>⚠ 빠진 칸은 «빈 칸»이다 — 0 번 타일이 아니다.</para>
    /// </summary>
    public class UndeadLobbyLayer
    {
        public string Name { get; set; }

        public Dictionary<string, int> Cells { get; set; }
    }

    /// <summary>맵 안의 한 점 — 원본 픽셀 좌표다 (타일 좌표가 아니다).</summary>
    public class UndeadLobbyPoint
    {
        public double X { get; set; }

        public double Y { get; set; }
    }
}
