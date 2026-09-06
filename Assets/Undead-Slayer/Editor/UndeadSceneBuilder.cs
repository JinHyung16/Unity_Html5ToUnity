using System.Collections.Generic;
using JinHyung.Core;
using JinHyung.UndeadSlayer;
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
    /// 씬을 <b>코드로 굽는다</b> (확정표 10-b · CLAUDE.md 「사람에게 유니티 조작을 요구하지 않는다」).
    /// 손으로 고친 것은 다음 실행에 날아간다 — 고칠 것은 <b>이 스크립트</b>다.
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.UndeadSceneBuilder.BuildScene</c></para>
    ///
    /// <para>
    /// ★ <b>확정값이 여기서 실제 오브젝트가 된다</b> —
    /// 직교 <b>12.0833…</b>(원본 세로 580 world ÷ 2 ÷ PPU 24 · <see cref="UndeadUnits"/>) ·
    /// 캔버스 <b>1920×1080</b>(확정 3) · <c>ScreenSpace-Camera</c> + 전용 UI 카메라(확정 3-c) ·
    /// <b>Match = Height</b>(확정 3-b — 원본 불변식이 «세로 고정»이다. <c>Expand</c> 는 16:9 보다
    /// 좁은 화면에서 세로를 늘려 원본과 갈린다).
    /// </para>
    /// </summary>
    public static class UndeadSceneBuilder
    {
        public const string ScenePath = "Assets/Undead-Slayer/Scenes/UndeadSlayer.unity";

        // ── 확정표 3 — 1920 × 1080 (Landscape)
        public const int RefWidth = 1920;

        public const int RefHeight = 1080;

        /// <summary>유니티 기본 UI 레이어. 카메라 둘이 «서로의 것을 안 그리게» 가르는 기준이다.</summary>
        private const int UiLayer = 5;

        /// <summary>
        /// 시작 배경색 — 원본 묘지 바이옴의 어두운 남보라. ⚠ <b>[미측정]</b> 실제 값은 지형 타일이 덮으므로
        /// «타일이 없는 자리»에만 보인다. 단위 4 에서 실측값으로 덮는다.
        /// </summary>
        private const string PlaceholderBackgroundHex = "1E1B2E";

        public static void BuildScene()
        {
            EnsureFolder(ParentOf(ScenePath));
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ══════════════════════════════ 게임 카메라
            var gameCamGo = new GameObject("GameCamera");
            Camera gameCam = gameCamGo.AddComponent<Camera>();
            gameCam.orthographic = true;
            gameCam.clearFlags = CameraClearFlags.SolidColor;
            gameCam.backgroundColor = Hex(PlaceholderBackgroundHex);

            // ★ 직교 크기를 «여기 상수로 적지 않는다» — UndeadUnits 한 줄만 고치면 씬이 따라온다.
            gameCam.orthographicSize = UndeadUnits.CameraOrthographicSize;
            gameCam.depth = -10;

            // ★ 카메라 둘이 있으면 «각자 무엇을 그릴지» 갈라야 한다 — 안 가르면
            //   UI 카메라가 월드를 자기 시점으로 다시 그린다.
            gameCam.cullingMask = ~(1 << UiLayer);
            gameCamGo.transform.position = new Vector3(0f, 0f, -10f);

            // ★★ URP 에서는 clearFlags 로 카메라를 «겹칠 수 없다» — 위 카메라를 Overlay 로 만들어
            //    아래 카메라의 스택에 넣는다. 둘 다 Base 면 나중 카메라가 화면을 자기 배경색으로 다시 지운다.
            var gameCamData = gameCamGo.AddComponent<UniversalAdditionalCameraData>();
            gameCamData.renderType = CameraRenderType.Base;

            // ══════════════════════════════ 월드
            var worldRoot = new GameObject("World");

            // ★ 개체 렌더 — 개체당 컴포넌트 0. 여기 하나가 풀링된 SpriteRenderer 를 돌린다 (확정표 G).
            var worldViewGo = new GameObject(nameof(UndeadWorldView));
            worldViewGo.transform.SetParent(worldRoot.transform, false);
            var worldView = worldViewGo.AddComponent<UndeadWorldView>();

            // ★ 지형 — 원본이 24×24 타일 격자다 [실측]. 셀 한 칸 = 1 유닛이 되게 PPU 를 24 로 맞춰 뒀다.
            var gridGo = new GameObject("Terrain", typeof(Grid));
            gridGo.transform.SetParent(worldRoot.transform, false);
            gridGo.GetComponent<Grid>().cellSize = Vector3.one;

            var tilemapGo = new GameObject("Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
            tilemapGo.transform.SetParent(gridGo.transform, false);

            var tilemapRenderer = tilemapGo.GetComponent<TilemapRenderer>();

            // ⚠ 지형은 «맨 아래»다 — 개체 정렬(SortOrder)이 10000 근처를 쓰므로 여기는 훨씬 낮게 둔다.
            tilemapRenderer.sortingOrder = -1000;

            var terrainView = tilemapGo.AddComponent<UndeadTerrainView>();
            SetRef(terrainView, "_tilemap", tilemapGo.GetComponent<Tilemap>());

            // ★ 뜨는 문구(+N XP · 피해) — 개체와 같은 이유로 «풀링»이다
            var floatingGo = new GameObject(nameof(UndeadFloatingTextView));
            floatingGo.transform.SetParent(worldRoot.transform, false);
            var floatingText = floatingGo.AddComponent<UndeadFloatingTextView>();

            var cameraDirector = gameCamGo.AddComponent<UndeadCameraDirector>();
            SetRef(cameraDirector, "_camera", gameCam);

            // ══════════════════════════════ UI 카메라 · 캔버스
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

            // ★ 확정 3-b — Expand 가 아니라 «세로 고정»이다. 원본 불변식이 「세로 580 고정」이라서다.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            // ⚠ UI 스프라이트 PPU 와 «같은 값»이어야 한다 — 어긋나면 9-slice 모서리가 통째로 늘어난다.
            scaler.referencePixelsPerUnit = UndeadUnits.UiPixelsPerUnit;

            GameObject uiRoot = NewUI("UIRoot", canvasGo.transform);
            Stretch(uiRoot);

            SetLayerRecursively(canvasGo, UiLayer);

            EnsureEventSystem();

            // ══════════════════════════════ 초기화 (씬에서 유일한 Awake)
            var managerRoot = new GameObject("Managers");
            var managementRoot = new GameObject("Managements");

            // ★ 입력 — 확정표 B (WASD + 터치 조이스틱 병행)
            var inputGo = new GameObject(nameof(UndeadMoveInput));
            var moveInput = inputGo.AddComponent<UndeadMoveInput>();

            var initGo = new GameObject(nameof(UndeadGameInitialize));
            var init = initGo.AddComponent<UndeadGameInitialize>();

            SetRef(init, "_gameCamera", gameCam);
            SetRef(init, "_uiCamera", uiCam);
            SetRef(init, "_uiRoot", uiRoot.transform);
            SetRef(init, "_managerRoot", managerRoot.transform);
            SetRef(init, "_managementRoot", managementRoot.transform);
            SetRef(init, "_worldRoot", worldRoot.transform);
            SetRef(init, "_worldView", worldView);
            SetRef(init, "_terrainView", terrainView);
            SetRef(init, "_floatingText", floatingText);
            SetRef(init, "_cameraDirector", cameraDirector);
            SetRef(init, "_moveInput", moveInput);

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();

            Log.Success($"Undead Slayer 씬 굽기 완료 — 직교 {gameCam.orthographicSize} · " +
                        $"캔버스 {RefWidth}×{RefHeight} · Match=Height · PPU {UndeadUnits.WorldPixelsPerUnit}");
        }

        /// <summary>
        /// ⚠ <b>다른 게임의 씬 등록을 지우지 않는다.</b> 먼저 이관한 게임들의 빌드 설정이 사라지면
        /// 그 게임을 다시 세울 때 «없어진 줄도 모른다».
        /// </summary>
        private static void RegisterInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            for (int i = scenes.Count - 1; i >= 0; i--)
            {
                if (scenes[i].path == ScenePath)
                    scenes.RemoveAt(i);
            }

            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ══════════════════════════════ 유틸

        private static void EnsureEventSystem()
        {
            var go = new GameObject("EventSystem",
                                    typeof(UnityEngine.EventSystems.EventSystem),
                                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            var module = go.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            module.actionsAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(
                "Assets/InputSystem_Actions.inputactions");
        }

        private static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
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

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;

            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
        }

        private static string ParentOf(string path)
        {
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

        /// <summary>⚠ 「썼다」가 아니라 «됐다»로 확인한다 — 조용히 안 들어가면 씬은 구워지고 화면만 빈다.</summary>
        private static void SetRef(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
            {
                Log.Error($"{target.GetType().Name} 에 {field} 이 없다");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var verify = new SerializedObject(target);

            if (verify.FindProperty(field).objectReferenceValue != value)
                Log.Error($"{target.GetType().Name}.{field} 이 안 들어갔다");
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            return color;
        }
    }
}
