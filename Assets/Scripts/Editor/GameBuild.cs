using System;
using System.IO;
using System.Linq;
using JinHyung.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// <b>게임 하나를 APK 로 굽는다.</b> 한 저장소에 게임이 여러 개인데
    /// <b>프로젝트 설정은 하나뿐</b>이라, 빌드 직전에 <b>그 게임의 확정표를 적용</b>하지 않으면
    /// <b>마지막에 만진 게임의 설정이 그대로 나간다.</b>
    ///
    /// <para>
    /// ⚠ <b>실제로 그렇게 나갔다</b> — Blumgi Bounce(가로 확정)가 세로로 빌드됐다.
    /// 원인은 확정표도 <see cref="PlatformSetup"/> 도 아니고 <b>「빌드가 그것을 안 불렀다」</b> 였다.
    /// </para>
    ///
    /// <para>
    /// 그래서 두 겹으로 막는다 —
    /// ① 여기 <b>게임별 진입점</b>이 설정·씬·빌드를 한 번에 한다.
    /// ② 사람이 에디터에서 손으로 빌드해도 <see cref="BuildPreprocess"/> 가 <b>씬을 보고</b> 설정을 맞춘다.
    /// </para>
    /// </summary>
    public static class GameBuild
    {
        /// <summary>구운 것이 쌓이는 자리. 저장소 밖이 아니라 루트 아래 <c>Build/</c> 다.</summary>
        private const string OutputRoot = "Build";

        // ────────────────────────────── 진입 (배치 실행 전용 · 인자 없는 static)

        public static void BuildCandyCrush() => Build(EGameProfile.CandyCrush);

        public static void BuildPacMan() => Build(EGameProfile.PacMan);

        public static void BuildBlumgiBounce() => Build(EGameProfile.BlumgiBounce);

        /// <summary>Undead Slayer 를 굽는다.</summary>
        public static void BuildUndeadSlayer() => Build(EGameProfile.UndeadSlayer);

        /// <summary>전 게임을 차례로 굽는다. 하나라도 실패하면 <b>거기서 멈춘다.</b></summary>
        public static void BuildAll()
        {
            foreach (EGameProfile id in Enum.GetValues(typeof(EGameProfile)))
                Build(id);
        }

        // ────────────────────────────── 본체

        public static void Build(EGameProfile id)
        {
            GameProfile profile = PlatformSetup.Get(id);

            // ① 그 게임의 확정표를 «먼저» 적용한다. 이 순서가 뒤집히면 이번 사고가 그대로 난다.
            PlatformSetup.Apply(profile);

            // ② 씬을 «그 게임 것만» 남긴다.
            //    다른 게임 씬이 섞이면 APK 가 부풀고, 첫 씬이 바뀌면 엉뚱한 게임이 뜬다.
            string scene = ResolveScene(profile);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene, true) };

            // ③ 굽기 전에 «되읽어» 확인한다 — 설정 파일 값이 아니라 실효값이다.
            AssertApplied(profile);

            string outDir = Path.Combine(OutputRoot, profile.AssetFolder);
            Directory.CreateDirectory(outDir);
            string outPath = Path.Combine(outDir, profile.PackageName + ".apk");

            Log.Success($"[GameBuild] {profile.ProductName} 굽기 시작\n"
                        + $"  방향   : {profile.Orientation} ({profile.DefaultWidth}×{profile.DefaultHeight})\n"
                        + $"  패키지 : {profile.PackageName}\n"
                        + $"  씬     : {scene}\n"
                        + $"  출력   : {outPath}");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = outPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"[GameBuild] {profile.ProductName} 빌드 실패 — {summary.result} "
                    + $"(오류 {summary.totalErrors}건)");
            }

            Log.Success($"[GameBuild] {profile.ProductName} 완료 — {outPath} "
                        + $"({summary.totalSize / (1024 * 1024)} MB · {summary.totalTime.TotalSeconds:F0}초)");
        }

        // ────────────────────────────── 확인

        /// <summary>
        /// 그 게임의 씬을 찾는다. <b>없으면 굽지 않고 멈춘다</b> —
        /// 씬이 없는 채로 구우면 <b>빈 APK 가 성공으로 나온다.</b>
        /// </summary>
        private static string ResolveScene(GameProfile profile)
        {
            string dir = $"Assets/{profile.AssetFolder}/Scenes";
            string[] found = AssetDatabase.FindAssets("t:Scene", new[] { dir })
                                          .Select(AssetDatabase.GUIDToAssetPath)
                                          .ToArray();

            if (found.Length == 0)
                throw new BuildFailedException($"[GameBuild] {dir} 에 씬이 없다. 굽지 않는다.");

            if (found.Length > 1)
            {
                throw new BuildFailedException(
                    $"[GameBuild] {dir} 에 씬이 {found.Length}개다 — 어느 것이 시작 씬인지 알 수 없다.\n"
                    + "  " + string.Join("\n  ", found));
            }

            return found[0];
        }

        /// <summary>
        /// 적용이 «실제로» 됐는지 엔진에 되물어 본다.
        /// 설정 파일에 적힌 값과 엔진이 쓰는 값은 다를 수 있다.
        /// </summary>
        private static void AssertApplied(GameProfile profile)
        {
            if (PlayerSettings.defaultInterfaceOrientation != profile.Orientation)
            {
                throw new BuildFailedException(
                    $"[GameBuild] 화면 방향이 확정표와 다르다 — "
                    + $"확정 {profile.Orientation} · 실제 {PlayerSettings.defaultInterfaceOrientation}");
            }

            string pkg = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (pkg != profile.PackageName)
            {
                throw new BuildFailedException(
                    $"[GameBuild] 패키지 이름이 확정표와 다르다 — "
                    + $"확정 {profile.PackageName} · 실제 {pkg}");
            }

            if (PlayerSettings.productName != profile.ProductName)
            {
                throw new BuildFailedException(
                    $"[GameBuild] 제품명이 확정표와 다르다 — "
                    + $"확정 {profile.ProductName} · 실제 {PlayerSettings.productName}");
            }
        }
    }
}
