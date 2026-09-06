using System;
using JinHyung.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace JinHyung.EditorTools
{
    /// <summary>어느 게임의 확정표를 적용하는가.</summary>
    public enum EGameProfile
    {
        CandyCrush,
        PacMan,
        BlumgiBounce,
        UndeadSlayer
    }

    /// <summary>
    /// <b>한 게임의 확정표 중 「프로젝트 설정으로 가는 행」만 담은 묶음.</b>
    ///
    /// <para>
    /// 값은 전부 그 게임 원장의 <b>확정표에서 온다.</b> 임의로 정하지 않는다.
    /// 자동 회전 4항목을 <b>파생시키지 않고 그대로 적는다</b> — 파생 규칙을 두면
    /// 「확정표에 뭐라고 적혀 있었나」가 코드에서 안 읽힌다.
    /// </para>
    /// </summary>
    public sealed class GameProfile
    {
        public GameProfile(EGameProfile id,
                           string assetFolder,
                           string productName,
                           string packageName,
                           UIOrientation orientation,
                           bool autorotatePortrait,
                           bool autorotatePortraitUpsideDown,
                           bool autorotateLandscapeLeft,
                           bool autorotateLandscapeRight,
                           int defaultWidth,
                           int defaultHeight)
        {
            Id = id;
            AssetFolder = assetFolder;
            ProductName = productName;
            PackageName = packageName;
            Orientation = orientation;
            AutorotatePortrait = autorotatePortrait;
            AutorotatePortraitUpsideDown = autorotatePortraitUpsideDown;
            AutorotateLandscapeLeft = autorotateLandscapeLeft;
            AutorotateLandscapeRight = autorotateLandscapeRight;
            DefaultWidth = defaultWidth;
            DefaultHeight = defaultHeight;
        }

        public EGameProfile Id { get; }

        /// <summary>`Assets/&lt;게임명&gt;/` — 번호를 뗀 게임 폴더 이름.</summary>
        public string AssetFolder { get; }

        /// <summary>확정표 — 제품명.</summary>
        public string ProductName { get; }

        /// <summary>확정표 — 패키지 이름.</summary>
        public string PackageName { get; }

        /// <summary>확정표 2 — 화면 방향(기본 방향).</summary>
        public UIOrientation Orientation { get; }

        /// <summary>확정표 2 — 자동 회전 4항목. <b>기본 방향만 바꾸고 이걸 안 맞추면 확정이 안 지켜진다.</b></summary>
        public bool AutorotatePortrait { get; }

        public bool AutorotatePortraitUpsideDown { get; }

        public bool AutorotateLandscapeLeft { get; }

        public bool AutorotateLandscapeRight { get; }

        /// <summary>확정표 3 — 화면 비율. 에디터 Game 뷰 기본 크기와 맞춘다.</summary>
        public int DefaultWidth { get; }

        public int DefaultHeight { get; }
    }

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
    /// ★★ <b>이 저장소는 게임을 여러 개 이관하는데 PlayerSettings 는 프로젝트에 하나뿐이다.</b>
    /// 그래서 값을 하드코딩하지 않고 <b>게임별 프로필</b>로 갖고 <b>활성 게임 하나를 적용</b>한다.
    /// 세 게임의 확정값이 전부 코드에 남으므로 <b>먼저 이관한 게임의 설정도 언제든 재현된다.</b>
    /// (원장 「착수 리스크 — PlatformSetup 이 이 게임의 확정을 담을 수 없다」 A 안)
    /// </para>
    ///
    /// <para>
    /// <b>어떻게 고르는가</b> — 두 갈래다. 둘 다 <b>인자 없는 static 메서드</b>라 배치에서 바로 돈다.
    /// <list type="bullet">
    /// <item><b>게임별 진입</b> — <c>SetupCandyCrush()</c> · <c>SetupPacMan()</c> · <c>SetupBlumgiBounce()</c>.
    /// 어느 게임을 적용하는지가 <b>실행 명령에 그대로 적힌다.</b> 이쪽이 기본이다.</item>
    /// <item><b>기본 진입</b> — <c>Setup()</c> 은 <see cref="ActiveGame"/> 에 선언된
    /// <b>「지금 이관 중인 게임」</b>을 적용한다. 게임을 갈아탈 때 <b>그 한 줄만</b> 고친다 —
    /// 무엇이 활성인지가 git diff 에 남는다.</item>
    /// </list>
    /// ⚠ 사람이 에디터 메뉴를 누르게 하지 않는다 — <c>[MenuItem]</c> 을 두지 않는 이유다.
    /// </para>
    /// </summary>
    public static class PlatformSetup
    {
        // ── 활성 게임 ───────────────────────────────────────────
        /// <summary>
        /// <b>지금 이관 중인 게임.</b> <see cref="Setup"/> 이 이걸 적용한다.
        /// 다음 게임에 착수하면 이 한 줄을 고친다.
        /// </summary>
        public const EGameProfile ActiveGame = EGameProfile.BlumgiBounce;

        // ── 전 게임 공통 (저장소 규약 이월) ─────────────────────
        private const string CompanyName = "JinHyung";

        /// <summary>확정표 1·1-b — 모바일 / Android.</summary>
        private const BuildTarget TargetPlatform = BuildTarget.Android;
        private const BuildTargetGroup TargetGroup = BuildTargetGroup.Android;

        /// <summary>확정표 6 — Linear 유지.</summary>
        private const ColorSpace Space = ColorSpace.Linear;

        // ── 게임별 확정표 ───────────────────────────────────────

        /// <summary>
        /// 원장 `HtmlToUnity_작업내역_Candy-Crush-Game.md` 확정표 2·3 — Portrait 고정 · 1080×1920.
        /// 원본이 세로 재배치 전제라 <b>가로 자동 회전을 명시적으로 끈다.</b>
        /// </summary>
        private static readonly GameProfile CandyCrush = new GameProfile(
            EGameProfile.CandyCrush,
            "Candy-Crush-Game",
            "Candy Crush",
            "com.jinhyung.candycrush",
            UIOrientation.Portrait,
            autorotatePortrait: true,
            autorotatePortraitUpsideDown: false,
            autorotateLandscapeLeft: false,
            autorotateLandscapeRight: false,
            defaultWidth: 1080,
            defaultHeight: 1920);

        /// <summary>
        /// 원장 `HtmlToUnity_작업내역_Pac-Man-Game.md` 확정표 2·3 —
        /// <b>Portrait 고정 · 1080×1920</b> (미로가 28×31 세로형).
        /// ⚠ 그 원장에 제품명·패키지 행이 <b>없다</b> — 아래 두 값은 저장소 관례를 따른 것이다.
        /// 원장에 확정이 올라오면 그 값으로 바꾼다.
        /// </summary>
        private static readonly GameProfile PacMan = new GameProfile(
            EGameProfile.PacMan,
            "Pac-Man-Game",
            "Pac Man",
            "com.jinhyung.pacman",
            UIOrientation.Portrait,
            autorotatePortrait: true,
            autorotatePortraitUpsideDown: false,
            autorotateLandscapeLeft: false,
            autorotateLandscapeRight: false,
            defaultWidth: 1080,
            defaultHeight: 1920);

        /// <summary>
        /// 원장 `HtmlToUnity_작업내역_Blumgi-Bounce.md` 확정표 2·3 —
        /// <b>Landscape 고정 · 1920×1080</b> (원본이 가로형 레벨 구성 [실측]).
        /// <para>
        /// ⚠ <b>자동 회전 4항목을 짝으로 뒤집었다</b> — 세로 둘을 끄고 가로 둘을 켠다.
        /// 「Landscape 고정」은 <b>세로로 안 돈다</b>는 뜻이라 가로 양쪽(Left/Right)은 켜 둔다 —
        /// 둘은 레이아웃이 같아 원본과 갈리지 않는다. 세로가 켜져 있으면 그때 원본에 없는 화면이 나온다.
        /// </para>
        /// </summary>
        private static readonly GameProfile BlumgiBounce = new GameProfile(
            EGameProfile.BlumgiBounce,
            "Blumgi-Bounce",
            "Blumgi Bounce",
            "com.jinhyung.blumgibounce",
            UIOrientation.LandscapeLeft,
            autorotatePortrait: false,
            autorotatePortraitUpsideDown: false,
            autorotateLandscapeLeft: true,
            autorotateLandscapeRight: true,
            defaultWidth: 1920,
            defaultHeight: 1080);

        /// <summary>
        /// 원장 `HtmlToUnity_작업내역_Undead-Slayer.md` 확정표 2·3 —
        /// <b>Landscape 고정 · 1920×1080</b>.
        /// <para>
        /// 근거는 <b>원본 실측</b>이다 — 원본(PixiJS)은 <b>세로 580 을 고정</b>하고 가로는 화면비만큼
        /// 늘린다(루트 컨테이너 scale = 렌더러높이 / 580, 검산 2건 일치 · `04_UIUX규칙.md`).
        /// 세로가 불변식이므로 <b>Canvas 는 Match = 1(Height)</b> 이고, 게임 카메라는 직교 크기를
        /// 세로로 고정한다.
        /// </para>
        /// <para>
        /// 자동 회전은 Blumgi 와 같은 짝 — 세로 둘을 끄고 가로 둘을 켠다.
        /// </para>
        /// </summary>
        private static readonly GameProfile UndeadSlayer = new GameProfile(
            EGameProfile.UndeadSlayer,
            "Undead-Slayer",
            "Undead Slayer",
            "com.jinhyung.undeadslayer",
            UIOrientation.LandscapeLeft,
            autorotatePortrait: false,
            autorotatePortraitUpsideDown: false,
            autorotateLandscapeLeft: true,
            autorotateLandscapeRight: true,
            defaultWidth: 1920,
            defaultHeight: 1080);

        /// <summary>
        /// 확정표를 담은 게임 전부. <b>배선 검증이 여기서 기대값을 읽는다</b> —
        /// 검사 쪽에 값을 다시 적으면 두 벌이 갈린다.
        /// </summary>
        public static GameProfile Get(EGameProfile game)
        {
            switch (game)
            {
                case EGameProfile.CandyCrush: return CandyCrush;
                case EGameProfile.PacMan: return PacMan;
                case EGameProfile.BlumgiBounce: return BlumgiBounce;
                case EGameProfile.UndeadSlayer: return UndeadSlayer;
                default: throw new ArgumentOutOfRangeException(nameof(game), game, "프로필이 없는 게임이다.");
            }
        }

        // ────────────────────────────── 진입 (배치 실행 전용 · 인자 없는 static)

        /// <summary>
        /// <b>기본 진입 — <see cref="ActiveGame"/> 프로필을 적용한다.</b>
        /// 지금은 Blumgi Bounce 다. 어느 게임인지 명시하고 싶으면 게임별 진입을 쓴다.
        /// </summary>
        public static void Setup()
        {
            Apply(Get(ActiveGame));
        }

        /// <summary>Candy Crush 확정표를 적용한다.</summary>
        public static void SetupCandyCrush()
        {
            Apply(CandyCrush);
        }

        /// <summary>Pac-Man 확정표를 적용한다.</summary>
        public static void SetupPacMan()
        {
            Apply(PacMan);
        }

        /// <summary>Blumgi Bounce 확정표를 적용한다.</summary>
        public static void SetupBlumgiBounce()
        {
            Apply(BlumgiBounce);
        }

        /// <summary>Undead Slayer 확정표를 적용한다.</summary>
        public static void SetupUndeadSlayer()
        {
            Apply(UndeadSlayer);
        }

        // ────────────────────────────── 적용

        public static void Apply(GameProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            ApplyCommon(profile);
            ApplyAndroid(profile);
            ApplyStandalone();

            AssetDatabase.SaveAssets();
            SwitchTarget();

            Log.Success(BuildReport(profile));
        }

        private static void ApplyCommon(GameProfile profile)
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = profile.ProductName;

            // 확정표 6 — 색공간. 바꾸면 전 텍스처가 재임포트된다.
            if (PlayerSettings.colorSpace != Space)
                PlayerSettings.colorSpace = Space;

            // 확정표 2 — 방향 «고정». 기본 방향만 바꾸고 자동 회전을 안 맞추면
            // 기기를 돌렸을 때 원본에 없는 레이아웃이 나온다.
            // ⚠ 4항목은 프로필이 그대로 갖고 있다 — 여기서 파생시키지 않는다.
            PlayerSettings.defaultInterfaceOrientation = profile.Orientation;
            PlayerSettings.allowedAutorotateToPortrait = profile.AutorotatePortrait;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = profile.AutorotatePortraitUpsideDown;
            PlayerSettings.allowedAutorotateToLandscapeLeft = profile.AutorotateLandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeRight = profile.AutorotateLandscapeRight;

            PlayerSettings.defaultScreenWidth = profile.DefaultWidth;
            PlayerSettings.defaultScreenHeight = profile.DefaultHeight;

            // 원본에 스플래시가 없다. 켜 두면 «원본에 없는 화면»이 한 겹 붙는다.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
        }

        private static void ApplyAndroid(GameProfile profile)
        {
            var named = NamedBuildTarget.Android;

            PlayerSettings.SetApplicationIdentifier(named, profile.PackageName);

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

        private static string BuildReport(GameProfile profile)
        {
            var named = NamedBuildTarget.Android;
            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);

            return $"플랫폼 설정 반영 — 게임 프로필 [{profile.Id}] ({profile.ProductName})\n"
                   + $"  적용 프로필    : {profile.Id} · Assets/{profile.AssetFolder}/ "
                   + $"(기본 진입 Setup() 의 활성 게임 = {ActiveGame})\n"
                   + $"  제품명         : {PlayerSettings.productName}\n"
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
