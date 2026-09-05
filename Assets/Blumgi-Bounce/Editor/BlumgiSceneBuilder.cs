using System.Collections.Generic;
using JinHyung.BlumgiBounce;
using JinHyung.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 씬을 <b>코드로 굽는다</b> (확정표 10-b · CLAUDE.md 「사람에게 유니티 조작을 요구하지 않는다」).
    /// 손으로 고친 것은 다음 실행에 날아간다 — 고칠 것은 <b>이 스크립트</b>다.
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.BlumgiSceneBuilder.BuildScene</c></para>
    ///
    /// <para>
    /// ★ <b>확정값이 여기서 실제 오브젝트가 된다</b> — 직교 <b>12.8</b>(확정 E 개정 · 1280 px ÷ 2 ÷ PPU 50) ·
    /// 캔버스 <b>1920×1080</b> · <c>ScreenSpace-Camera</c> + 전용 UI 카메라(확정 3-c) ·
    /// <b>Match = Height</b>(확정 3-b — <c>Expand</c> 는 16:9 보다 좁은 화면에서 세로를 늘려 원본과 갈린다).
    /// </para>
    /// </summary>
    public static class BlumgiSceneBuilder
    {
        public const string ScenePath = "Assets/Blumgi-Bounce/Scenes/BlumgiBounce.unity";

        // ── 확정표 3 — 1920 × 1080 (Landscape)
        private const int RefWidth = 1920;
        private const int RefHeight = 1080;

        /// <summary>유니티 기본 UI 레이어. 카메라 둘이 «서로의 것을 안 그리게» 가르는 기준이다.</summary>
        private const int UiLayer = 5;

        /// <summary>
        /// 게임 카메라가 보는 중심 — 원본 <b>레이아웃 스크롤</b> <c>(640, 639.5)</c> [실측].
        /// 이 점이 곧 원근 소실 중심이라 배경 부모의 피벗과 같은 값이다.
        /// </summary>
        private const double CameraCenterX = BlumgiBackgroundLayout.VanishingCenterX;

        private const double CameraCenterY = BlumgiBackgroundLayout.VanishingCenterY;

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

            // ⚠ 시작 색은 W1L1 테마다 — 실제 값은 «레벨마다» 배선이 덮어쓴다 (BlumgiLevelPresenter).
            gameCam.backgroundColor = Hex("E4FF85");

            // ★ 직교 크기 = 1280 world ÷ 2 ÷ PPU 50 = 12.8. 상수를 여기 «적지» 않는다 —
            //   확정이 흔들려도 BlumgiUnits 한 줄만 고치면 씬이 따라온다.
            gameCam.orthographicSize = BlumgiUnits.CameraOrthographicSize;
            gameCam.depth = -10;

            // ★ 카메라 둘이 있으면 «각자 무엇을 그릴지» 갈라야 한다 — 안 가르면
            //   UI 카메라가 월드를 자기 시점으로 다시 그린다.
            gameCam.cullingMask = ~(1 << UiLayer);

            Vector3 center = BlumgiUnits.ToPosition(CameraCenterX, CameraCenterY);
            gameCamGo.transform.position = new Vector3(center.x, center.y, -10f);

            // ★★ URP 에서는 clearFlags 로 카메라를 «겹칠 수 없다» — 위 카메라를 Overlay 로 만들어
            //    아래 카메라의 스택에 넣는다. 둘 다 Base 면 나중 카메라가 화면을 자기 배경색으로 다시 지운다.
            var gameCamData = gameCamGo.AddComponent<UniversalAdditionalCameraData>();
            gameCamData.renderType = CameraRenderType.Base;

            var director = gameCamGo.AddComponent<BlumgiCameraDirector>();
            SetRef(director, "_camera", gameCam);

            // ══════════════════════════════ 월드
            var worldRoot = new GameObject("World");

            var physicsGo = new GameObject("PhysicsWorld");
            physicsGo.transform.SetParent(worldRoot.transform, false);
            var physicsWorld = physicsGo.AddComponent<BlumgiPhysicsWorld>();

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

            // ★ 확정 3-b — Expand 가 아니라 «세로 고정»이다. 원본 불변식이 「세로 1280 고정」이라서다.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            // ⚠ UI 스프라이트 PPU 와 «같은 값»이어야 한다 — 어긋나면 9-slice 모서리가 통째로 늘어난다.
            scaler.referencePixelsPerUnit = BlumgiUnits.UiPixelsPerUnit;

            // ★★ 홀드 판은 «창보다 먼저» 그려져야 한다 — 그래야 버튼이 위에 온다.
            //    UIRoot 안에 두면 WindowManagement 가 밴드를 만들며 형제 순서를 밀어내므로
            //    UIRoot 의 «형제»로 두고 첫 자식에 고정한다.
            GameObject holdArea = NewUI("HoldArea", canvasGo.transform);
            Stretch(holdArea);

            var holdImage = holdArea.AddComponent<Image>();
            holdImage.color = new Color(0f, 0f, 0f, 0f);
            holdImage.raycastTarget = true;

            var holdInput = holdArea.AddComponent<BlumgiHoldInput>();

            GameObject uiRoot = NewUI("UIRoot", canvasGo.transform);
            Stretch(uiRoot);

            holdArea.transform.SetAsFirstSibling();

            SetLayerRecursively(canvasGo, UiLayer);

            EnsureEventSystem();

            // ══════════════════════════════ 초기화 (씬에서 유일한 Awake)
            var managerRoot = new GameObject("Managers");
            var managementRoot = new GameObject("Managements");

            var initGo = new GameObject(nameof(BlumgiGameInitialize));
            var init = initGo.AddComponent<BlumgiGameInitialize>();

            SetRef(init, "_gameCamera", gameCam);
            SetRef(init, "_uiCamera", uiCam);
            SetRef(init, "_uiRoot", uiRoot.transform);
            SetRef(init, "_managerRoot", managerRoot.transform);
            SetRef(init, "_managementRoot", managementRoot.transform);
            SetRef(init, "_worldRoot", worldRoot.transform);
            SetRef(init, "_physicsWorld", physicsWorld);
            SetRef(init, "_cameraDirector", director);
            SetRef(init, "_holdInput", holdInput);

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();

            Log.Success($"Blumgi 씬 굽기 완료 — 직교 {gameCam.orthographicSize} · 캔버스 {RefWidth}×{RefHeight} " +
                        $"· Match=Height · 카메라 중심 ({CameraCenterX}, {CameraCenterY})");
        }

        /// <summary>
        /// ⚠ <b>다른 게임의 씬 등록을 지우지 않는다.</b> 먼저 이관한 두 게임의 빌드 설정이 사라지면
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
