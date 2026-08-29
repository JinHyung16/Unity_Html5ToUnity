using System.IO;
using JinHyung.Core;
using JinHyung.PacMan;
using JinHyung.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 프리팹과 씬을 <b>코드로 굽는다</b> (확정표 10-b).
    /// 손으로 고친 것은 다음 실행에 날아간다 — 고칠 것은 이 스크립트다.
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.PacManBuilder.BuildAll</c></para>
    /// </summary>
    public static class PacManBuilder
    {
        private const string ArtRoot = "Assets/Pac-Man-Game/Art";
        private const string ResourceRoot = "Assets/Pac-Man-Game/Resources/UI";
        private const string ScenePath = "Assets/Pac-Man-Game/Scenes/PacMan.unity";

        // ── 확정표 3 · 3-b · 3-c
        private const int RefWidth = 1080;
        private const int RefHeight = 1920;

        // ── 04_UIUX규칙.md — 배율 1.8 · 칸 36
        private const int Cols = 28;
        private const int Rows = 31;
        private const int CellSize = 36;

        // ── 모바일 UX 하한 (첫 게임과 같은 이유로 키운다 — 「의도된 차이」 등재)
        private const int FontTitle = 64;
        private const int FontLabel = 40;
        private const int FontHint = 36;
        private const int MinTouchHeight = 140;
        private const int RestartWidth = 360;

        /// <summary>헤더가 먹는 세로 픽셀 (여백 40 + 제목 + 간격 + 점수줄). 보드를 이만큼 내린다.</summary>
        private const int HeaderPixels = 420;

        // ── 색 (05_연출.md)
        private static readonly Color CanvasBg = Hex("000016");
        private static readonly Color PageBg = Hex("01010A");
        private static readonly Color TextColor = Hex("F8F8FF");
        private static readonly Color RestartBg = Hex("F5C33B");
        private static readonly Color RestartText = Hex("1B1B1B");
        private static readonly Color Cyan = Hex("00FFFF");

        /// <summary>유니티 기본 UI 레이어. 카메라 둘이 «서로의 것을 안 그리게» 가르는 기준이다.</summary>
        private const int UiLayer = 5;

        private static TMP_FontAsset _font;

        public static void BuildAll()
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Candy-Crush-Game/Art/Font/Montserrat SDF.asset");

            // ⚠ `Directory.CreateDirectory` 로 만든 폴더는 «AssetDatabase 가 모른다» —
            //   그 상태로 CreateAsset 을 부르면 «조용히 실패»한다 (실측: 타일 3종이 0개로 남았다).
            //   에디터 API 로 만들어 즉시 등록되게 한다.
            EnsureFolder(ResourceRoot + "/Window");
            EnsureFolder(ParentOf(ScenePath));

            AssetDatabase.Refresh();

            BuildWindowPrefab();
            BuildAlertPrefab();
            BuildScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log.Success("Pac-Man 창 1종 + 씬 굽기 완료 (타일은 런타임 생성)");
        }

        /// <summary>
        /// 폴더를 <b>AssetDatabase 에 등록되게</b> 만든다.
        /// ⚠ 파일시스템으로만 만들면 그 안에 만든 에셋이 «조용히 사라진다».
        /// </summary>
        private static string ParentOf(string path)
        {
            // ⚠ 에셋 경로는 «항상 슬래시»다. 역슬래시를 섞으면 AssetDatabase 가 못 찾는다.
            int index = path.LastIndexOf('/');
            return index < 0 ? string.Empty : path.Substring(0, index);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = ParentOf(path);
            string leaf = path.Substring(parent.Length + 1);

            if (AssetDatabase.IsValidFolder(parent) == false)
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, leaf);
        }

        // ────────────────────────────── 창

        private static void BuildWindowPrefab()
        {
            GameObject root = NewUI("PacGameWindow", null, Vector2.zero);
            Stretch(root);

            // ⚠ 이 창에는 BG 가 «없다» — 배경은 게임 카메라가 칠하고, 보드가 창 «뒤»에 있다.
            //   창에 불투명 배경을 깔면 «미로가 통째로 가려진다» (실측: 타일 812개가 칠해졌는데 화면이 검었다).
            //   창 구조 규칙(Window → BG · SafeArea)은 «배경이 있는 창»의 규칙이다.
            GameObject safe = NewSafeArea(root.transform);

            // ── 헤더 (원본 .header — 제목 · 점수 · 목숨 · Restart)
            GameObject header = NewUI("Header", safe.transform, new Vector2(RefWidth, 320));
            AnchorTopCenter(header, 40);

            var headerLayout = header.AddComponent<VerticalLayoutGroup>();
            headerLayout.childAlignment = TextAnchor.UpperCenter;
            headerLayout.spacing = 24;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;

            TextMeshProUGUI title = NewText("Title", header.transform, "PAC-MAN GAME", FontTitle, TextColor);
            title.fontStyle = FontStyles.Bold;

            GameObject row = NewUI("ScoreRow", header.transform, new Vector2(RefWidth - 80, MinTouchHeight));

            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.spacing = 24;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            NewText("ScoreLabel", row.transform, "Score:", FontLabel, TextColor);
            TextMeshProUGUI score = NewText("Score", row.transform, "0", FontLabel, TextColor);
            NewText("LivesLabel", row.transform, "Lives:", FontLabel, TextColor);
            TextMeshProUGUI lives = NewText("Lives", row.transform, "3", FontLabel, TextColor);

            Button restart = NewButton("RestartButton", row.transform, "Restart",
                                       FontLabel, RestartBg, RestartText, RestartWidth, MinTouchHeight);

            // ── 안내 문구 (원본 .hint)
            TextMeshProUGUI hint = NewText("Hint", safe.transform, string.Empty, FontHint, TextColor);
            hint.enableWordWrapping = true;
            var hintRect = hint.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 0f);
            hintRect.anchorMax = new Vector2(0.5f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.sizeDelta = new Vector2(RefWidth - 120, 120);
            hintRect.anchoredPosition = new Vector2(0f, 40f);

            // ── 스와이프 판 — 보드 위 어디를 밀어도 먹혀야 하므로 «화면 전체»다.
            //   ⚠ 헤더보다 «먼저» 그려져야 버튼이 위에 온다.
            GameObject swipeArea = NewUI("SwipeArea", safe.transform, Vector2.zero);
            Stretch(swipeArea);
            swipeArea.transform.SetAsFirstSibling();

            var swipeImage = swipeArea.AddComponent<Image>();
            swipeImage.color = new Color(0f, 0f, 0f, 0f);
            swipeImage.raycastTarget = true;
            SwipeInput swipe = swipeArea.AddComponent<SwipeInput>();

            PacGameWindow window = root.AddComponent<PacGameWindow>();
            SetField(window, "_windowType", EWindowType.Normal);
            SetField(window, "_scoreText", score);
            SetField(window, "_livesText", lives);
            SetField(window, "_hintText", hint);
            SetField(window, "_restartButton", restart);
            SetField(window, "_swipe", swipe);

            // 창도 UI 레이어여야 UI 카메라가 그린다.
            SetLayerRecursively(root, UiLayer);

            SavePrefab(root, ResourceRoot + "/Window/PacGameWindow.prefab");
        }

        /// <summary>원본 <c>alert()</c> 대응. 보드를 숨기지 않는다 — 투명 블로커 + 상자.</summary>
        private static void BuildAlertPrefab()
        {
            GameObject root = NewUI("PacAlertPopup", null, Vector2.zero);
            Stretch(root);

            // 원본 alert 는 페이지 입력을 막는다. 보이는 딤은 원본에 없으므로 투명 블로커만.
            GameObject bg = NewUI("BG", root.transform, Vector2.zero);
            Stretch(bg);
            var blocker = bg.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0f);
            blocker.raycastTarget = true;

            GameObject safe = NewSafeArea(root.transform);

            GameObject box = NewUI("Box", safe.transform, new Vector2(880, 420));
            var boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.anchoredPosition = Vector2.zero;

            var boxBg = box.AddComponent<Image>();
            boxBg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Candy-Crush-Game/Art/UI/ui_round_32.png");
            boxBg.type = Image.Type.Sliced;
            boxBg.color = Hex("0A1A3A");

            var boxLayout = box.AddComponent<VerticalLayoutGroup>();
            boxLayout.padding = new RectOffset(48, 48, 48, 48);
            boxLayout.childAlignment = TextAnchor.MiddleCenter;
            boxLayout.spacing = 32;
            boxLayout.childControlWidth = true;
            boxLayout.childControlHeight = true;
            boxLayout.childForceExpandWidth = false;
            boxLayout.childForceExpandHeight = false;

            TextMeshProUGUI message = NewText("Message", box.transform, "Game Over!", 52, TextColor);
            message.enableWordWrapping = true;

            Button confirm = NewButton("ConfirmButton", box.transform, "OK",
                                       FontLabel, RestartBg, RestartText, 320, MinTouchHeight);

            PacAlertPopup popup = root.AddComponent<PacAlertPopup>();
            SetField(popup, "_windowType", EWindowType.Popup);
            SetField(popup, "_messageText", message);
            SetField(popup, "_confirmButton", confirm);

            SetLayerRecursively(root, UiLayer);
            SavePrefab(root, ResourceRoot + "/Window/PacAlertPopup.prefab");
        }

        // ────────────────────────────── 씬

        private static void BuildScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── 게임 카메라 (Tilemap 을 비춘다)
            var gameCamGo = new GameObject("GameCamera");
            Camera gameCam = gameCamGo.AddComponent<Camera>();
            gameCam.orthographic = true;
            gameCam.clearFlags = CameraClearFlags.SolidColor;
            gameCam.backgroundColor = CanvasBg;
            gameCam.depth = -10;

            // ★ 카메라 둘이 있으면 «각자 무엇을 그릴지» 갈라야 한다.
            //   ⚠ 안 가르면 UI 카메라가 «월드를 자기 시점으로 다시» 그린다 —
            //     화면에는 엉뚱한 배율의 미로가 뜬다 (실측: 3칸만 크게 보였다).
            gameCam.cullingMask = ~(1 << UiLayer);

            // ★★ URP 에서는 `clearFlags` 로 카메라를 «겹칠 수 없다».
            //   두 카메라를 다 Base 로 두면 나중 카메라가 화면을 «자기 배경색으로 다시 지운다» —
            //   실측: 미로가 통째로 사라지고 UI 카메라 배경색만 남았다.
            //   겹치려면 위 카메라를 Overlay 로 만들고 «아래 카메라의 스택»에 넣어야 한다.
            var gameCamData = gameCamGo.AddComponent<UniversalAdditionalCameraData>();
            gameCamData.renderType = CameraRenderType.Base;

            // ★ 보드 «폭»(28칸)이 화면에 들어와야 한다 — 세로 화면이라 폭이 먼저 모자란다.
            //   ⚠ 세로 기준으로 잡으면 좌우가 잘린다 (실측: 19.7칸만 보였다).
            float aspect = (float)RefWidth / RefHeight;
            gameCam.orthographicSize = Cols * 0.5f / aspect;

            // ★ 헤더가 화면 위를 먹는다 — 보드 «윗변»이 헤더 아래로 오게 카메라를 맞춘다.
            //   ⚠ 눈대중으로 내리면 기기 비율이 바뀔 때 다시 겹친다. 픽셀을 유닛으로 환산해 계산한다.
            float headerUnits = HeaderPixels / (float)RefHeight * (gameCam.orthographicSize * 2f);
            float cameraY = headerUnits + 0.5f - gameCam.orthographicSize;

            gameCamGo.transform.position = new Vector3((Cols - 1) * 0.5f, cameraY, -10f);

            // ── Tilemap
            var gridGo = new GameObject("Grid", typeof(Grid));
            var grid = gridGo.GetComponent<Grid>();
            grid.cellSize = Vector3.one;

            var tilemapGo = new GameObject("Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
            tilemapGo.transform.SetParent(gridGo.transform, false);

            var tilemapRenderer = tilemapGo.GetComponent<TilemapRenderer>();
            tilemapRenderer.sortingOrder = 0;

            // ★★ 타일 앵커를 (0,0) 으로 — 타일 «중심»이 셀 원점 (c, -r) 에 오게 한다.
            //   ⚠ 기본값 (0.5,0.5) 로 두면 타일 중심이 (c+0.5, -r+0.5) 인데
            //     개체(팩맨·유령)는 (c, -r) 에 그리므로 «판과 개체가 반 칸 어긋난다» (검산으로 발견).
            //     원본은 둘 다 «칸 중심» 기준이다 — 좌표계는 하나여야 한다.
            tilemapGo.GetComponent<Tilemap>().tileAnchor = Vector3.zero;

            // ⚠ 머티리얼을 안 물리면 «칠해져 있는데 안 보인다» — 타일 수는 맞는데 화면이 검다.
            tilemapRenderer.sharedMaterial =
                AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

            var entityRoot = new GameObject("Entities");
            entityRoot.transform.SetParent(gridGo.transform, false);

            // ── 팩맨 · 유령
            SpriteRenderer pacman = NewSpriteRenderer("Pacman", entityRoot.transform, "pac_0", 10);
            SpriteRenderer ghostPrefab = NewSpriteRenderer("GhostTemplate", entityRoot.transform, "ghost", 10);

            // ⚠ 템플릿은 «꺼 둔다». 켜 두면 원점에 흰 유령이 하나 더 서 있다 (실측).
            ghostPrefab.gameObject.SetActive(false);

            var mazeViewGo = new GameObject("MazeView");
            MazeView mazeView = mazeViewGo.AddComponent<MazeView>();
            SetField(mazeView, "_tilemap", tilemapGo.GetComponent<Tilemap>());
            SetField(mazeView, "_entityRoot", entityRoot.transform);
            SetField(mazeView, "_wallSprite", LoadSprite("tile_wall"));
            SetField(mazeView, "_pelletSprite", LoadSprite("tile_pellet"));
            SetField(mazeView, "_powerSprite", LoadSprite("tile_power"));
            SetField(mazeView, "_pacman", pacman);
            SetField(mazeView, "_ghostPrefab", ghostPrefab);
            SetField(mazeView, "_pacFrames", LoadPacFrames());

            // ── UI
            var uiCamGo = new GameObject("UICamera");
            Camera uiCam = uiCamGo.AddComponent<Camera>();
            uiCam.orthographic = true;
            uiCam.clearFlags = CameraClearFlags.Depth;
            uiCam.depth = 10;
            uiCam.cullingMask = 1 << UiLayer;
            uiCamGo.transform.position = new Vector3(0f, 0f, -100f);

            var uiCamData = uiCamGo.AddComponent<UniversalAdditionalCameraData>();
            uiCamData.renderType = CameraRenderType.Overlay;
            gameCamData.cameraStack.Add(uiCam);

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler),
                                          typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCam;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;

            var uiRoot = new GameObject("UIRoot", typeof(RectTransform));
            uiRoot.transform.SetParent(canvasGo.transform, false);
            Stretch(uiRoot);

            SetLayerRecursively(canvasGo, UiLayer);

            EnsureEventSystem();

            // ── 오디오
            var audioGo = new GameObject("PacAudio");
            var bgm = audioGo.AddComponent<AudioSource>();
            bgm.playOnAwake = false;
            var sfx = audioGo.AddComponent<AudioSource>();
            sfx.playOnAwake = false;

            PacAudio audio = audioGo.AddComponent<PacAudio>();
            SetField(audio, "_bgm", bgm);
            SetField(audio, "_sfx", sfx);
            SetField(audio, "_siren", LoadClip("bgm_siren"));
            SetField(audio, "_pellet", LoadClip("sfx_pellet"));
            SetField(audio, "_death", LoadClip("sfx_death"));

            // ── 초기화 (씬에서 유일한 Awake)
            var initGo = new GameObject("PacGameInitialize");
            var managerRoot = new GameObject("Managers");
            var managementRoot = new GameObject("Managements");

            PacGameInitialize init = initGo.AddComponent<PacGameInitialize>();
            SetField(init, "_uiCamera", uiCam);
            SetField(init, "_uiRoot", uiRoot.transform);
            SetField(init, "_managerRoot", managerRoot.transform);
            SetField(init, "_managementRoot", managementRoot.transform);
            SetField(init, "_mazeView", mazeView);
            SetField(init, "_audio", audio);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // ────────────────────────────── 유틸

        private static SpriteRenderer NewSpriteRenderer(string name, Transform parent,
                                                        string spriteName, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/{spriteName}.png");
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Sprite[] LoadPacFrames()
        {
            var frames = new Sprite[PacSpriteBuilder.ChompFrames];

            for (int i = 0; i < frames.Length; i++)
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/pac_{i}.png");

            return frames;
        }

        private static Sprite LoadSprite(string name)
        {
            string path = $"{ArtRoot}/{name}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            // ⚠ 「없으면 조용히 null」이 가장 나쁜 실패다 — 씬은 구워지고 화면만 빈다.
            if (sprite == null)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Log.Error($"스프라이트를 못 읽었다: {path}"
                          + $" (임포터 {(importer == null ? "없음" : importer.textureType.ToString())})");
            }

            return sprite;
        }

        private static AudioClip LoadClip(string name)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>($"{ArtRoot}/Audio/{name}.wav");
        }

        private static void EnsureEventSystem()
        {
            var go = new GameObject("EventSystem",
                                    typeof(UnityEngine.EventSystems.EventSystem),
                                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            var module = go.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            module.actionsAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(
                "Assets/InputSystem_Actions.inputactions");
        }

        private static GameObject NewSafeArea(Transform parent)
        {
            GameObject go = NewUI("SafeArea", parent, Vector2.zero);
            Stretch(go);
            go.AddComponent<SafeArea>();
            return go;
        }

        private static Image NewBackground(Transform parent, Color color)
        {
            GameObject go = NewUI("BG", parent, Vector2.zero);
            Stretch(go);

            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
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

        private static void AnchorTopCenter(GameObject go, float top)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string text,
                                               int size, Color color)
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
        /// ⚠ <c>ContentSizeFitter</c> 를 붙이지 않는다 — 레이아웃 그룹의 자식이면
        /// 서로 크기를 덮어써 <b>폭주</b>한다 (첫 게임 회고).
        /// </summary>
        private static Button NewButton(string name, Transform parent, string label, int fontSize,
                                        Color background, Color textColor, int width, int height)
        {
            GameObject go = NewUI(name, parent, new Vector2(width, height));

            var image = go.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Candy-Crush-Game/Art/UI/ui_round_32.png");
            image.type = Image.Type.Sliced;
            image.color = background;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;

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
            element.flexibleWidth = 0f;
            element.flexibleHeight = 0f;

            TextMeshProUGUI text = NewText("Text", go.transform, label, fontSize, textColor);
            text.raycastTarget = false;
            return button;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;

            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
        }

        private static void SavePrefab(GameObject root, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void SetField(Object target, string fieldName, object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Log.Error($"{target.GetType().Name} 에 {fieldName} 이 없다");
                return;
            }

            if (value is int intValue)
                property.intValue = intValue;
            else if (value is Object objectValue)
                property.objectReferenceValue = objectValue;
            else if (value is System.Enum enumValue)
                property.enumValueIndex = System.Convert.ToInt32(enumValue);
            else if (value is Sprite[] sprites)
            {
                property.arraySize = sprites.Length;

                for (int i = 0; i < sprites.Length; i++)
                    property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            // ⚠ 「썼다」가 아니라 «됐다»로 확인한다 — 조용히 안 들어가면
            //   씬은 멀쩡히 구워지고 화면만 빈다 (실측: 스프라이트 3종이 0으로 남았다).
            if (value is Object expected && expected != null)
            {
                var verify = new SerializedObject(target);

                if (verify.FindProperty(fieldName).objectReferenceValue != expected)
                    Log.Error($"{target.GetType().Name}.{fieldName} 이 안 들어갔다 ({expected.name})");
            }
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }
    }
}
