using System.Collections.Generic;
using System.Text;

namespace JinHyung.Data
{
    /// <summary>
    /// 로비 맵 표 — <b>한 줄</b>이다 (맵이 하나다).
    ///
    /// <para>
    /// ⚠ <b>「레이어가 있나」가 아니라 「칸이 채워졌나」를 센다.</b> 레이어 껍데기만 있고 칸이 비면
    /// 로비가 <b>빈 바닥</b>으로 뜨는데 어느 검사에도 안 걸린다.
    /// </para>
    /// </summary>
    public class UndeadLobbyMapDataContainer : DictionaryContainer<int, UndeadLobbyMapData>
    {
        /// <summary>원본 TMX 규격 [소스 <c>Ju</c>].</summary>
        public const int OriginWidth = 30;

        public const int OriginHeight = 20;

        public const int OriginTileSize = 24;

        /// <summary>원본 레이어 수 — <c>$ground</c> · <c>walls_top</c> · <c>objects</c> · <c>walls_bottom</c> · <c>$colliders</c>.</summary>
        public const int OriginLayerCount = 5;

        /// <summary>로비가 실제로 쓰는 타일 종류 — 굽는 시트의 칸 수와 <b>같아야</b> 한다.</summary>
        public const int OriginTileKinds = 65;

        public override string Name
        {
            get { return "UndeadLobbyMap"; }
        }

        public UndeadLobbyMapData Map
        {
            get { return Get(1); }
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            UndeadLobbyMapData map = Map;

            if (map == null)
            {
                errorMessage = "로비 맵이 없다";
                return false;
            }

            if (map.Width != OriginWidth || map.Height != OriginHeight)
                sb.AppendLine($"맵 크기 {map.Width}x{map.Height} — 원본 {OriginWidth}x{OriginHeight}");

            if (map.TileWidth != OriginTileSize || map.TileHeight != OriginTileSize)
                sb.AppendLine($"타일 {map.TileWidth}x{map.TileHeight} — 원본 {OriginTileSize}");

            if (map.Layers == null || map.Layers.Count != OriginLayerCount)
                sb.AppendLine($"레이어 {(map.Layers == null ? 0 : map.Layers.Count)} — 원본 {OriginLayerCount}");

            if (map.SourceTiles == null || map.SourceTiles.Count != OriginTileKinds)
                sb.AppendLine($"타일 종류 {(map.SourceTiles == null ? 0 : map.SourceTiles.Count)} — 원본 {OriginTileKinds}");

            // ★ 껍데기만 있는 레이어를 잡는다 — 「있다」가 아니라 「채워졌다」
            if (map.Layers != null)
            {
                bool sawGround = false;
                bool sawCollider = false;

                foreach (UndeadLobbyLayer layer in map.Layers)
                {
                    if (layer.Cells == null || layer.Cells.Count == 0)
                        sb.AppendLine($"레이어 {layer.Name} 의 칸이 비었다");

                    sawGround |= layer.Name == UndeadLobbyMapData.GroundLayer;
                    sawCollider |= layer.Name == UndeadLobbyMapData.ColliderLayer;
                }

                if (sawGround == false)
                    sb.AppendLine($"{UndeadLobbyMapData.GroundLayer} 레이어가 없다 — 원본은 «정확히 하나»를 요구한다");

                if (sawCollider == false)
                    sb.AppendLine($"{UndeadLobbyMapData.ColliderLayer} 레이어가 없다 — 벽이 통째로 사라진다");
            }

            // ★ 이름 붙은 자리가 다 있나 — 하나만 빠져도 그 개체가 «화면 밖»에 선다
            string[] required =
            {
                "hero", "lobbyNpcTask1Anchor", "lobbyNpcTask2Anchor",
                "lobbyPortalBiome1Anchor", "lobbyPortalBiome2Anchor",
            };

            for (int i = 0; i < required.Length; i++)
            {
                if (map.Objects == null || map.Objects.ContainsKey(required[i]) == false)
                    sb.AppendLine($"자리 «{required[i]}» 가 없다");
            }

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }

        /// <summary>레이어를 이름으로 찾는다 — 없으면 <c>null</c>.</summary>
        public UndeadLobbyLayer Layer(string name)
        {
            UndeadLobbyMapData map = Map;

            if (map == null || map.Layers == null)
                return null;

            for (int i = 0; i < map.Layers.Count; i++)
            {
                if (map.Layers[i].Name == name)
                    return map.Layers[i];
            }

            return null;
        }

        /// <summary>이름 붙은 자리를 꺼낸다 — 없으면 오류를 찍고 <c>null</c>(폴백을 만들지 않는다).</summary>
        public UndeadLobbyPoint Point(string name)
        {
            UndeadLobbyMapData map = Map;

            if (map != null && map.Objects != null && map.Objects.TryGetValue(name, out UndeadLobbyPoint point))
                return point;

            Core.Log.Error($"{Name}: 자리 «{name}» 이 없다");
            return null;
        }
    }
}
