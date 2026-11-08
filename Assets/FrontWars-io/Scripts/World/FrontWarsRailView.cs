using System;
using System.Collections.Generic;
using UnityEngine;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 철도 그리기 — 도시 · 항구 · 공장을 잇는 선로를 칸마다 그린다 `[소스] BMmXchgl.js:ki=class{…paint(e){if(this.#t=0,this.state.getRailCount()===0)return`.
    /// <list type="bullet">
    /// <item>칸 하나에 선로 조각 하나. 모양은 시뮬이 준 종류(세로 · 가로 · 네 모서리)를 따른다.</item>
    /// <item>색은 그 칸 주인의 국경색. 주인이 없으면 흰색 `[소스] m=e.isPlayer()?e.borderColor():Di` (Di = 흰색).</item>
    /// <item>물 위 칸이면 아래에 녹슨 교각을 먼저 그린다 `[소스] if(a){let i=this.#r(r);…}`.</item>
    /// <item>줌 1 부터 서서히 보이고(투명도 = 줌−1, 최대 1), 0.2 이하면 아예 안 그린다 `[소스] n=Math.max(0,Math.min(1,t-1));if(n<=.2)return`.</item>
    /// </list>
    /// <para>그림은 우리가 새로 그린다 (원본 그림은 안 쓴다) — 종류마다 한 장씩 만들어 돌려 쓰고, 칸마다 사각형 하나로 한 메시에 모아 그린다.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FrontWarsRailView : MonoBehaviour
    {
        /// <summary>줌이 이보다 작으면 안 그린다 `[소스] if(n<=.2)return`.</summary>
        private const float MinAlpha = 0.2f;

        /// <summary>선로 그림 한 변 (px) — 칸 하나를 채운다.</summary>
        private const int TextureSize = 32;

        /// <summary>선로 두 줄의 굵기 · 간격 · 침목 굵기 · 간격 (그림 px). 원본 그림 대신 우리가 그린 값이다.</summary>
        private const int RailWidth = 3;
        private const int RailGap = 10;
        private const int SleeperWidth = 3;
        private const int SleeperStep = 8;

        /// <summary>교각 색 `[소스] SUPPORT_DARK_RUST=rgb(150,50,55)` · `SUPPORT_RUST_MID=rgb(197,69,72)`.</summary>
        private static readonly Color32 SupportDark = new Color32(150, 50, 55, 255);
        private static readonly Color32 SupportMid = new Color32(197, 69, 72, 255);

        /// <summary>주인 없는 칸의 선로 색 `[소스] Di=new Rt({r:255,g:255,b:255,a:1})`.</summary>
        private static readonly Color32 NoOwnerColor = new Color32(255, 255, 255, 255);

        [SerializeField] private int _sortingOrder = -50;

        private FrontWarsGame _game;
        private FrontWarsPlayerColors _colors;
        private FrontWarsCameraController _camera;
        private Func<FrontWarsPlayer> _me;
        private MeshRenderer _renderer;
        private MeshFilter _filter;
        private Mesh _mesh;
        private Material _material;
        private Texture2D _atlas;

        private readonly Dictionary<int, List<FrontWarsRailTile>> _rails = new Dictionary<int, List<FrontWarsRailTile>>();
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<Color32> _colorsBuffer = new List<Color32>();
        private readonly List<int> _indices = new List<int>();
        private bool _dirty;

        /// <summary>선로 칸 수 — 성능 창이 읽는다.</summary>
        public int TileCount { get; private set; }

        /// <summary>그 가운데 지금 화면에 든 칸 수 `[소스] getDebugStats(){return{total,rails,inViewport}}`.</summary>
        public int ViewportTileCount { get; private set; }

        public int RailCount => _rails.Count;

        public void Bind(FrontWarsGame game, FrontWarsPlayerColors colors, FrontWarsCameraController camera, Func<FrontWarsPlayer> me)
        {
            Unbind();
            _game = game;
            _colors = colors;
            _camera = camera;
            _me = me;
            _game.OnRailAdded += HandleAdded;
            _game.OnRailRemoved += HandleRemoved;
            _game.OnRailSplit += HandleSplit;
            EnsureResources();
            _dirty = true;
        }

        public void Unbind()
        {
            if (_game != null)
            {
                _game.OnRailAdded -= HandleAdded;
                _game.OnRailRemoved -= HandleRemoved;
                _game.OnRailSplit -= HandleSplit;
            }

            _game = null;
            _rails.Clear();
            TileCount = 0;
            _dirty = true;
        }

        private void HandleAdded(int id, List<FrontWarsRailTile> tiles)
        {
            if (_rails.TryGetValue(id, out List<FrontWarsRailTile> existing))
                existing.AddRange(tiles);
            else
                _rails[id] = new List<FrontWarsRailTile>(tiles);

            _dirty = true;
        }

        private void HandleRemoved(int id, List<int> tiles)
        {
            _rails.Remove(id);
            _dirty = true;
        }

        /// <summary>철도가 잘리면 옛 id 를 지우고 머리 · 꼬리를 새로 넣는다 `[소스] handleSnap(e){`.</summary>
        private void HandleSplit(int id, FrontWarsRailroad head, FrontWarsRailroad tail)
        {
            _rails.Remove(id);
            AddRailroad(head);
            AddRailroad(tail);
            _dirty = true;
        }

        private void AddRailroad(FrontWarsRailroad railroad)
        {
            if (railroad == null || _game == null)
                return;

            _rails[railroad.Id] = FrontWarsRailTypes.Compute(_game.Map, railroad.Tiles);
        }

        private void LateUpdate()
        {
            if (_game == null || _camera == null || _renderer == null)
                return;

            float alpha = Mathf.Clamp01(_camera.Zoom - 1f);

            if (_dirty)
                Rebuild();

            CountInViewport();

            if (alpha <= MinAlpha || _rails.Count == 0)
            {
                _renderer.enabled = false;
                return;
            }

            _renderer.enabled = _indices.Count > 0;
            _material.color = new Color(1f, 1f, 1f, alpha);
        }

        /// <summary>칸마다 사각형 하나 — 주인 국경색을 꼭짓점 색으로 넣고, 물 위면 교각 칸을 먼저 넣는다.</summary>
        private void Rebuild()
        {
            _dirty = false;
            _vertices.Clear();
            _uvs.Clear();
            _colorsBuffer.Clear();
            _indices.Clear();
            TileCount = 0;

            foreach (List<FrontWarsRailTile> rail in _rails.Values)
            {
                foreach (FrontWarsRailTile tile in rail)
                {
                    FrontWarsPlayer owner = _game.Owner(tile.Tile);
                    Color32 color = owner != null ? _colors.BorderColor(owner, owner == _me?.Invoke()) : NoOwnerColor;

                    if (_game.Map.IsLand(tile.Tile) == false)
                        AddQuad(tile.Tile, SupportColumn(tile.Type), SupportMid);

                    AddQuad(tile.Tile, (int)tile.Type, color);
                    TileCount++;
                }
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colorsBuffer);
            _mesh.SetTriangles(_indices, 0);
            _mesh.RecalculateBounds();
        }

        /// <summary>화면에 든 선로 칸을 센다 — 성능 창 「In viewport」.</summary>
        private void CountInViewport()
        {
            ViewportTileCount = 0;

            if (_camera == null || _game == null)
                return;

            Vector2Int a = _camera.ScreenToTile(Vector2.zero);
            Vector2Int b = _camera.ScreenToTile(new Vector2(Screen.width, Screen.height));
            int minX = Mathf.Min(a.x, b.x), maxX = Mathf.Max(a.x, b.x);
            int minY = Mathf.Min(a.y, b.y), maxY = Mathf.Max(a.y, b.y);

            foreach (List<FrontWarsRailTile> rail in _rails.Values)
            {
                foreach (FrontWarsRailTile tile in rail)
                {
                    int x = _game.Map.X(tile.Tile);
                    int y = _game.Map.Y(tile.Tile);

                    if (x >= minX && x <= maxX && y >= minY && y <= maxY)
                        ViewportTileCount++;
                }
            }
        }

        /// <summary>교각 칸 — 선로 그림 뒤에 이어 붙인 여섯 장 중 같은 모양.</summary>
        private static int SupportColumn(EFrontWarsRailType type) => 6 + (int)type;

        private void AddQuad(int tile, int column, Color32 color)
        {
            int x = _game.Map.X(tile);
            int y = _game.Map.Y(tile);
            Vector3 center = FrontWarsWorld.TileCenter(x, y);
            int start = _vertices.Count;

            _vertices.Add(center + new Vector3(-0.5f, -0.5f, 0f));
            _vertices.Add(center + new Vector3(-0.5f, 0.5f, 0f));
            _vertices.Add(center + new Vector3(0.5f, 0.5f, 0f));
            _vertices.Add(center + new Vector3(0.5f, -0.5f, 0f));

            float u0 = column / 12f;
            float u1 = (column + 1) / 12f;
            _uvs.Add(new Vector2(u0, 0f));
            _uvs.Add(new Vector2(u0, 1f));
            _uvs.Add(new Vector2(u1, 1f));
            _uvs.Add(new Vector2(u1, 0f));

            for (int i = 0; i < 4; i++)
                _colorsBuffer.Add(color);

            _indices.Add(start);
            _indices.Add(start + 1);
            _indices.Add(start + 2);
            _indices.Add(start);
            _indices.Add(start + 2);
            _indices.Add(start + 3);
        }

        private void EnsureResources()
        {
            if (_renderer != null)
                return;

            _filter = gameObject.GetComponent<MeshFilter>();
            if (_filter == null)
                _filter = gameObject.AddComponent<MeshFilter>();

            _renderer = gameObject.GetComponent<MeshRenderer>();
            if (_renderer == null)
                _renderer = gameObject.AddComponent<MeshRenderer>();

            _mesh = new Mesh { name = "FrontWarsRails", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            _filter.sharedMesh = _mesh;

            _atlas = BuildAtlas();
            _material = new Material(Shader.Find("Sprites/Default")) { name = "FrontWarsRails", mainTexture = _atlas };
            _renderer.sharedMaterial = _material;
            _renderer.sortingOrder = _sortingOrder;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        private void OnDestroy()
        {
            Unbind();
            Destroy(_mesh);
            Destroy(_material);
            Destroy(_atlas);
        }

        /// <summary>선로 여섯 모양 + 교각 여섯 모양을 가로로 이어 붙인 한 장 (칸마다 32px).</summary>
        private static Texture2D BuildAtlas()
        {
            const int columns = 12;
            var texture = new Texture2D(TextureSize * columns, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "FrontWarsRailAtlas",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[texture.width * texture.height];

            for (int column = 0; column < columns; column++)
            {
                bool support = column >= 6;
                var type = (EFrontWarsRailType)(column % 6);
                DrawCell(pixels, texture.width, column, type, support);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>한 칸 그림 — 선로면 레일 두 줄과 침목, 교각이면 굵은 기둥 하나.</summary>
        private static void DrawCell(Color32[] pixels, int width, int column, EFrontWarsRailType type, bool support)
        {
            int ox = column * TextureSize;
            bool up = type == EFrontWarsRailType.Vertical || type == EFrontWarsRailType.TopLeft || type == EFrontWarsRailType.TopRight;
            bool down = type == EFrontWarsRailType.Vertical || type == EFrontWarsRailType.BottomLeft || type == EFrontWarsRailType.BottomRight;
            bool left = type == EFrontWarsRailType.Horizontal || type == EFrontWarsRailType.TopLeft || type == EFrontWarsRailType.BottomLeft;
            bool right = type == EFrontWarsRailType.Horizontal || type == EFrontWarsRailType.TopRight || type == EFrontWarsRailType.BottomRight;

            int half = TextureSize / 2;
            int railOffset = RailGap / 2;

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    bool on = false;

                    if (support)
                    {
                        // 교각 — 선로가 가는 방향으로 굵은 띠 하나
                        bool vertical = up || down;
                        bool horizontal = left || right;
                        on = (vertical && Mathf.Abs(x - half) <= RailWidth + 1 && (up || y <= half) && (down || y >= half))
                             || (horizontal && Mathf.Abs(y - half) <= RailWidth + 1 && (left || x >= half) && (right || x <= half));
                    }
                    else
                    {
                        // 레일 두 줄 — 가는 방향마다 가운데에서 바깥으로
                        if (up && y >= half && Rail(x, half, railOffset) && Straight(y, half, TextureSize))
                            on = true;
                        if (down && y <= half && Rail(x, half, railOffset) && Straight(y, half, TextureSize))
                            on = true;
                        if (left && x <= half && Rail(y, half, railOffset) && Straight(x, half, TextureSize))
                            on = true;
                        if (right && x >= half && Rail(y, half, railOffset) && Straight(x, half, TextureSize))
                            on = true;

                        // 침목 — 방향과 직각으로 일정 간격
                        if (on == false)
                        {
                            if ((up && y >= half || down && y <= half) && y % SleeperStep < SleeperWidth && Mathf.Abs(x - half) <= RailGap)
                                on = true;
                            else if ((left && x <= half || right && x >= half) && x % SleeperStep < SleeperWidth && Mathf.Abs(y - half) <= RailGap)
                                on = true;
                        }
                    }

                    if (on == false)
                        continue;

                    Color32 color = support ? (x + y) % 6 < 3 ? SupportDark : SupportMid : new Color32(255, 255, 255, 255);
                    pixels[y * width + ox + x] = color;
                }
            }
        }

        /// <summary>가운데에서 <paramref name="offset"/> 만큼 떨어진 두 줄 안인가.</summary>
        private static bool Rail(int value, int center, int offset)
        {
            int d = Mathf.Abs(value - center);
            return d >= offset && d < offset + RailWidth;
        }

        private static bool Straight(int value, int center, int size) => value >= 0 && value < size;
    }
}
