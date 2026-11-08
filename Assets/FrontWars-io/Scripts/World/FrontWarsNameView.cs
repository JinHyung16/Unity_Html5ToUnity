using System;
using System.Collections.Generic;
using JinHyung.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 지도 위 나라 이름 · 병력 · 아이콘 줄 — 이름 자리는 원본처럼 조금씩 나눠 다시 계산한다 `[소스] Cq3gr8XO.js:ug … placeNameIfChanged · a&lt;3 이면 전부, 그 뒤엔 틱마다 30명 중 하나씩`.
    ///
    /// <para>
    /// 이름표는 <b>화면 px</b> 로 그린다 — 기준 크기 s = max(1, floor(자리크기)), 화면 크기 a = zoom × s,
    /// 글자 크기 = max(4, floor(s × zoom)) px `[소스] BMmXchgl.js:l.fontSize=Math.max(4,Math.floor(r*i))`.
    /// 월드에 놓을 때는 zoom 으로 나눠 칸 단위로 바꾼다.
    /// </para>
    ///
    /// <para>이모지가 와 있으면 아이콘 줄 대신 이모지 하나만 그린다 `[소스] BMmXchgl.js:if(r){e.save(),e.font=\`${o}px sans-serif\``.</para>
    /// </summary>
    public sealed class FrontWarsNameView : MonoBehaviour
    {
        /// <summary>틱마다 이름 자리를 다시 계산할 간격 (플레이어 수 기준) `[소스] for(let t=a%30;t&lt;e.length;t+=30)`.</summary>
        private const int PlacementStride = 30;

        /// <summary>처음 틱들은 전원 계산 `[소스] if(a&lt;3)`.</summary>
        private const int InitialFullTicks = 3;

        /// <summary>TMP 3D 글자 크기 → 월드 높이 비 (fontSize 10 ≈ 1 단위).</summary>
        private const float TmpSizePerWorldUnit = 10f;

        /// <summary>숨김 — 화면 크기가 이보다 작으면 `[소스] BMmXchgl.js:if(a&lt;6||i&gt;16&&a&gt;101)return`.</summary>
        private const float MinScreenSize = 6f;

        /// <summary>숨김 — 아주 크게 당겼는데 이름 자리가 너무 크면 (화면을 뒤덮는다).</summary>
        private const float HideZoom = 16f;
        private const float HideScreenSize = 101f;

        /// <summary>가장 작은 글자 크기 (화면 px).</summary>
        private const float MinFontPx = 4f;

        /// <summary>병력 줄 — 이름 아래 글자크기 × 1.05 `[소스] BMmXchgl.js:y+글자크기*1.05`.</summary>
        private const float TroopsLineGap = 1.05f;

        /// <summary>아이콘 줄 가운데 — 이름 위 글자크기 × 1.15 `[소스] BMmXchgl.js:i-a*1.15`.</summary>
        private const float IconRowGap = 1.15f;

        /// <summary>아이콘 한 변 = min(글자크기 × 1.5, 48) px `[소스] BMmXchgl.js:f=Math.min(a*1.5,48)`.</summary>
        private const float IconScale = 1.5f;
        private const float IconMaxPx = 48f;

        /// <summary>아이콘 사이 간격 (화면 px) · 투명도 `[소스] BMmXchgl.js:e.globalAlpha=.77,…,c+=o+4`.</summary>
        private const float IconGapPx = 4f;
        private const float IconAlpha = 0.77f;

        /// <summary>마우스가 이름표 위에 있을 때 투명도 `[소스] render_specs.md 5번 「마우스가 이름표 위에 있으면 이름표 투명도 0.5」`.</summary>
        private const float HoverAlpha = 0.5f;

        /// <summary>이모지가 보이는 틱 수 `[소스] Cq3gr8XO.js:emojiMessageDuration(){return 50}` (1틱 = 100ms).</summary>
        private const int EmojiTicks = 50;

        /// <summary>내 이름 외곽선 두께 = max(2, 글자크기 × 0.25) px `[소스] BMmXchgl.js:Math.max(2,글자크기*.25)`.</summary>
        private const float MyOutlineRatio = 0.25f;
        private const float MyOutlineMinPx = 2f;

        /// <summary>땅 1위를 다시 세는 간격 (틱) `[소스] BMmXchgl.js:왕관은 10틱마다 다시 셈`.</summary>
        private const int TopPlayerInterval = 10;

        /// <summary>내 이름 `[소스] BMmXchgl.js:#FFFFFF` + 외곽선 `#003580`.</summary>
        private static readonly Color32 MyNameColor = new Color32(255, 255, 255, 255);
        private static readonly Color32 MyOutlineColor = new Color32(0, 53, 128, 255);

        /// <summary>사람 이름 `[소스] BMmXchgl.js:textColor(e){return e.type()===ke.Human?\`#000000\`:\`#4D4D4D\`}`.</summary>
        private static readonly Color32 HumanNameColor = new Color32(0, 0, 0, 255);
        private static readonly Color32 OtherNameColor = new Color32(77, 77, 77, 255);

        /// <summary>내 땅으로 날아오는 핵은 빨강, 아니면 흰색 `[소스] BMmXchgl.js:목표가 내 땅이면 빨강`.</summary>
        private static readonly Color32 NukeAlertColor = new Color32(255, 60, 60, 255);

        /// <summary>아이콘 줄 차례 `[소스] BMmXchgl.js:e===this._topPlayer&&t.push(\`crown\`)` 이후 순서.</summary>
        private static readonly FrontWarsIconFactory.ENameIcon[] IconOrder =
        {
            FrontWarsIconFactory.ENameIcon.Crown,
            FrontWarsIconFactory.ENameIcon.Traitor,
            FrontWarsIconFactory.ENameIcon.Disconnected,
            FrontWarsIconFactory.ENameIcon.Alliance,
            FrontWarsIconFactory.ENameIcon.AllianceRequest,
            FrontWarsIconFactory.ENameIcon.Target,
            FrontWarsIconFactory.ENameIcon.Embargo,
            FrontWarsIconFactory.ENameIcon.Nuke
        };

        private sealed class Label
        {
            public TextMeshPro Name;
            public TextMeshPro Troops;
            public readonly List<SpriteRenderer> Icons = new List<SpriteRenderer>();

            /// <summary>이번 틱에 붙는 아이콘과 그 색 — 매 프레임이 아니라 틱마다 다시 센다.</summary>
            public readonly List<FrontWarsIconFactory.ENameIcon> IconKinds = new List<FrontWarsIconFactory.ENameIcon>(8);

            public readonly List<Color32> IconColors = new List<Color32>(8);

            public SpriteRenderer Emoji;
            public string EmojiText;
            public int EmojiUntilTick = -1;
            public FrontWarsNameLocation Location;
            public int LastTileChange = -1;
            public bool Placed;
        }

        [SerializeField] private TMP_FontAsset _font;

        private FrontWarsGame _game;
        private FrontWarsCameraController _camera;
        private FrontWarsMapRenderer _mapRenderer;
        private readonly Dictionary<string, Label> _labels = new Dictionary<string, Label>();

        /// <summary>이번 틱에 핵을 날리고 있는 플레이어 — 값이 참이면 목표가 내 땅이다.</summary>
        private readonly Dictionary<string, bool> _nukeInFlight = new Dictionary<string, bool>();

        /// <summary>플레이어 id → 가짜 이름. 한 번 정하면 판이 끝날 때까지 그대로다.</summary>
        private readonly Dictionary<string, string> _fakeNames = new Dictionary<string, string>();

        private IReadOnlyList<string> _fakePrefixes;
        private IReadOnlyList<string> _fakeSuffixes;
        private bool _nameTableSearched;
        private bool _anonymousNames;
        private bool _emojisEnabled = true;
        private string _topPlayerId;
        private int _tick;

        /// <summary>
        /// 설정 「실제 이름을 무작위 이름으로 표시」 — 켜면 지도 이름표가 가짜 이름으로 보인다 (기본 끔).
        /// <para>⚠ 보이는 글자만 바꾼다. 시뮬의 이름 · 해시 · 난수는 손대지 않는다 (<see cref="FrontWarsPlayer.Name"/> 는 그대로다).</para>
        /// </summary>
        public bool AnonymousNames
        {
            get { return _anonymousNames; }
            set { _anonymousNames = value; }
        }

        /// <summary>설정 「게임 내 이모지 표시 여부」 — 끄면 이모지를 안 띄우고 떠 있던 것도 치운다 (기본 켬).</summary>
        public bool EmojisEnabled
        {
            get { return _emojisEnabled; }
            set
            {
                _emojisEnabled = value;

                if (value)
                    return;

                foreach (Label label in _labels.Values)
                {
                    label.EmojiText = null;
                    label.EmojiUntilTick = -1;
                }
            }
        }

        /// <summary>
        /// 나 — 이름 색 · 동맹 · 목표 · 금수 · 핵 아이콘이 나를 기준으로 갈린다.
        /// <para>안 넣으면 내 이름표도 남처럼 그린다 (<see cref="FrontWarsWorldView"/> 가 넣어 주면 된다).</para>
        /// </summary>
        public Func<FrontWarsPlayer> LocalPlayer { get; set; }

        /// <summary>전술 보기면 이름표를 안 그린다 — 지도 그리기에서 값을 읽는다.</summary>
        private bool TacticalView
        {
            get
            {
                if (_mapRenderer == null)
                {
                    FrontWarsWorldView world = GetComponentInParent<FrontWarsWorldView>();
                    _mapRenderer = world != null ? world.MapRenderer : null;
                }

                return _mapRenderer != null && _mapRenderer.IsTacticalView;
            }
        }

        public void Bind(FrontWarsGame game, FrontWarsCameraController camera)
        {
            Unbind();
            _game = game;
            _camera = camera;
        }

        public void Unbind()
        {
            foreach (Label label in _labels.Values)
                DestroyLabel(label);

            _labels.Clear();
            _fakeNames.Clear();
            _nukeInFlight.Clear();
            _topPlayerId = null;
            _game = null;
        }

        private void DestroyLabel(Label label)
        {
            if (label.Name != null)
                Destroy(label.Name.gameObject);

            if (label.Troops != null)
                Destroy(label.Troops.gameObject);

            foreach (SpriteRenderer icon in label.Icons)
            {
                if (icon != null)
                    Destroy(icon.gameObject);
            }

            if (label.Emoji != null)
                Destroy(label.Emoji.gameObject);
        }

        // ── 이모지

        /// <summary>보낸 사람 이름표 위에 이모지를 띄운다 — 50틱 동안 아이콘 줄 대신 보인다.</summary>
        public void ShowEmoji(FrontWarsPlayer sender, string emoji)
        {
            if (_emojisEnabled == false || sender == null || string.IsNullOrEmpty(emoji) || _game == null)
                return;

            if (_labels.TryGetValue(sender.Id, out Label label) == false)
                return;

            label.EmojiText = emoji;
            label.EmojiUntilTick = _tick + EmojiTicks;
        }

        /// <summary>그 플레이어 이름표가 놓인 월드 자리 — 정복 연출이 쓴다.</summary>
        public bool TryNamePosition(FrontWarsPlayer player, out Vector3 position)
        {
            position = Vector3.zero;

            if (player == null || _labels.TryGetValue(player.Id, out Label label) == false || label.Placed == false)
                return false;

            position = FrontWarsWorld.FromTileCoords(label.Location.X, label.Location.Y);
            return true;
        }

        // ── 틱

        /// <summary>시뮬 한 틱 뒤에 부른다.</summary>
        public void OnTick(int tick)
        {
            if (_game == null)
                return;

            _tick = tick;
            List<FrontWarsPlayer> alive = _game.Players();

            if (tick < InitialFullTicks || _game.InSpawnPhase)
            {
                foreach (FrontWarsPlayer p in alive)
                    Place(p);
            }
            else
            {
                for (int i = tick % PlacementStride; i < alive.Count; i += PlacementStride)
                    Place(alive[i]);
            }

            // 왕관 — 땅 1위를 10틱마다 다시 센다
            if (tick % TopPlayerInterval == 0)
                _topPlayerId = TopPlayer(alive);

            RefreshNukesInFlight();
            FrontWarsPlayer me = LocalPlayer?.Invoke();

            foreach (KeyValuePair<string, Label> pair in _labels)
            {
                FrontWarsPlayer player = _game.Player(pair.Key);

                if (player.IsAlive == false)
                {
                    Hide(pair.Value);
                    continue;
                }

                CollectIcons(pair.Value, player, me);
            }
        }

        /// <summary>날아가는 중인 핵을 틱마다 한 번만 훑는다 — 이름표마다 유닛을 다시 세지 않는다.</summary>
        private void RefreshNukesInFlight()
        {
            _nukeInFlight.Clear();
            FrontWarsPlayer me = LocalPlayer?.Invoke();

            List<FrontWarsUnit> nukes = _game.AllUnits(EFrontWarsUnit.AtomBomb, EFrontWarsUnit.HydrogenBomb,
                EFrontWarsUnit.MIRV, EFrontWarsUnit.MIRVWarhead);

            foreach (FrontWarsUnit nuke in nukes)
            {
                if (nuke.IsActive == false || nuke.Owner == null)
                    continue;

                bool atMe = me != null && nuke.TargetTile >= 0 && _game.OwnerSmallId(nuke.TargetTile) == me.SmallId;
                bool had = _nukeInFlight.TryGetValue(nuke.Owner.Id, out bool was) && was;
                _nukeInFlight[nuke.Owner.Id] = had || atMe;
            }
        }

        private static string TopPlayer(List<FrontWarsPlayer> alive)
        {
            string best = null;
            int bestTiles = 0;

            foreach (FrontWarsPlayer p in alive)
            {
                if (p.NumTilesOwned <= bestTiles)
                    continue;

                bestTiles = p.NumTilesOwned;
                best = p.Id;
            }

            return best;
        }

        private void Place(FrontWarsPlayer player)
        {
            if (_labels.TryGetValue(player.Id, out Label label) == false)
            {
                label = new Label { Name = CreateText(player, "Name"), Troops = CreateText(player, "Troops") };
                _labels[player.Id] = label;
            }

            if (label.Placed && label.LastTileChange == player.LastTileChange)
                return;

            label.LastTileChange = player.LastTileChange;
            label.Location = FrontWarsNamePlacement.Compute(_game, player);
            label.Placed = true;
        }

        private TextMeshPro CreateText(FrontWarsPlayer player, string kind)
        {
            var go = new GameObject($"{kind}_{player.Name}");
            go.transform.SetParent(transform, false);

            var text = go.AddComponent<TextMeshPro>();
            text.font = _font != null ? _font : FrontWarsFonts.Korean;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = HumanNameColor;
            text.sortingOrder = 50;
            return text;
        }

        // ── 가짜 이름

        /// <summary>이름표에 쓸 글자 — 익명이 꺼져 있으면 진짜 이름 그대로.</summary>
        public string DisplayName(FrontWarsPlayer player)
        {
            if (_anonymousNames == false || player == null)
                return player == null ? string.Empty : player.Name;

            if (_fakeNames.TryGetValue(player.Id, out string fake))
                return fake;

            fake = MakeFakeName(player.Id);
            _fakeNames[player.Id] = fake;
            return fake;
        }

        /// <summary>
        /// 플레이어 id 를 섞어 가짜 이름을 하나 고른다 — 같은 id 면 늘 같은 이름이다.
        /// <para>봇 이름 표(<c>FrontWarsBotNameTable</c>)의 낱말을 쓴다. 표가 아직 안 올라왔으면 번호만 붙인다.</para>
        /// <para>⚠ 시뮬 난수를 쓰지 않는다 — 여기서 난수를 뽑으면 판이 달라진다.</para>
        /// </summary>
        private string MakeFakeName(string id)
        {
            uint h = Hash(id);
            LoadNameTable();

            if (_fakePrefixes != null && _fakePrefixes.Count > 0 && _fakeSuffixes != null && _fakeSuffixes.Count > 0)
            {
                string prefix = _fakePrefixes[(int)(h % (uint)_fakePrefixes.Count)];
                string suffix = _fakeSuffixes[(int)(h / (uint)_fakePrefixes.Count % (uint)_fakeSuffixes.Count)];
                return $"{prefix} {suffix}";
            }

            return $"Player {h % 10000u}";
        }

        /// <summary>봇 이름 표를 한 번만 찾는다 — 표가 아직 안 올라왔으면 오류 없이 넘어간다.</summary>
        private void LoadNameTable()
        {
            if (_nameTableSearched)
                return;

            _nameTableSearched = true;

            if (DataManager.Instance.IsInitialized == false)
            {
                _nameTableSearched = false;
                return;
            }

            FrontWarsBotNameDataContainer table = GameRoot.Instance.FrontWarsBotNameDataContainer;

            if (table == null)
                return;

            _fakePrefixes = table.Prefixes;
            _fakeSuffixes = table.Suffixes;
        }

        /// <summary>FNV-1a — 글자 하나만 달라도 다른 값이 나오면 된다 (시뮬 해시와 무관하다).</summary>
        private static uint Hash(string id)
        {
            uint h = 2166136261u;

            for (int i = 0; i < id.Length; i++)
            {
                h ^= id[i];
                h = unchecked(h * 16777619u);
            }

            return h;
        }

        // ── 그리기

        private void LateUpdate()
        {
            if (_game == null || _camera == null)
                return;

            bool tactical = TacticalView;
            FrontWarsPlayer me = LocalPlayer?.Invoke();
            float zoom = _camera.Zoom;
            bool hasPointer = TryPointerPosition(out Vector3 pointer);

            foreach (KeyValuePair<string, Label> pair in _labels)
            {
                Label label = pair.Value;
                FrontWarsPlayer player = _game.Player(pair.Key);

                if (label.Placed == false || player.IsAlive == false || tactical)
                {
                    Hide(label);
                    continue;
                }

                // 기준 크기 s 와 화면 크기 a — 숨김 조건이 이 둘로 갈린다
                float s = Mathf.Max(1f, Mathf.Floor((float)label.Location.Size));
                float screen = zoom * s;

                if (screen < MinScreenSize || (zoom > HideZoom && screen > HideScreenSize))
                {
                    Hide(label);
                    continue;
                }

                float fontPx = Mathf.Max(MinFontPx, Mathf.Floor(s * zoom));
                float fontWorld = fontPx / zoom;
                Vector3 anchor = FrontWarsWorld.FromTileCoords(label.Location.X, label.Location.Y);

                // 마우스가 글자 상자 안에 있으면 이름표 전체가 반투명해진다
                float alpha = hasPointer && IsUnderPointer(label, pointer) ? HoverAlpha : 1f;

                DrawName(label, player, me, anchor, fontPx, fontWorld, alpha);
                DrawIconRow(label, anchor, fontPx, fontWorld, zoom, alpha);
            }
        }

        /// <summary>
        /// 마우스가 가리키는 칸의 월드 자리 — 마우스가 없거나 HUD 위에 있으면 false.
        /// <para>칸 단위라 반 칸 안팎의 오차가 있다 (카메라가 주는 것이 칸이다).</para>
        /// </summary>
        private bool TryPointerPosition(out Vector3 position)
        {
            position = Vector3.zero;
            Mouse mouse = Mouse.current;

            if (mouse == null)
                return false;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return false;

            Vector2Int tile = _camera.UnityScreenToTile(mouse.position.ReadValue());
            position = FrontWarsWorld.TileCenter(tile.x, tile.y);
            return true;
        }

        /// <summary>그 자리가 이름 · 병력 글자 상자 안인가 — 글자가 실제로 차지한 범위로 잰다.</summary>
        private static bool IsUnderPointer(Label label, Vector3 point)
        {
            return Covers(label.Name, point) || Covers(label.Troops, point);
        }

        private static bool Covers(TextMeshPro text, Vector3 point)
        {
            if (text == null || text.gameObject.activeSelf == false)
                return false;

            // 글자 메시 범위 — 글자를 넣은 다음 프레임에 잡히니 처음 한 프레임은 비어 있다
            Bounds box = text.bounds;

            if (box.size.x <= 0f || box.size.y <= 0f)
                return false;

            Vector3 local = point - text.transform.localPosition;
            return Mathf.Abs(local.x - box.center.x) <= box.extents.x
                   && Mathf.Abs(local.y - box.center.y) <= box.extents.y;
        }

        private void DrawName(Label label, FrontWarsPlayer player, FrontWarsPlayer me, Vector3 anchor, float fontPx, float fontWorld, float alpha)
        {
            Color32 color = player == me ? MyNameColor
                : player.Type == EFrontWarsPlayerType.Human ? HumanNameColor
                : OtherNameColor;

            color.a = (byte)Mathf.RoundToInt(255f * alpha);

            label.Name.gameObject.SetActive(true);
            label.Name.fontSize = fontWorld * TmpSizePerWorldUnit;
            label.Name.color = color;
            label.Name.text = DisplayName(player);
            label.Name.transform.localPosition = anchor + new Vector3(0f, 0f, -0.2f);

            label.Troops.gameObject.SetActive(true);
            label.Troops.fontSize = label.Name.fontSize;
            label.Troops.color = color;
            label.Troops.text = FrontWarsNumberFormat.Troops(player.Troops);
            label.Troops.transform.localPosition = anchor + new Vector3(0f, -fontWorld * TroopsLineGap, -0.2f);

            // 내 이름만 파란 외곽선 — 두께는 글자 크기에 비례하되 최소 2px
            float outline = player == me ? Mathf.Max(MyOutlineMinPx / fontPx, MyOutlineRatio) : 0f;
            SetOutline(label.Name, outline, alpha);
            SetOutline(label.Troops, outline, alpha);
        }

        private static void SetOutline(TextMeshPro text, float width, float alpha)
        {
            Color32 color = MyOutlineColor;
            color.a = (byte)Mathf.RoundToInt(255f * alpha);
            text.outlineWidth = Mathf.Clamp01(width);
            text.outlineColor = color;
        }

        /// <summary>아이콘 줄 — 이모지가 와 있으면 이모지 하나로 갈아치운다.</summary>
        private void DrawIconRow(Label label, Vector3 anchor, float fontPx, float fontWorld, float zoom, float alpha)
        {
            float sidePx = Mathf.Min(fontPx * IconScale, IconMaxPx);
            float side = sidePx / zoom;
            float rowY = anchor.y + fontWorld * IconRowGap;

            bool hasEmoji = _emojisEnabled && string.IsNullOrEmpty(label.EmojiText) == false && _tick <= label.EmojiUntilTick;

            if (hasEmoji)
            {
                HideIcons(label);
                SpriteRenderer emoji = label.Emoji ??= CreateIcon("Emoji");
                emoji.gameObject.SetActive(true);
                emoji.sprite = FrontWarsIconFactory.GetEmoji(label.EmojiText);
                emoji.color = new Color(1f, 1f, 1f, alpha);
                emoji.transform.localPosition = new Vector3(anchor.x, rowY, -0.2f);
                emoji.transform.localScale = new Vector3(side, side, 1f);
                return;
            }

            if (label.Emoji != null)
                label.Emoji.gameObject.SetActive(false);

            while (label.Icons.Count < label.IconKinds.Count)
                label.Icons.Add(CreateIcon("Icon"));

            float gap = IconGapPx / zoom;
            float total = label.IconKinds.Count * side + Mathf.Max(0, label.IconKinds.Count - 1) * gap;
            float x = anchor.x - total * 0.5f + side * 0.5f;

            for (int i = 0; i < label.Icons.Count; i++)
            {
                SpriteRenderer icon = label.Icons[i];

                if (i >= label.IconKinds.Count)
                {
                    icon.gameObject.SetActive(false);
                    continue;
                }

                Color32 tint = label.IconColors[i];
                icon.gameObject.SetActive(true);
                icon.sprite = FrontWarsIconFactory.GetNameIcon(label.IconKinds[i]);
                icon.color = new Color(tint.r / 255f, tint.g / 255f, tint.b / 255f, IconAlpha * alpha);
                icon.transform.localPosition = new Vector3(x, rowY, -0.2f);
                icon.transform.localScale = new Vector3(side, side, 1f);
                x += side + gap;
            }
        }

        /// <summary>이 플레이어에게 붙는 아이콘을 순서대로 모은다 `[소스] render_specs.md 5번 표`.</summary>
        private void CollectIcons(Label label, FrontWarsPlayer player, FrontWarsPlayer me)
        {
            label.IconKinds.Clear();
            label.IconColors.Clear();

            foreach (FrontWarsIconFactory.ENameIcon icon in IconOrder)
            {
                if (HasIcon(icon, player, me, out Color32 color) == false)
                    continue;

                label.IconKinds.Add(icon);
                label.IconColors.Add(color);
            }
        }

        private bool HasIcon(FrontWarsIconFactory.ENameIcon icon, FrontWarsPlayer player, FrontWarsPlayer me, out Color32 color)
        {
            color = new Color32(255, 255, 255, 255);

            switch (icon)
            {
                case FrontWarsIconFactory.ENameIcon.Crown:
                    return player.Id == _topPlayerId;
                case FrontWarsIconFactory.ENameIcon.Traitor:
                    return player.IsTraitor;
                case FrontWarsIconFactory.ENameIcon.Disconnected:
                    return player.IsDisconnected;
                case FrontWarsIconFactory.ENameIcon.Alliance:
                    return me != null && player != me && player.IsAlliedWith(me);
                case FrontWarsIconFactory.ENameIcon.AllianceRequest:
                    return me != null && player != me && HasRequestTo(player, me);
                case FrontWarsIconFactory.ENameIcon.Target:
                    return me != null && IsMyTarget(player, me);
                case FrontWarsIconFactory.ENameIcon.Embargo:
                    return me != null && player != me && me.HasEmbargoAgainst(player);
                default:
                    return HasNukeInFlight(player, out color);
            }
        }

        /// <summary>그 플레이어가 나에게 동맹을 걸어 놓았나.</summary>
        private static bool HasRequestTo(FrontWarsPlayer player, FrontWarsPlayer me)
        {
            foreach (FrontWarsAllianceRequest request in player.OutgoingAllianceRequests())
            {
                if (request.Recipient == me && request.Status == EFrontWarsRequestStatus.Pending)
                    return true;
            }

            return false;
        }

        /// <summary>내 목표 목록 — 내가 건 목표와 동맹이 건 목표(transitiveTargets)를 함께 본다.</summary>
        private static bool IsMyTarget(FrontWarsPlayer player, FrontWarsPlayer me)
        {
            if (player == me)
                return false;

            if (me.Targets().Contains(player))
                return true;

            foreach (FrontWarsPlayer ally in me.Allies())
            {
                if (ally.Targets().Contains(player))
                    return true;
            }

            return false;
        }

        /// <summary>그 플레이어의 핵이 날아가는 중인가 — 목표가 내 땅이면 빨강, 아니면 흰색.</summary>
        private bool HasNukeInFlight(FrontWarsPlayer player, out Color32 color)
        {
            if (_nukeInFlight.TryGetValue(player.Id, out bool atMe) == false)
            {
                color = new Color32(255, 255, 255, 255);
                return false;
            }

            color = atMe ? NukeAlertColor : new Color32(255, 255, 255, 255);
            return true;
        }

        private SpriteRenderer CreateIcon(string kind)
        {
            var go = new GameObject(kind);
            go.transform.SetParent(transform, false);
            var icon = go.AddComponent<SpriteRenderer>();
            icon.sortingOrder = 51;
            return icon;
        }

        private static void Hide(Label label)
        {
            if (label.Name != null)
                label.Name.gameObject.SetActive(false);

            if (label.Troops != null)
                label.Troops.gameObject.SetActive(false);

            HideIcons(label);

            if (label.Emoji != null)
                label.Emoji.gameObject.SetActive(false);
        }

        private static void HideIcons(Label label)
        {
            foreach (SpriteRenderer icon in label.Icons)
            {
                if (icon != null)
                    icon.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
