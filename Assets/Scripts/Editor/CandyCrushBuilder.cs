using System.IO;
using JinHyung.CandyCrush;
using JinHyung.Core;
using JinHyung.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 프리팹과 씬을 <b>코드로 굽는다</b> (확정표 10-b: 빌더 스크립트).
    ///
    /// <para>
    /// ⚠ <b>손으로 고친 것은 다음 실행에 날아간다.</b> 고칠 것이 있으면 이 파일을 고친다.
    /// </para>
    ///
    /// <para>
    /// <b>보드와 셀은 원본 치수 ×1.6 그대로다</b> — 그게 게임 자체다.
    /// <b>글자·버튼·여백은 모바일 기준으로 키웠다</b> — 원본은 데스크톱 브라우저 치수라
    /// 그대로 옮기면 폰에서 읽을 수 없다. 이건 <b>원장 확정표 12번에 「의도된 차이」로 등재</b>돼 있다.
    /// </para>
    /// </summary>
    public static class CandyCrushBuilder
    {
        // ══ 파리티 — 원본 실측 × 1.6 (04_UIUX규칙.md). 게임 판이라 원본 그대로다.
        private const int CellSize = 112;         // 원본 70
        private const int BoardPadding = 8;       // 원본 5
        private const int BoardSize = 912;        // 원본 570

        // ══ 모바일 UX — 원본에 대응 값이 없다. 「의도된 차이」로 등재된 값이다.
        //    기준: 1080 폭에서 본문 글자 40+ · 터치 대상 높이 140+ (안드로이드 48dp ≒ 144px @xxhdpi)
        private const int FontScore = 96;         // 주 피드백. 크게 본다
        private const int FontLabel = 40;
        private const int FontTimer = 44;
        private const int FontChangeMode = 40;
        private const int FontTitle = 72;
        private const int FontModeButton = 48;

        private const int MinTouchHeight = 140;   // 터치 대상 최소 높이
        private const int ModeButtonWidth = 620;
        private const int ChangeModeWidth = 420;
        private const int ConfirmButtonWidth = 360;

        private const int ScoreBoardWidth = BoardSize;  // 보드와 좌우를 맞춘다
        private const int ScoreBoardHeight = 420;   // 라벨48+점수115+버튼140+간격24+패딩80
        private const int ScoreBoardPadding = 40;

        /// <summary>
        /// 원본 <c>#score { margin-top: -10px }</c> (<c>style.css</c>) 를 ×1.6 한 값.
        /// <b>음수다</b> — 점수 숫자가 라벨과 «겹쳐» 붙는 것이 원본의 모습이다 (실측 6px 겹침).
        /// </summary>
        private const int ScoreMarginTop = -16;

        private const int ScoreBoardTop = 140;
        private const int BoardTop = 620;

        // ══ 원본 색 (style.css) — 원본 값 그대로. 알파 역산을 하지 않는다.
        private static readonly Color TextBrown = Hex("85796B");      // style.css:9 · 53
        private static readonly Color ScoreBoardCyan = Hex("00FFFF"); // style.css:41
        private static readonly Color ModeSelectBg = Hex("F0F0F0");   // style.css:77
        private static readonly Color TitleGray = Hex("333333");      // style.css:88
        private static readonly Color ModeButtonBlue = Hex("87CEEB"); // style.css:96
        private static readonly Color ChangeModeRed = Hex("FF6347");  // style.css:111

        private const string ResourceRoot = "Assets/Resources/UI";
        private const string ArtRoot = "Assets/Candy-Crush-Game/Art";
        private const string FontAssetPath = ArtRoot + "/Font/Montserrat-Regular SDF.asset";
        private const string ScenePath = "Assets/Candy-Crush-Game/Scenes/CandyCrush.unity";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        private static TMP_FontAsset _font;

        public static void BuildAll()
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

            if (_font == null)
            {
                Log.Error($"폰트 에셋이 없다: {FontAssetPath}. TMP 세팅을 먼저 돌린다.");
                return;
            }

            Directory.CreateDirectory(ResourceRoot + "/Window");
            Directory.CreateDirectory(ResourceRoot + "/Component");
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            BuildCellPrefab();
            BuildModeSelectPrefab();
            BuildGamePrefab();
            BuildTimeUpPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildScene();

            Log.Success("프리팹 4종 + 씬 굽기 완료");
        }

        // ══════════════════════════════ 프리팹

        /// <summary>원본 <c>.grid div</c> (<c>style.css:25~33</c>) — 70×70 → 112×112.</summary>
        private static void BuildCellPrefab()
        {
            GameObject root = NewUI("CellComponent", null, new Vector2(CellSize, CellSize));

            // ★ 루트는 «항상» 레이캐스트를 받는다. 빈 칸도 드래그가 시작돼야 한다 —
            //   원본도 빈 div 에 draggable=true 가 그대로 붙어 있다.
            Image hit = root.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;

            GameObject iconGo = NewUI("Icon", root.transform, Vector2.zero);
            Stretch(iconGo);
            Image icon = iconGo.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.enabled = false;

            CellComponent cell = root.AddComponent<CellComponent>();
            SetField(cell, "_icon", icon);

            SavePrefab(root, ResourceRoot + "/Component/CellComponent.prefab");
        }

        /// <summary>원본 <c>#modeSelection</c> (<c>index.html:23~27</c> · <c>style.css:71~101</c>).</summary>
        private static void BuildModeSelectPrefab()
        {
            GameObject root = NewUI("ModeSelectWindow", null, Vector2.zero);
            Stretch(root);

            // 덮개는 «풀블리드» — 노치까지 덮어야 한다. 그래서 SafeArea 밖이다.
            Image bg = root.AddComponent<Image>();
            bg.color = ModeSelectBg;
            bg.raycastTarget = true;

            GameObject safe = NewSafeArea(root.transform);

            var layout = safe.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 32;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            TextMeshProUGUI title = NewText("Title", safe.transform, "Choose Game Mode", FontTitle, TitleGray);
            var titleLayout = title.gameObject.AddComponent<LayoutElement>();
            titleLayout.minHeight = FontTitle + 64;   // 원본 margin-bottom 을 여백으로

            Button endless = NewButton("EndlessButton", safe.transform, "Endless Mode",
                                       FontModeButton, ModeButtonBlue, Color.white,
                                       ModeButtonWidth, MinTouchHeight);
            Button timed = NewButton("TimedButton", safe.transform, "Timed Mode",
                                     FontModeButton, ModeButtonBlue, Color.white,
                                     ModeButtonWidth, MinTouchHeight);

            ModeSelectWindow window = root.AddComponent<ModeSelectWindow>();
            SetField(window, "_windowType", EWindowType.Normal);
            SetField(window, "_endlessButton", endless);
            SetField(window, "_timedButton", timed);

            SavePrefab(root, ResourceRoot + "/Window/ModeSelectWindow.prefab");
        }

        /// <summary>원본 <c>.scoreBoard</c> + <c>.grid</c>. Portrait 에서 위아래로 놓는다.</summary>
        private static void BuildGamePrefab()
        {
            GameObject root = NewUI("GameWindow", null, Vector2.zero);
            Stretch(root);

            GameObject safe = NewSafeArea(root.transform);

            // ── 스코어보드 (원본 style.css:40~54)
            //    원본은 width:auto 였지만, 모바일에서는 보드와 좌우를 맞춘 «바»가 읽기 좋다.
            GameObject board = NewUI("ScoreBoard", safe.transform,
                                     new Vector2(ScoreBoardWidth, ScoreBoardHeight));
            AnchorTopCenter(board, ScoreBoardTop);

            Image boardBg = board.AddComponent<Image>();
            boardBg.sprite = LoadSprite("UI/ui_round_32");
            boardBg.type = Image.Type.Sliced;
            boardBg.color = ScoreBoardCyan;

            // ★ 원본은 `justify-content: space-between` 이다 (`style.css:44`) —
            //   간격을 «적어 둔 값»이 아니라 «남는 높이를 똑같이 나눈 값»으로 준다.
            //   유니티 세로 레이아웃에는 그 배분이 없으므로 **늘어나는 빈 칸**을 사이사이에 넣어 흉내낸다.
            var boardLayout = board.AddComponent<VerticalLayoutGroup>();
            boardLayout.padding = new RectOffset(ScoreBoardPadding, ScoreBoardPadding,
                                                 ScoreBoardPadding, ScoreBoardPadding);
            boardLayout.childAlignment = TextAnchor.UpperCenter;
            boardLayout.spacing = 0;
            boardLayout.childControlWidth = true;
            boardLayout.childControlHeight = true;
            boardLayout.childForceExpandWidth = false;
            boardLayout.childForceExpandHeight = false;

            // ── 라벨 + 점수는 «한 덩어리»다. 원본이 점수에 `margin-top: -10` 을 줘서
            //    라벨과 «겹쳐» 붙여 놓았기 때문이다 (실측: 라벨 아래끝 517 · 점수 위끝 511 = 6px 겹침).
            //    유니티 세로 레이아웃의 간격은 «음수»가 된다 — 그걸로 그대로 옮긴다.
            GameObject titleGroup = NewUI("LabelScore", board.transform, Vector2.zero);
            var titleLayout = titleGroup.AddComponent<VerticalLayoutGroup>();
            titleLayout.childAlignment = TextAnchor.UpperCenter;
            titleLayout.spacing = ScoreMarginTop;   // 원본 -10 × 1.6
            titleLayout.childControlWidth = true;
            titleLayout.childControlHeight = true;
            titleLayout.childForceExpandWidth = false;
            titleLayout.childForceExpandHeight = false;

            var titleFitter = titleGroup.AddComponent<ContentSizeFitter>();
            titleFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 원본 h3 「score」 — CSS text-transform:uppercase 라 «표시만» 대문자다.
            TextMeshProUGUI label = NewText("Label", titleGroup.transform, "SCORE", FontLabel, TextBrown);
            label.fontStyle = FontStyles.Bold;   // 원본 weight 700 인데 폰트에 700 이 없어 «합성 볼드»

            TextMeshProUGUI score = NewText("Score", titleGroup.transform, "0", FontScore, TextBrown);
            score.fontStyle = FontStyles.Bold;

            NewFlexibleSpace("Gap1", board.transform);

            // 원본 #timer — Endless 에서 «빈 문자열»이라 높이가 0이 된다. 끄지 않는다.
            TextMeshProUGUI timer = NewText("Timer", board.transform, string.Empty, FontTimer, TextBrown);

            NewFlexibleSpace("Gap2", board.transform);

            Button changeMode = NewButton("ChangeModeButton", board.transform, "Change Mode",
                                          FontChangeMode, ChangeModeRed, Color.white,
                                          ChangeModeWidth, MinTouchHeight);

            // ── 보드 (원본 style.css:12~23)
            GameObject grid = NewUI("Board", safe.transform, new Vector2(BoardSize, BoardSize));
            AnchorTopCenter(grid, BoardTop);

            Image gridBg = grid.AddComponent<Image>();
            gridBg.sprite = LoadSprite("UI/ui_board");   // 색 + inset 그림자가 구워져 있다
            gridBg.type = Image.Type.Sliced;
            gridBg.color = Color.white;

            // 원본 box-shadow 의 «바깥» 부분: 0 1px 0 #fff (style.css:20). ×1.6 → 2px.
            // ⚠ 보드의 자식으로 두지 않는다 — GridLayoutGroup 이 셀로 잡아 65번째 칸이 된다.
            GameObject underline = NewUI("BoardBottomHighlight", safe.transform, new Vector2(BoardSize, 2));
            AnchorTopCenter(underline, BoardTop + BoardSize);
            Image underlineImage = underline.AddComponent<Image>();
            underlineImage.color = Color.white;
            underlineImage.raycastTarget = false;

            var gridLayout = grid.AddComponent<GridLayoutGroup>();
            gridLayout.padding = new RectOffset(BoardPadding, BoardPadding, BoardPadding, BoardPadding);
            gridLayout.cellSize = new Vector2(CellSize, CellSize);
            gridLayout.spacing = Vector2.zero;           // 원본에 칸 사이 간격이 없다
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 8;
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperLeft;

            GameWindow window = root.AddComponent<GameWindow>();
            SetField(window, "_windowType", EWindowType.Normal);
            SetField(window, "_scoreText", score);
            SetField(window, "_timerText", timer);
            SetField(window, "_changeModeButton", changeMode);
            SetField(window, "_boardRoot", grid.GetComponent<RectTransform>());

            SavePrefab(root, ResourceRoot + "/Window/GameWindow.prefab");
        }

        /// <summary>원본 <c>alert()</c> 의 대체. <b>원본에 없는 창이라 최소로 만든다.</b></summary>
        private static void BuildTimeUpPrefab()
        {
            GameObject root = NewUI("TimeUpPopup", null, Vector2.zero);
            Stretch(root);

            // 원본 alert 는 페이지 입력을 막는다. 보이는 딤은 «원본에 없으므로» 넣지 않고
            // 투명 블로커로 입력만 막는다.
            Image blocker = root.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0f);
            blocker.raycastTarget = true;

            GameObject safe = NewSafeArea(root.transform);

            GameObject box = NewUI("Box", safe.transform, new Vector2(880, 460));
            AnchorCenter(box);

            Image boxBg = box.AddComponent<Image>();
            boxBg.sprite = LoadSprite("UI/ui_round_32");
            boxBg.type = Image.Type.Sliced;
            boxBg.color = ScoreBoardCyan;

            var boxLayout = box.AddComponent<VerticalLayoutGroup>();
            boxLayout.padding = new RectOffset(48, 48, 48, 48);
            boxLayout.spacing = 40;
            boxLayout.childAlignment = TextAnchor.MiddleCenter;
            boxLayout.childControlWidth = true;
            boxLayout.childControlHeight = true;
            boxLayout.childForceExpandWidth = false;
            boxLayout.childForceExpandHeight = false;

            TextMeshProUGUI message = NewText("Message", box.transform,
                                              "Time's Up! Your score is 0", FontTimer, TextBrown);
            message.enableWordWrapping = true;
            var messageLayout = message.gameObject.AddComponent<LayoutElement>();
            messageLayout.preferredWidth = 780;

            Button confirm = NewButton("ConfirmButton", box.transform, "OK",
                                       FontModeButton, ModeButtonBlue, Color.white,
                                       ConfirmButtonWidth, MinTouchHeight);

            TimeUpPopup popup = root.AddComponent<TimeUpPopup>();
            SetField(popup, "_windowType", EWindowType.Popup);
            SetField(popup, "_messageText", message);
            SetField(popup, "_confirmButton", confirm);

            SavePrefab(root, ResourceRoot + "/Window/TimeUpPopup.prefab");
        }

        // ══════════════════════════════ 씬

        private static void BuildScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── 카메라. 확정표 3-c: ScreenSpace-Camera + 전용 UICamera
            var cameraGo = new GameObject("UICamera");
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;
            camera.transform.position = new Vector3(0f, 0f, -100f);
            cameraGo.tag = "MainCamera";

            // ── 캔버스
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);   // 확정표 3
            // ★ 확정표 3-b — Expand.
            //   폭 기준(Match=0)으로 두면 «세로가 짧은 화면에서 보드가 화면 밖으로 나간다» (실측).
            //   Expand 는 기준 해상도가 항상 화면 안에 들어오게 맞춘다 — 잘리지 않는다.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            // ⚠ 아트 임포트의 스프라이트 PPU 와 «짝»이다. 어긋나면 9-slice 경계가 그 비로 줄어
            //   둥근 모서리가 통째로 늘어난다 (ArtImportSetup.PixelsPerUnit 주석).
            scaler.referencePixelsPerUnit = 100f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // ── 배경 (원본 body background — 타일링). «풀블리드»라 SafeArea 밖이다.
            GameObject bgGo = NewUI("Background", canvasGo.transform, Vector2.zero);
            Stretch(bgGo);
            RawImage bgImage = bgGo.AddComponent<RawImage>();
            bgImage.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/bg_tile.png");
            bgImage.raycastTarget = false;
            bgGo.AddComponent<TiledBackground>();

            // ── UI 루트 (창이 붙는 자리). 창마다 자기 SafeArea 를 갖는다.
            GameObject uiRoot = NewUI("UIRoot", canvasGo.transform, Vector2.zero);
            Stretch(uiRoot);

            // ── EventSystem
            //
            // ⚠ 입력 모듈은 «프로젝트의 입력 처리 설정»과 짝이 맞아야 한다.
            //   Input System 패키지 프로젝트에 구형 모듈을 넣으면 재생 즉시 예외로 죽는다.
            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();

            var inputModule = eventSystemGo.AddComponent<InputSystemUIInputModule>();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

            if (actions != null)
                inputModule.actionsAsset = actions;
            else
                Log.Error($"입력 액션 에셋이 없다: {InputActionsPath}. 드래그가 안 온다.");

            // ── 루트 오브젝트
            var managerRoot = new GameObject("ManagerRoot");
            var managementRoot = new GameObject("ManagementRoot");

            // ── GameInitialize — 이 프로젝트에서 Awake 를 쓰는 유일한 곳
            var initGo = new GameObject("GameInitialize");
            GameInitialize init = initGo.AddComponent<GameInitialize>();
            SetField(init, "_uiCamera", camera);
            SetField(init, "_uiRoot", uiRoot.transform);
            SetField(init, "_managerRoot", managerRoot.transform);
            SetField(init, "_managementRoot", managementRoot.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // ══════════════════════════════ 유틸

        /// <summary>창 안의 «콘텐츠가 사는 자리». 노치·제스처 바를 피한다.</summary>
        private static GameObject NewSafeArea(Transform parent)
        {
            GameObject go = NewUI("SafeArea", parent, Vector2.zero);
            Stretch(go);
            go.AddComponent<SafeAreaPanel>();
            return go;
        }

        private static GameObject NewUI(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));

            if (parent != null)
                go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            return go;
        }

        private static void Stretch(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AnchorTopCenter(GameObject go, int topOffset)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -topOffset);
        }

        private static void AnchorCenter(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// <b>늘어나는 빈 칸.</b> 원본 <c>space-between</c> 을 흉내내는 도구다 —
        /// 사이사이에 같은 것을 넣으면 남는 높이가 <b>똑같이 나뉜다.</b>
        /// </summary>
        private static void NewFlexibleSpace(string name, Transform parent)
        {
            GameObject go = NewUI(name, parent, Vector2.zero);
            var element = go.AddComponent<LayoutElement>();
            element.flexibleHeight = 1f;
            element.minHeight = 0f;
            element.preferredHeight = 0f;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string text, int size, Color color)
        {
            GameObject go = NewUI(name, parent, new Vector2(0, size + 8));
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = _font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            return tmp;
        }

        /// <summary>
        /// 버튼 하나. <b>크기를 명시로 받는다.</b>
        ///
        /// <para>
        /// ⚠ <b><c>ContentSizeFitter</c> 를 붙이지 않는다.</b> 이 버튼들은 전부
        /// <c>VerticalLayoutGroup</c> 의 자식인데, 부모가 자식 크기를 정하는 동시에
        /// 자식이 제 크기를 스스로 정하면 <b>서로 덮어쓰며 폭주한다</b> —
        /// 실제로 버튼이 화면을 통째로 덮었다 (원장 회고).
        /// 대신 <c>LayoutElement</c> 로 <b>부모에게 크기를 «알려 준다».</b>
        /// </para>
        /// </summary>
        private static Button NewButton(string name, Transform parent, string label, int fontSize,
                                        Color background, Color textColor, int width, int height)
        {
            if (height < MinTouchHeight)
                Log.Error($"{name} 높이 {height} 가 터치 하한 {MinTouchHeight} 미만이다");

            GameObject go = NewUI(name, parent, new Vector2(width, height));

            var image = go.AddComponent<Image>();
            image.sprite = LoadSprite("UI/ui_round_8");   // 원본 border-radius 5 → 8
            image.type = Image.Type.Sliced;
            image.color = background;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;

            // 글자를 가운데 두는 용도로만 쓴다 — 크기는 아래 LayoutElement 가 정한다.
            //
            // ⚠ childForceExpand 를 켜면 이 그룹이 «자기 flexible 크기»를 1로 보고한다.
            //   그러면 부모 레이아웃이 그걸 보고 «남는 폭을 다 줘» 버튼이 부모 폭을 꽉 채운다 (실측 832).
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var element = go.AddComponent<LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
            element.minHeight = height;
            element.preferredHeight = height;

            // 「늘어나도 되는 정도」를 0으로 못 박는다 — 이게 비면 부모가 늘려도 막을 수 없다.
            element.flexibleWidth = 0f;
            element.flexibleHeight = 0f;

            TextMeshProUGUI text = NewText("Text", go.transform, label, fontSize, textColor);
            text.raycastTarget = false;

            return button;
        }

        private static Sprite LoadSprite(string relative)
        {
            string path = $"{ArtRoot}/{relative}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite == null)
                Log.Error($"스프라이트가 없다: {path}");

            return sprite;
        }

        private static void SavePrefab(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        /// <summary><c>[SerializeField]</c> private 필드를 직렬화로 채운다.</summary>
        private static void SetField(Object target, string fieldName, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);

            if (prop == null)
            {
                Log.Error($"필드를 못 찾았다: {target.GetType().Name}.{fieldName}");
                return;
            }

            if (value is Object unityObject)
                prop.objectReferenceValue = unityObject;
            else if (value is System.Enum)
                prop.enumValueIndex = (int)value;
            else if (value is int intValue)
                prop.intValue = intValue;
            else if (value is float floatValue)
                prop.floatValue = floatValue;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            return color;
        }
    }
}
