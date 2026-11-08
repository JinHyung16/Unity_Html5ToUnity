using System.Collections.Generic;
using JinHyung.Core;
using JinHyung.FrontWarsIo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// FrontWars-io 씬의 <b>첫 초안</b>을 굽는다 (확정표 10-b — 빌더는 첫 초안만, 이후 씬·프리팹이 주인).
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.FrontWarsSceneBuilder.BuildScene</c></para>
    ///
    /// <para>
    /// ⚠ <b>씬이 이미 있으면 덮어쓰지 않고 멈춘다</b> (원칙 R22). 다시 굽고 싶으면 <see cref="RebuildScene"/> 을
    /// 사람이 명시했을 때만 부른다.
    /// </para>
    ///
    /// <para>
    /// 확정값: 캔버스 1920×1080(확정 3) · ScreenSpace-Camera + 전용 UI 카메라(3-c) · Match 0.5(3-b —
    /// 원본 HUD 가 창 크기와 무관한 px 고정이라 한쪽 기준이 원본과 같지 않다).
    /// </para>
    /// </summary>
    public static class FrontWarsSceneBuilder
    {
        public const string ScenePath = "Assets/FrontWars-io/Scenes/FrontWarsIo.unity";

        public const int RefWidth = 1920;
        public const int RefHeight = 1080;

        /// <summary>확정 3-b — 가로·세로 반반.</summary>
        public const float MatchWidthOrHeight = 0.5f;

        private const int UiLayer = 5;

        /// <summary>원본 배경 rgb(27,35,48) `[소스] BMmXchgl.js:this.background=O({r:27,g:35,b:48})`. 씬에 굽힌 뒤에는 씬이 주인이다.</summary>
        private static readonly Color Background = new Color32(27, 35, 48, 255);

        public static void BuildScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Log.Warning($"[FrontWarsSceneBuilder] 씬이 이미 있다 — 덮어쓰지 않는다: {ScenePath}");
                return;
            }

            Build();
        }

        /// <summary>⚠ 사람이 다시 굽기를 명시했을 때만 부른다 — 씬에서 손본 것이 사라진다.</summary>
        public static void RebuildScene()
        {
            Build();
        }

        private static void Build()
        {
            EnsureFolder("Assets/FrontWars-io/Scenes");
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── 게임 카메라 (지도) — 직교. 줌·이동은 단위 8 의 카메라가 바꾼다.
            var gameCamGo = new GameObject("GameCamera");
            Camera gameCam = gameCamGo.AddComponent<Camera>();
            gameCam.orthographic = true;
            gameCam.clearFlags = CameraClearFlags.SolidColor;
            gameCam.backgroundColor = Background;
            gameCam.orthographicSize = RefHeight * 0.5f;
            gameCam.depth = -10;
            gameCam.cullingMask = ~(1 << UiLayer);
            gameCamGo.transform.position = new Vector3(0f, 0f, -10f);

            var gameCamData = gameCamGo.AddComponent<UniversalAdditionalCameraData>();
            gameCamData.renderType = CameraRenderType.Base;

            var worldRoot = new GameObject("World");

            // ── UI 카메라 · 캔버스
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

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCam;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = MatchWidthOrHeight;

            var uiRoot = new GameObject("UIRoot", typeof(RectTransform));
            uiRoot.transform.SetParent(canvasGo.transform, false);
            var uiRect = uiRoot.GetComponent<RectTransform>();
            uiRect.anchorMin = Vector2.zero;
            uiRect.anchorMax = Vector2.one;
            uiRect.offsetMin = Vector2.zero;
            uiRect.offsetMax = Vector2.zero;

            SetLayerRecursively(canvasGo, UiLayer);
            EnsureEventSystem();

            // ── 초기화 (씬에서 유일한 Awake)
            var managerRoot = new GameObject("Managers");
            var managementRoot = new GameObject("Managements");

            var initGo = new GameObject(nameof(FrontWarsGameInitialize));
            var init = initGo.AddComponent<FrontWarsGameInitialize>();

            SetRef(init, "_gameCamera", gameCam);
            SetRef(init, "_uiCamera", uiCam);
            SetRef(init, "_uiRoot", uiRoot.transform);
            SetRef(init, "_managerRoot", managerRoot.transform);
            SetRef(init, "_managementRoot", managementRoot.transform);
            SetRef(init, "_worldRoot", worldRoot.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();

            Log.Success($"FrontWars-io 씬 굽기 완료 — 캔버스 {RefWidth}×{RefHeight} · Match {MatchWidthOrHeight} · " +
                        $"배경 #{ColorUtility.ToHtmlStringRGB(Background)}");
        }

        private const string TerritoryShaderPath = "Assets/FrontWars-io/Art/Shaders/FrontWarsTerritory.shader";

        /// <summary>
        /// 이미 있는 씬에 월드(지도 · 유닛 · 이름 · 입력 · 카메라 조종) · 창 조종을 <b>없는 것만</b> 더한다 (원칙 R22 — 씬을 새로 굽지 않는다).
        /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.FrontWarsSceneBuilder.AddWorldAndManagement</c></para>
        /// </summary>
        public static void AddWorldAndManagement()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (scene.path != ScenePath)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    if (SceneManager.GetSceneAt(i).isDirty)
                    {
                        Log.Error($"[FrontWarsSceneBuilder] 저장 안 된 씬이 열려 있다 — 건드리지 않는다: {SceneManager.GetSceneAt(i).path}");
                        return;
                    }
                }

                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            GameObject initGo = FindRoot(scene, nameof(FrontWarsGameInitialize));
            GameObject cameraGo = FindRoot(scene, "GameCamera");
            GameObject world = FindRoot(scene, "World");
            GameObject managements = FindRoot(scene, "Managements");

            if (initGo == null || cameraGo == null || world == null || managements == null)
            {
                Log.Error("[FrontWarsSceneBuilder] 씬 뼈대(초기화 · GameCamera · World · Managements)가 없다");
                return;
            }

            Camera gameCam = cameraGo.GetComponent<Camera>();

            GameObject map = Child(world, "Map");
            GetOrAdd<MeshFilter>(map);
            GetOrAdd<MeshRenderer>(map);
            FrontWarsMapRenderer renderer = GetOrAdd<FrontWarsMapRenderer>(map);
            SetRef(renderer, "_shader", AssetDatabase.LoadAssetAtPath<Shader>(TerritoryShaderPath));
            SetRef(renderer, "_camera", gameCam);

            FrontWarsUnitView units = GetOrAdd<FrontWarsUnitView>(Child(world, "Units"));
            FrontWarsNameView names = GetOrAdd<FrontWarsNameView>(Child(world, "Names"));
            FrontWarsMapInput input = GetOrAdd<FrontWarsMapInput>(Child(world, "Input"));
            FrontWarsFxView fx = GetOrAdd<FrontWarsFxView>(Child(world, "Fx"));
            // 철도 — 칸마다 선로 조각 하나를 한 메시로 그린다 (줌 1 부터 보인다)
            FrontWarsRailView rails = GetOrAdd<FrontWarsRailView>(Child(world, "Rails"));
            // 성능 표시 — 설정 「성능 오버레이」 · Shift+D 로 켠다 (기본 꺼짐)
            FrontWarsPerfOverlay perf = GetOrAdd<FrontWarsPerfOverlay>(Child(world, "PerfOverlay"));

            FrontWarsCameraController cameraController = GetOrAdd<FrontWarsCameraController>(cameraGo);
            SetRef(cameraController, "_camera", gameCam);

            FrontWarsWorldView view = GetOrAdd<FrontWarsWorldView>(world);
            SetRef(view, "_mapRenderer", renderer);
            SetRef(view, "_unitView", units);
            SetRef(view, "_nameView", names);
            SetRef(view, "_fxView", fx);
            SetRef(view, "_perfOverlay", perf);
            SetRef(view, "_railView", rails);
            SetRef(view, "_cameraController", cameraController);
            SetRef(view, "_input", input);
            SetRef(view, "_gameCamera", gameCam);

            FrontWarsManagement management = GetOrAdd<FrontWarsManagement>(Child(managements, nameof(FrontWarsManagement)));

            var init = initGo.GetComponent<FrontWarsGameInitialize>();
            SetRef(init, "_worldView", view);
            SetRef(init, "_management", management);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Log.Success("FrontWars-io 씬에 월드 · 창 조종을 더했다");
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == name)
                    return go;
            }

            return null;
        }

        private static GameObject Child(GameObject parent, string name)
        {
            Transform found = parent.transform.Find(name);

            if (found != null)
                return found.gameObject;

            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static T GetOrAdd<T>(GameObject go)
            where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        /// <summary>⚠ 다른 게임의 씬 등록을 지우지 않는다.</summary>
        private static void RegisterInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
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

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;

            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int index = path.LastIndexOf('/');
            string parent = path.Substring(0, index);
            string leaf = path.Substring(index + 1);

            if (AssetDatabase.IsValidFolder(parent) == false)
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>「썼다」가 아니라 «됐다»로 확인한다.</summary>
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

            if (new SerializedObject(target).FindProperty(field).objectReferenceValue != value)
                Log.Error($"{target.GetType().Name}.{field} 이 안 들어갔다");
        }
    }
}
