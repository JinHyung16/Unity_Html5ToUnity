using JinHyung.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// <b>확정표를 프로젝트 설정에 반영한다.</b>
    ///
    /// <para>
    /// ⚠ <b>확정표에 적어 둔 답이 프로젝트 설정에 안 들어가면 그 확정은 종이에만 있는 것이다.</b>
    /// 「타깃은 모바일 · Portrait」라고 확정해 놓고 빌드 타깃이 Windows 이고
    /// 화면 방향이 자동 회전이면, 그 프로젝트는 <b>확정한 적이 없는 것과 같다.</b>
    /// </para>
    ///
    /// <para>
    /// 여기 값은 전부 그 게임 원장의 <b>확정표에서 온다.</b> 임의로 정하지 않는다.
    /// </para>
    /// </summary>
    public static class PlatformSetup
    {
        // ── 확정표 (원장 「확정표」) ─────────────────────────────
        private const string CompanyName = "JinHyung";
        private const string ProductName = "Candy Crush";
        private const string PackageName = "com.jinhyung.candycrush";

        /// <summary>확정표 1·1-b — 모바일 / Android.</summary>
        private const BuildTarget TargetPlatform = BuildTarget.Android;
        private const BuildTargetGroup TargetGroup = BuildTargetGroup.Android;

        /// <summary>확정표 2 — Portrait 고정. 원본이 세로 재배치 전제다.</summary>
        private const UIOrientation Orientation = UIOrientation.Portrait;

        /// <summary>확정표 3 — 에디터 Game 뷰 기본 크기와 맞춘다.</summary>
        private const int DefaultWidth = 1080;
        private const int DefaultHeight = 1920;

        /// <summary>확정표 6 — Linear 유지.</summary>
        private const ColorSpace Space = ColorSpace.Linear;

        public static void Setup()
        {
            ApplyCommon();
            ApplyAndroid();
            ApplyStandalone();

            AssetDatabase.SaveAssets();
            SwitchTarget();

            Log.Success(BuildReport());
        }

        // ────────────────────────────── 적용

        private static void ApplyCommon()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;

            // 확정표 6 — 색공간. 바꾸면 전 텍스처가 재임포트된다.
            if (PlayerSettings.colorSpace != Space)
                PlayerSettings.colorSpace = Space;

            // 확정표 2 — Portrait «고정». 자동 회전을 끄지 않으면
            // 가로로 돌렸을 때 원본에 없는 레이아웃이 나온다.
            PlayerSettings.defaultInterfaceOrientation = Orientation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            PlayerSettings.defaultScreenWidth = DefaultWidth;
            PlayerSettings.defaultScreenHeight = DefaultHeight;

            // 원본에 스플래시가 없다. 켜 두면 «원본에 없는 화면»이 한 겹 붙는다.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
        }

        private static void ApplyAndroid()
        {
            var named = NamedBuildTarget.Android;

            PlayerSettings.SetApplicationIdentifier(named, PackageName);

            // Play 스토어가 64비트를 요구한다. IL2CPP + ARM64 가 사실상 강제다.
            PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // ⚠ Linear 색공간은 OpenGLES2 로는 못 쓴다. 자동 그래픽 API 를 끄고
            //   Vulkan / GLES3 만 남긴다 — 안 그러면 기기에 따라 색이 갈린다.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                                           new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // 확정표 7 — 리소스를 4의 배수로 구운 이유가 여기다.
            // ASTC 는 블록이 4의 배수여야 한다. 안 맞으면 압축이 떨어지거나 늘어난다.
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
        }

        private static void ApplyStandalone()
        {
            // PC 는 «에디터 검증용»이다. 타깃이 아니므로 최소만 맞춘다.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        }

        private static void SwitchTarget()
        {
            if (EditorUserBuildSettings.activeBuildTarget == TargetPlatform)
                return;

            if (BuildPipeline.IsBuildTargetSupported(TargetGroup, TargetPlatform) == false)
            {
                Log.Warning($"{TargetPlatform} 모듈이 설치돼 있지 않다. 설정만 반영하고 타깃은 안 바꾼다.");
                return;
            }

            // ⚠ 타깃 전환은 전 에셋 재임포트를 부른다. 느린 것이 정상이다.
            EditorUserBuildSettings.SwitchActiveBuildTarget(TargetGroup, TargetPlatform);
        }

        // ────────────────────────────── 보고

        private static string BuildReport()
        {
            var named = NamedBuildTarget.Android;
            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);

            return "플랫폼 설정 반영\n"
                   + $"  활성 빌드 타깃 : {EditorUserBuildSettings.activeBuildTarget}\n"
                   + $"  색공간         : {PlayerSettings.colorSpace}\n"
                   + $"  화면 방향      : {PlayerSettings.defaultInterfaceOrientation} "
                   + $"(자동회전 P{Bit(PlayerSettings.allowedAutorotateToPortrait)}"
                   + $"/U{Bit(PlayerSettings.allowedAutorotateToPortraitUpsideDown)}"
                   + $"/L{Bit(PlayerSettings.allowedAutorotateToLandscapeLeft)}"
                   + $"/R{Bit(PlayerSettings.allowedAutorotateToLandscapeRight)})\n"
                   + $"  기본 해상도    : {PlayerSettings.defaultScreenWidth}x{PlayerSettings.defaultScreenHeight}\n"
                   + $"  패키지         : {PlayerSettings.GetApplicationIdentifier(named)}\n"
                   + $"  스크립팅       : {PlayerSettings.GetScriptingBackend(named)} / {PlayerSettings.Android.targetArchitectures}\n"
                   + $"  그래픽 API     : {string.Join(", ", apis)}\n"
                   + $"  minSdk         : {PlayerSettings.Android.minSdkVersion}\n"
                   + $"  텍스처 압축    : {EditorUserBuildSettings.androidBuildSubtarget}\n"
                   + $"  스플래시       : {(PlayerSettings.SplashScreen.show ? "켜짐 (원본에 없다!)" : "꺼짐")}";
        }

        private static int Bit(bool value)
        {
            return value ? 1 : 0;
        }
    }
}
