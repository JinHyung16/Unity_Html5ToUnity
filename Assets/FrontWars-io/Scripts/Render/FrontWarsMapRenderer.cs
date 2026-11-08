using System.Collections.Generic;
using JinHyung.Core;
using Unity.Collections;
using UnityEngine;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 지형 + 영토를 사각형 하나 · 셰이더 하나로 그린다 (칸마다 GameObject 없음 · 결정 사항).
    ///
    /// <para>
    /// 원본 구조를 따른다 `[소스] BMmXchgl.js:WebGL2 renderer active (terrain+territory on GPU)`:
    /// 지형 텍스처(칸당 RGBA8, 한 번 굽기) · 칸 값 텍스처(칸당 16비트 = 주인 id + 국경 · 방어 · 낙진 · 공격 비트) · 팔레트(8×2048).
    /// 국경 판정은 CPU 가 칸 값에 비트로 넣는다(원본과 같음) — 셰이더는 이웃을 읽지 않는다. 국경 두께 = 1칸.
    /// </para>
    ///
    /// <para>
    /// 칸 다시 칠하기도 원본 박자다 `[소스] paint(e){…let e=Math.floor(this.#t.size/10);(e===0||this._view.inSpawnPhase())&&(e=this.#t.size)`:
    /// 바뀐 칸을 (틱 + 0~0.5 난수) 우선순위로 줄 세우고, 16ms 마다 줄의 1/10(스폰 단계는 전부)을 꺼내 그 칸과 주인 있는 이웃을 다시 계산한다.
    /// </para>
    ///
    /// <para>
    /// 좌표: 칸 1개 = 월드 1 단위. 이 오브젝트 위치가 지도 왼쪽 위 모서리다. 칸 (x, y) 는 x 오른쪽 · y 아래(월드 −y)로 자란다.
    /// 회전 · 크기 변경은 지원하지 않는다.
    /// </para>
    ///
    /// <para>
    /// 쓰는 쪽: <see cref="Bind(FrontWarsGame, FrontWarsPlayer)"/> 한 번 → 시뮬이 틱을 돌린 «직후마다» <see cref="OnTick"/> (안 부르면 LateUpdate 가 틱 번호를 보고 대신 부른다 —
    /// 한 프레임에 틱이 2개 이상 돌면 앞 틱의 바뀐 칸을 놓친다).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class FrontWarsMapRenderer : MonoBehaviour
    {
        [Header("Shader")]
        [SerializeField] private Shader _shader;

        [Header("Theme")]
        [Tooltip("원본 게임은 늘 Dark 클래스를 쓴다")]
        [SerializeField] private bool _darkTheme = true;

        [SerializeField] private Color32 _waterColor = new Color32(0x0e, 0x2e, 0x5a, 255);
        [SerializeField] private Color32 _canvasBackground = new Color32(0x1b, 0x23, 0x2f, 255);

        [Header("Camera")]
        [Tooltip("줌 1 미만(화면 1px 에 칸 1개 이상)이면 부드럽게 합성한다. 비우면 늘 끔")]
        [SerializeField] private Camera _camera;

        [SerializeField] private int _sortingOrder = -100;

        /// <summary>칸 그리기 간격 `[소스] this.#s=16` · `r=t>this.#c+this.#s`.</summary>
        private const long PaintIntervalMs = 16;

        /// <summary>프레임마다 대기열의 1/이 수 `[소스] Math.floor(this.#t.size/10)`.</summary>
        private const int RepaintDivisor = 10;

        /// <summary>우선순위 흔들기 폭 `[소스] this._view.ticks()+this.#n.nextFloat(0,.5)`.</summary>
        private const double RepaintJitter = 0.5;

        /// <summary>흔들기 난수 시드 `[소스] this.#n=new fo(123)`.</summary>
        private const long RepaintSeed = 123;

        private const sbyte AttackOutgoing = 1;
        private const sbyte AttackIncoming = 2;

        private static readonly int TerrainTexId = Shader.PropertyToID("_TerrainTex");
        private static readonly int OwnerTexId = Shader.PropertyToID("_OwnerTex");
        private static readonly int PaletteTexId = Shader.PropertyToID("_PaletteTex");
        private static readonly int MapSizeId = Shader.PropertyToID("_MapSize");
        private static readonly int CanvasBgId = Shader.PropertyToID("_CanvasBg");
        private static readonly int TacticalId = Shader.PropertyToID("_Tactical");
        private static readonly int SmoothId = Shader.PropertyToID("_Smooth");

        private readonly int[] _neighbors = new int[4];
        private readonly List<int> _dirtyTiles = new List<int>(4096);
        private readonly List<FrontWarsUnit> _pendingDefensePosts = new List<FrontWarsUnit>();
        private readonly Dictionary<string, Dictionary<int, sbyte>> _attackTilesById = new Dictionary<string, Dictionary<int, sbyte>>();
        private readonly Dictionary<int, sbyte> _attackOverrides = new Dictionary<int, sbyte>();
        private readonly List<int> _previousOverrideTiles = new List<int>();
        private readonly HashSet<string> _liveAttackIds = new HashSet<string>();
        private readonly List<string> _staleAttackIds = new List<string>();

        private FrontWarsGame _game;
        private FrontWarsPlayer _myPlayer;
        private FrontWarsPlayerColors _colors;
        private FrontWarsTileRepaintQueue _queue;
        private FrontWarsRandom _repaintRandom;

        private int _width = 2000;
        private int _height = 1500;
        private ushort[] _tileCodes;
        private byte[] _terrainBytes;
        private byte[] _paletteBytes;
        private bool[] _paletteRowWritten;
        private bool _paletteDirty;
        private int _ownerBytesPerPixel = 2;

        private Texture2D _terrainTex;
        private Texture2D _ownerTex;
        private Texture2D _paletteTex;
        private Material _material;
        private Mesh _mesh;


        private bool _tactical;
        private bool _smooth;
        private long _lastPaintMs;
        private int _lastTickSeen = -1;
        private bool _warnedTickSkip;

        public FrontWarsGame Game
        {
            get { return _game; }
        }

        public bool IsBound
        {
            get { return _game != null; }
        }

        public int MapWidth
        {
            get { return _width; }
        }

        public int MapHeight
        {
            get { return _height; }
        }

        /// <summary>다시 칠할 칸이 남은 수 — 재생 검사가 «다 칠해졌나» 기다릴 때 본다.</summary>
        public int PendingRepaintCount
        {
            get { return _queue == null ? 0 : _queue.Count; }
        }

        public bool IsDarkTheme
        {
            get { return _darkTheme; }
        }

        public bool IsTacticalView
        {
            get { return _tactical; }
        }

        public FrontWarsPlayerColors Colors
        {
            get { return _colors; }
        }

        // ── 연결

        /// <summary>싱글 기본 — 첫 사람 플레이어를 «나»로 본다.</summary>
        public void Bind(FrontWarsGame game)
        {
            FrontWarsPlayer me = null;

            if (game != null)
            {
                foreach (FrontWarsPlayer p in game.AllPlayers)
                {
                    if (p.Type == EFrontWarsPlayerType.Human)
                    {
                        me = p;
                        break;
                    }
                }
            }

            Bind(game, me);
        }

        /// <summary>
        /// 게임에 붙는다 — 텍스처를 만들고 지형을 굽고 모든 칸을 한 번 칠한다 `[소스] refreshSurfaces(){`.
        /// </summary>
        /// <param name="myPlayer">
        /// «나» — 국경이 (230,230,230) 이 되고 전술 보기 관계 색 · 공격 국경의 기준이 된다 `[소스] let s=this.game.myClientID()===this.data.clientID`. null 이면 없음.
        /// </param>
        public void Bind(FrontWarsGame game, FrontWarsPlayer myPlayer)
        {
            Unbind();

            if (game == null)
            {
                Log.Error("[FrontWarsMapRenderer] Bind — game 이 null 이다");
                return;
            }

            if (_shader == null)
            {
                Log.Error("[FrontWarsMapRenderer] 셰이더가 비었다 — JinHyung/FrontWars/Territory 를 넣어야 한다");
                return;
            }

            _game = game;
            _myPlayer = myPlayer;
            _width = game.Map.Width;
            _height = game.Map.Height;
            _colors = new FrontWarsPlayerColors();
            _queue = new FrontWarsTileRepaintQueue(game.Map.TileCount);
            _repaintRandom = new FrontWarsRandom(RepaintSeed);
            _tileCodes = new ushort[game.Map.TileCount];
            _paletteBytes = new byte[FrontWarsTerritoryCode.PaletteWidth * FrontWarsTerritoryCode.PaletteHeight * 4];
            _paletteRowWritten = new bool[FrontWarsTerritoryCode.PaletteHeight];
            _lastTickSeen = game.Ticks;
            _warnedTickSkip = false;

            CreateGpuResources();
            RebuildTerrain();

            game.OnUnitUpdate += HandleUnitUpdate;

            BuildPalette();
            RefreshAll();
            Flush();
        }

        /// <summary>게임에서 떨어진다. 텍스처는 남겨 두고 다음 Bind 에서 다시 만든다.</summary>
        public void Unbind()
        {
            if (_game != null)
                _game.OnUnitUpdate -= HandleUnitUpdate;

            _game = null;
            _myPlayer = null;
            _pendingDefensePosts.Clear();
            _attackTilesById.Clear();
            _attackOverrides.Clear();
            _dirtyTiles.Clear();
        }

        /// <summary>«나»를 바꾼다 — 팔레트 행을 다시 쓴다 (색 배정은 그대로).</summary>
        public void SetMyPlayer(FrontWarsPlayer myPlayer)
        {
            _myPlayer = myPlayer;

            if (_paletteRowWritten == null)
                return;

            System.Array.Clear(_paletteRowWritten, 0, _paletteRowWritten.Length);
            ClearAttackOverrides();
            BuildPalette();
        }

        // ── 테마 · 보기

        /// <summary>
        /// 지형 식을 고른다. 참 = Dark 클래스(원본 게임이 쓰는 것), 거짓 = 기본 클래스. 지형 텍스처를 다시 굽는다 `[소스] if(this._view.theme()!==this.#t){this.regenerateSurface()`.
        /// </summary>
        public void SetTheme(bool dark)
        {
            if (_darkTheme == dark)
                return;

            _darkTheme = dark;
            RebuildTerrain();
        }

        /// <summary>물 색 토큰 (Dark 식에서만 쓰인다). 프리셋은 <see cref="FrontWarsTerrainColors.WaterDarkPreset"/> 등.</summary>
        public void SetWaterColor(Color32 water)
        {
            _waterColor = new Color32(water.r, water.g, water.b, 255);
            RebuildTerrain();
        }

        /// <summary>캔버스 배경 토큰 — 지도 밖 · 부드러운 합성의 가장자리 색. 게임 카메라 배경도 같은 색이어야 한다.</summary>
        public void SetCanvasBackground(Color32 background)
        {
            _canvasBackground = background;

            if (_material != null)
                _material.SetVector(CanvasBgId, ToVector(background));
        }

        /// <summary>
        /// 전술 보기 — 국경만 관계 색(나 · 동맹 · 금수 · 중립)으로, 나머지는 지형만 `[소스] if (uTactical == 1) { if (!border || id == 0u) return vec4(0.0);`.
        /// </summary>
        public void SetTacticalView(bool tactical)
        {
            _tactical = tactical;

            if (_material != null)
                _material.SetFloat(TacticalId, tactical ? 1f : 0f);
        }

        public void SetCamera(Camera cam)
        {
            _camera = cam;
        }

        // ── 틱 · 프레임

        /// <summary>
        /// 시뮬 틱 직후에 부른다 (<c>TileUpdates</c> 가 비워지기 전) `[소스] tick(){this.#A(),this._view.recentlyUpdatedTiles().forEach(`.
        /// </summary>
        public void OnTick()
        {
            if (_game == null)
                return;

            _lastTickSeen = _game.Ticks;
            BuildPalette();

            List<int> updates = _game.TileUpdates;
            for (int i = 0; i < updates.Count; i++)
            {
                int tile = updates[i];
                Schedule(tile);

                if (_game.Map.IsWater(tile))
                    Write(tile, 0);
            }

            ScheduleDefensePostBorders();
            SyncAttackOverrides();
        }

        private void LateUpdate()
        {
            if (_game == null)
                return;

            if (_game.Ticks != _lastTickSeen)
            {
                if (_game.Ticks - _lastTickSeen > 1 && _warnedTickSkip == false)
                {
                    _warnedTickSkip = true;
                    Log.Warning($"[FrontWarsMapRenderer] 한 프레임에 틱이 {_game.Ticks - _lastTickSeen}개 지나갔다 — 틱마다 OnTick() 을 불러야 바뀐 칸을 놓치지 않는다");
                }

                OnTick();
            }

            Paint();
            UpdateSmooth();
            Flush();
        }

        private void OnDestroy()
        {
            Unbind();
            DestroyGpuResources();
        }

        /// <summary>모든 칸을 다시 계산한다 `[소스] refreshSurfaces(){if(this.#O){this.#O.clearTiles(),this._view.forEachTile(e=>{this.renderTile(e)})`.</summary>
        public void RefreshAll()
        {
            if (_game == null)
                return;

            for (int t = 0; t < _tileCodes.Length; t++)
                RenderTile(t, false);
        }

        // ── 좌표 도우미 (카메라 · 입력)

        /// <summary>칸 가운데의 월드 좌표.</summary>
        public Vector3 TileToWorld(int x, int y)
        {
            Vector3 origin = transform.position;
            return new Vector3(origin.x + x + 0.5f, origin.y - (y + 0.5f), origin.z);
        }

        public Vector3 TileToWorld(int tile)
        {
            return TileToWorld(tile % _width, tile / _width);
        }

        /// <summary>월드 좌표 → 칸 좌표 (내림). 지도 밖이면 false — 좌표는 그래도 채운다 `[소스] toWorldSpace(e,t){…Math.floor(s+a/2)`.</summary>
        public bool WorldToTile(Vector3 world, out int x, out int y)
        {
            Vector3 origin = transform.position;
            x = Mathf.FloorToInt(world.x - origin.x);
            y = Mathf.FloorToInt(origin.y - world.y);
            return x >= 0 && y >= 0 && x < _width && y < _height;
        }

        /// <summary>월드 좌표 → 칸 번호. 지도 밖이면 −1.</summary>
        public int WorldToTile(Vector3 world)
        {
            return WorldToTile(world, out int x, out int y) ? y * _width + x : -1;
        }

        /// <summary>지도 전체의 월드 사각형 (xMin = 왼쪽, yMax = 위).</summary>
        public Rect WorldBounds
        {
            get
            {
                Vector3 origin = transform.position;
                return new Rect(origin.x, origin.y - _height, _width, _height);
            }
        }

        // ── 검사용 읽기

        /// <summary>칸의 현재 16비트 값 (다시 칠하기 대기 중이면 옛 값).</summary>
        public int GetTileCode(int tile)
        {
            return _tileCodes == null ? 0 : _tileCodes[tile];
        }

        public Color32 GetPaletteColor(int slot, int row)
        {
            int o = (row * FrontWarsTerritoryCode.PaletteWidth + slot) * 4;
            return new Color32(_paletteBytes[o], _paletteBytes[o + 1], _paletteBytes[o + 2], _paletteBytes[o + 3]);
        }

        /// <summary>
        /// 칸 하나의 최종 색을 CPU 로 계산한다 (부드러운 합성 끔 기준) — 셰이더와 같은 규칙, sRGB 바이트. GPU 는 반올림 차이로 ±1 날 수 있다.
        /// </summary>
        public Color32 ComputeTileColor(int tile)
        {
            if (_terrainBytes == null)
                return new Color32(0, 0, 0, 255);

            int x = tile % _width, y = tile / _width;
            int t = tile * 4;
            float tr = _terrainBytes[t], tg = _terrainBytes[t + 1], tb = _terrainBytes[t + 2], ta = _terrainBytes[t + 3] / 255f;
            int index = FrontWarsTerritoryCode.PaletteIndex(_tileCodes[tile], x, y, _tactical);
            float orr = 0, og = 0, ob = 0, oa = 0;

            if (index >= 0)
            {
                int p = index * 4;
                orr = _paletteBytes[p];
                og = _paletteBytes[p + 1];
                ob = _paletteBytes[p + 2];
                oa = _paletteBytes[p + 3] / 255f;
            }

            float cr = tr + (orr - tr) * oa, cg = tg + (og - tg) * oa, cb = tb + (ob - tb) * oa;
            float ca = oa + ta * (1 - oa);
            Color32 bg = _canvasBackground;
            return new Color32(
                (byte)Mathf.RoundToInt(bg.r + (cr - bg.r) * ca),
                (byte)Mathf.RoundToInt(bg.g + (cg - bg.g) * ca),
                (byte)Mathf.RoundToInt(bg.b + (cb - bg.b) * ca),
                255);
        }

        // ── 칸 계산

        private void Schedule(int tile)
        {
            // 난수는 이미 줄에 있어도 쓴다 — 원본이 인자를 먼저 계산한다
            double priority = _game.Ticks + _repaintRandom.NextFloat(0, RepaintJitter);
            _queue.Schedule(tile, priority);
        }

        private void Paint()
        {
            long now = (long)(Time.realtimeSinceStartupAsDouble * 1000.0);

            if (now <= _lastPaintMs + PaintIntervalMs)
                return;

            _lastPaintMs = now;

            int count = _queue.Count / RepaintDivisor;
            if (count == 0 || _game.InSpawnPhase)
                count = _queue.Count;

            while (count > 0 && _queue.Count > 0)
            {
                count--;
                int tile = _queue.Dequeue();
                RenderTile(tile, false);

                int n = _game.Map.NeighborsInto(tile, _neighbors);
                for (int i = 0; i < n; i++)
                    RenderTile(_neighbors[i], true);
            }
        }

        /// <summary>`[소스] renderTile(e,t=!1){if(t&&!this._view.hasOwner(e))return;`</summary>
        private void RenderTile(int tile, bool neighborPass)
        {
            bool owned = _game.HasOwner(tile);

            if (neighborPass && owned == false)
                return;

            if (owned == false)
            {
                Write(tile, (ushort)(_game.HasFallout(tile) ? FrontWarsTerritoryCode.AltBit : 0));
                return;
            }

            FrontWarsPlayer owner = _game.Owner(tile);
            bool border = IsBorder(tile);
            int attack = 0;
            bool defended = false;

            if (border)
            {
                if (_attackOverrides.TryGetValue(tile, out sbyte dir))
                    attack = dir;

                defended = _game.HasUnitNearby(tile, _game.Settings.Config.DefensePostRange, EFrontWarsUnit.DefensePost, owner);
            }

            Write(tile, FrontWarsTerritoryCode.Encode(owner.SmallId, false, border, attack, defended));
        }

        /// <summary>4방향에 주인이 다른 칸이 있나 `[소스] isBorder(e){return this.neighbors(e).some(t=>this.ownerID(t)!==this.ownerID(e))}`.</summary>
        private bool IsBorder(int tile)
        {
            int owner = _game.OwnerSmallId(tile);
            int n = _game.Map.NeighborsInto(tile, _neighbors);

            for (int i = 0; i < n; i++)
            {
                if (_game.OwnerSmallId(_neighbors[i]) != owner)
                    return true;
            }

            return false;
        }

        private void Write(int tile, ushort value)
        {
            if (_tileCodes[tile] == value)
                return;

            _tileCodes[tile] = value;
            _dirtyTiles.Add(tile);
        }

        // ── 방어초소

        private void HandleUnitUpdate(FrontWarsUnit unit)
        {
            if (unit != null && unit.Type == EFrontWarsUnit.DefensePost)
                _pendingDefensePosts.Add(unit);
        }

        /// <summary>
        /// 켜진 방어초소 반경 안 국경 칸 중 주인(또는 옛 주인)이 같은 칸을 다시 칠한다
        /// `[소스] if(e.unitType===D.DefensePost){if(!e.isActive)return;let t=e.pos,n=Xt(t,this._view.config().defensePostRange());`.
        /// 반경 = 유클리드 거리 제곱 ≤ r² `[소스] function Xt(e,t,n=!1){let r=t*t;…t.euclideanDistSquared(e,n)<=r}` (원 안은 4방향으로 이어져 있어 BFS = 원 전체).
        /// </summary>
        private void ScheduleDefensePostBorders()
        {
            if (_pendingDefensePosts.Count == 0)
                return;

            int range = _game.Settings.Config.DefensePostRange;
            FrontWarsTerrainMap map = _game.Map;

            for (int u = 0; u < _pendingDefensePosts.Count; u++)
            {
                FrontWarsUnit unit = _pendingDefensePosts[u];
                if (unit.IsActive == false || unit.Owner == null)
                    continue;

                int ownerId = unit.Owner.SmallId;
                int lastOwnerId = unit.LastOwner != null ? unit.LastOwner.SmallId : -1;
                int cx = map.X(unit.Tile), cy = map.Y(unit.Tile);

                for (int y = Mathf.Max(0, cy - range); y <= Mathf.Min(map.Height - 1, cy + range); y++)
                {
                    for (int x = Mathf.Max(0, cx - range); x <= Mathf.Min(map.Width - 1, cx + range); x++)
                    {
                        int dx = x - cx, dy = y - cy;
                        if (dx * dx + dy * dy > range * range)
                            continue;

                        int t = map.Ref(x, y);
                        if (IsBorder(t) == false)
                            continue;

                        int o = _game.OwnerSmallId(t);
                        if (o == ownerId || o == lastOwnerId)
                            Schedule(t);
                    }
                }
            }

            _pendingDefensePosts.Clear();
        }

        // ── 공격 국경 (내가 거는 · 나에게 오는)

        /// <summary>
        /// `[소스] syncAttackOverrides(){let e=this._view.myPlayer();if(!e?.isAlive()){…}` — 내 공격 중 대상이 있는 것, 받는 공격 중 봇이 아닌 것.
        /// 원본은 앞줄 칸을 워커에 비동기로 묻지만 여기서는 틱마다 바로 읽는다.
        /// </summary>
        private void SyncAttackOverrides()
        {
            FrontWarsPlayer me = _myPlayer;

            if (me == null || me.IsAlive == false)
            {
                if (_attackOverrides.Count > 0)
                    ClearAttackOverrides();

                return;
            }

            List<FrontWarsAttack> outgoing = me.OutgoingAttacks;
            List<FrontWarsAttack> incoming = me.IncomingAttacks;
            int outCount = 0, inCount = 0;

            for (int i = 0; i < outgoing.Count; i++)
            {
                if (outgoing[i].Target != null)
                    outCount++;
            }

            for (int i = 0; i < incoming.Count; i++)
            {
                if (incoming[i].Attacker.Type != EFrontWarsPlayerType.Bot)
                    inCount++;
            }

            if (outCount == 0 && inCount == 0 && _attackOverrides.Count > 0)
            {
                ClearAttackOverrides();
                return;
            }

            _liveAttackIds.Clear();

            for (int i = 0; i < outgoing.Count; i++)
            {
                if (outgoing[i].Target != null)
                    _liveAttackIds.Add(outgoing[i].Id);
            }

            for (int i = 0; i < incoming.Count; i++)
            {
                if (incoming[i].Attacker.Type != EFrontWarsPlayerType.Bot)
                    _liveAttackIds.Add(incoming[i].Id);
            }

            _staleAttackIds.Clear();
            foreach (string id in _attackTilesById.Keys)
            {
                if (_liveAttackIds.Contains(id) == false)
                    _staleAttackIds.Add(id);
            }

            for (int i = 0; i < _staleAttackIds.Count; i++)
            {
                _attackTilesById.Remove(_staleAttackIds[i]);
                RebuildAttackOverrides();
            }

            // 원본 호출 순서: 내 공격 먼저, 받는 공격 다음 `[소스] this.#j(e,t,`outgoing`),this.#j(e,n,`incoming`)`
            for (int i = 0; i < outgoing.Count; i++)
            {
                if (outgoing[i].Target != null)
                    ApplyAttackTiles(outgoing[i], AttackOutgoing);
            }

            for (int i = 0; i < incoming.Count; i++)
            {
                if (incoming[i].Attacker.Type != EFrontWarsPlayerType.Bot)
                    ApplyAttackTiles(incoming[i], AttackIncoming);
            }
        }

        /// <summary>
        /// 앞줄 칸 = 그 방향, 앞줄 칸의 이웃 국경 칸 = 반대 방향 `[소스] applyAttackTiles(e,t,n){let r=n===`outgoing`?`incoming`:`outgoing`;`.
        /// </summary>
        private void ApplyAttackTiles(FrontWarsAttack attack, sbyte direction)
        {
            sbyte other = direction == AttackOutgoing ? AttackIncoming : AttackOutgoing;
            Dictionary<int, sbyte> tiles = null;

            foreach (int t in attack.BorderTiles)
            {
                tiles ??= new Dictionary<int, sbyte>();
                tiles[t] = direction;
            }

            if (tiles == null)
            {
                _attackTilesById.Remove(attack.Id);
                RebuildAttackOverrides();
                return;
            }

            var front = new List<int>(tiles.Keys);
            for (int i = 0; i < front.Count; i++)
            {
                int n = _game.Map.NeighborsInto(front[i], _neighbors);
                for (int k = 0; k < n; k++)
                {
                    int nb = _neighbors[k];
                    if (IsBorder(nb) && tiles.ContainsKey(nb) == false)
                        tiles[nb] = other;
                }
            }

            _attackTilesById[attack.Id] = tiles;
            RebuildAttackOverrides();
        }

        /// <summary>같은 칸이 두 공격에 걸리면 «받는 공격»이 이긴다 `[소스] (!this.#_.has(t)||n===`incoming`)&&this.#_.set(t,n)`.</summary>
        private void RebuildAttackOverrides()
        {
            _previousOverrideTiles.Clear();
            _previousOverrideTiles.AddRange(_attackOverrides.Keys);
            _attackOverrides.Clear();

            foreach (Dictionary<int, sbyte> tiles in _attackTilesById.Values)
            {
                foreach (KeyValuePair<int, sbyte> pair in tiles)
                {
                    if (_attackOverrides.ContainsKey(pair.Key) == false || pair.Value == AttackIncoming)
                        _attackOverrides[pair.Key] = pair.Value;
                }
            }

            for (int i = 0; i < _previousOverrideTiles.Count; i++)
            {
                if (_attackOverrides.ContainsKey(_previousOverrideTiles[i]) == false)
                    Schedule(_previousOverrideTiles[i]);
            }

            foreach (int t in _attackOverrides.Keys)
                Schedule(t);
        }

        private void ClearAttackOverrides()
        {
            if (_game != null)
            {
                foreach (int t in _attackOverrides.Keys)
                    Schedule(t);
            }

            _attackOverrides.Clear();
            _attackTilesById.Clear();
        }

        // ── 팔레트

        /// <summary>
        /// 팔레트를 채운다 — 0행 특수 칸, 플레이어 행은 처음 볼 때 한 번, 관계 색은 틱마다 `[source] build(e,t,n){this.#e=!1;`.
        /// </summary>
        private void BuildPalette()
        {
            IReadOnlyList<Color32> fallout = FrontWarsPlayerColors.FalloutColors;
            IReadOnlyList<int> falloutSlots = FrontWarsPlayerColors.FalloutSlots;

            for (int k = 0; k < falloutSlots.Count; k++)
                SetPalette(falloutSlots[k], 0, fallout[k % fallout.Count], FrontWarsTerritoryCode.FalloutAlpha);

            SetPalette(FrontWarsTerritoryCode.SlotOutgoingAttack, 0, FrontWarsPlayerColors.OutgoingAttackBorder, 255);
            SetPalette(FrontWarsTerritoryCode.SlotIncomingAttack, 0, FrontWarsPlayerColors.IncomingAttackBorder, 255);

            FrontWarsPlayer me = _myPlayer;
            IReadOnlyList<FrontWarsPlayer> players = _game.AllPlayers;

            for (int i = 0; i < players.Count; i++)
            {
                FrontWarsPlayer p = players[i];
                int row = p.SmallId;

                if (row <= 0 || row >= FrontWarsTerritoryCode.PaletteHeight)
                    continue;

                if (_paletteRowWritten[row] == false)
                {
                    _paletteRowWritten[row] = true;

                    // 팀전이면 팀 색 — 영토 칸은 소수 버림, 국경은 소수 색에서 반올림이라 따로 받는다
                    Color32 territory = _colors.TerritoryColor(p);
                    Color32 border = _colors.BorderColor(p, p == me);

                    SetPalette(FrontWarsTerritoryCode.SlotTerritory, row, territory, FrontWarsTerritoryCode.TerritoryAlpha);
                    SetPalette(FrontWarsTerritoryCode.SlotAlt, row, border, FrontWarsTerritoryCode.TerritoryAlpha);
                    SetPalette(FrontWarsTerritoryCode.SlotBorder, row, border, 255);
                    SetPalette(FrontWarsTerritoryCode.SlotDefendedLight, row, FrontWarsPlayerColors.DefendedBorderLight(border), 255);
                    SetPalette(FrontWarsTerritoryCode.SlotDefendedDark, row, FrontWarsPlayerColors.DefendedBorderDark(border), 255);
                }

                if (me != null)
                {
                    // `[소스] let e=t.smallID()===a.smallID()?r.selfColor():t.isFriendly(a)?r.allyColor():t.hasEmbargo(a)?r.enemyColor():r.neutralColor()`
                    Color32 relation;

                    if (p.SmallId == me.SmallId)
                        relation = FrontWarsPlayerColors.SelfColor;
                    else if (p.IsAlliedWith(me) || p.IsOnSameTeam(me))
                        relation = FrontWarsPlayerColors.AllyColor;
                    else if (p.HasEmbargoAgainst(me) || me.HasEmbargoAgainst(p))
                        relation = FrontWarsPlayerColors.EnemyColor;
                    else
                        relation = FrontWarsPlayerColors.NeutralColor;

                    SetPalette(FrontWarsTerritoryCode.SlotRelation, row, relation, 255);
                }
                else
                {
                    SetPalette(FrontWarsTerritoryCode.SlotRelation, row, new Color32(0, 0, 0, 0), 0);
                }
            }
        }

        private void SetPalette(int slot, int row, Color32 color, byte alpha)
        {
            int o = (row * FrontWarsTerritoryCode.PaletteWidth + slot) * 4;

            if (_paletteBytes[o] == color.r && _paletteBytes[o + 1] == color.g && _paletteBytes[o + 2] == color.b && _paletteBytes[o + 3] == alpha)
                return;

            _paletteBytes[o] = color.r;
            _paletteBytes[o + 1] = color.g;
            _paletteBytes[o + 2] = color.b;
            _paletteBytes[o + 3] = alpha;
            _paletteDirty = true;
        }

        // ── GPU

        private void CreateGpuResources()
        {
            DestroyGpuResources();

            _terrainTex = CreateTexture("FrontWarsTerrain", _width, _height, TextureFormat.RGBA32);

            TextureFormat ownerFormat = SystemInfo.SupportsTextureFormat(TextureFormat.RG16) ? TextureFormat.RG16 : TextureFormat.RGBA32;
            _ownerBytesPerPixel = ownerFormat == TextureFormat.RG16 ? 2 : 4;
            _ownerTex = CreateTexture("FrontWarsTileCodes", _width, _height, ownerFormat);

            _paletteTex = CreateTexture("FrontWarsPalette", FrontWarsTerritoryCode.PaletteWidth, FrontWarsTerritoryCode.PaletteHeight, TextureFormat.RGBA32);

            // 칸 값 텍스처는 0 으로 시작 (GetPixelData 는 초기화되지 않은 메모리일 수 있다)
            NativeArray<byte> ownerData = _ownerTex.GetPixelData<byte>(0);
            for (int i = 0; i < ownerData.Length; i++)
                ownerData[i] = 0;

            _ownerTex.Apply(false);

            _material = new Material(_shader) { name = "FrontWarsTerritory (runtime)" };
            _material.SetTexture(TerrainTexId, _terrainTex);
            _material.SetTexture(OwnerTexId, _ownerTex);
            _material.SetTexture(PaletteTexId, _paletteTex);
            _material.SetVector(MapSizeId, new Vector4(_width, _height, 0, 0));
            _material.SetVector(CanvasBgId, ToVector(_canvasBackground));
            _material.SetFloat(TacticalId, _tactical ? 1f : 0f);
            _material.SetFloat(SmoothId, _smooth ? 1f : 0f);

            _mesh = BuildQuad(_width, _height);
            GetComponent<MeshFilter>().sharedMesh = _mesh;

            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            meshRenderer.sortingOrder = _sortingOrder;
        }

        private void DestroyGpuResources()
        {
            DestroyObject(ref _terrainTex);
            DestroyObject(ref _ownerTex);
            DestroyObject(ref _paletteTex);
            DestroyObject(ref _material);
            DestroyObject(ref _mesh);
        }

        private static void DestroyObject<T>(ref T obj) where T : Object
        {
            if (obj == null)
                return;

            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);

            obj = null;
        }

        private void RebuildTerrain()
        {
            if (_game == null || _terrainTex == null)
                return;

            _terrainBytes ??= new byte[_width * _height * 4];
            FrontWarsTerrainColors.Paint(_game.Map, _darkTheme, _waterColor, _terrainBytes);
            _terrainTex.SetPixelData(_terrainBytes, 0);
            _terrainTex.Apply(false);
        }

        /// <summary>바뀐 칸만 CPU 텍스처 메모리에 쓰고 한 프레임에 한 번 올린다.</summary>
        private void Flush()
        {
            if (_dirtyTiles.Count > 0 && _ownerTex != null)
            {
                NativeArray<byte> data = _ownerTex.GetPixelData<byte>(0);
                int bpp = _ownerBytesPerPixel;

                for (int i = 0; i < _dirtyTiles.Count; i++)
                {
                    int tile = _dirtyTiles[i];
                    ushort v = _tileCodes[tile];
                    int o = tile * bpp;
                    data[o] = (byte)(v & 0xFF);
                    data[o + 1] = (byte)(v >> 8);
                }

                _dirtyTiles.Clear();
                _ownerTex.Apply(false);
            }

            if (_paletteDirty && _paletteTex != null)
            {
                _paletteDirty = false;
                _paletteTex.SetPixelData(_paletteBytes, 0);
                _paletteTex.Apply(false);
            }
        }

        /// <summary>
        /// 줌(화면 px / 칸) 1 미만이면 부드러운 합성 `[소스] r.uniform1i(i.uSmooth,+(e.zoomLevel<1))`.
        /// 원본 줌은 CSS px 기준 — 여기서는 카메라 실제 px 기준이다 (윈도우 배율 100% 에서 같다).
        /// </summary>
        private void UpdateSmooth()
        {
            bool smooth = false;

            if (_camera != null && _camera.orthographic && _camera.orthographicSize > 0f)
            {
                float pixelsPerTile = _camera.pixelHeight / (2f * _camera.orthographicSize);
                smooth = pixelsPerTile < 1f;
            }

            if (smooth == _smooth)
                return;

            _smooth = smooth;

            if (_material != null)
                _material.SetFloat(SmoothId, smooth ? 1f : 0f);
        }

        private static Texture2D CreateTexture(string name, int width, int height, TextureFormat format)
        {
            // linear = true — sRGB 디코드 없이 바이트/255 를 그대로 읽는다 (셰이더가 바이트 공간에서 섞는다)
            return new Texture2D(width, height, format, false, true)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };
        }

        /// <summary>로컬 x 0..W, y 0..−H 사각형. 셰이더가 로컬 위치로 칸 좌표를 낸다.</summary>
        private static Mesh BuildQuad(int width, int height)
        {
            var mesh = new Mesh { name = "FrontWarsMapQuad" };
            mesh.vertices = new[]
            {
                new Vector3(0, 0, 0),
                new Vector3(width, 0, 0),
                new Vector3(width, -height, 0),
                new Vector3(0, -height, 0)
            };
            mesh.uv = new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector4 ToVector(Color32 c)
        {
            return new Vector4(c.r / 255f, c.g / 255f, c.b / 255f, 1f);
        }
    }
}
