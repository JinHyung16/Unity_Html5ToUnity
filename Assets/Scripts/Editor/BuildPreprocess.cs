using System;
using System.Linq;
using JinHyung.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// <b>사람이 에디터에서 손으로 빌드해도 그 게임의 확정표가 적용되게 한다.</b>
    ///
    /// <para>
    /// <see cref="GameBuild"/> 를 쓰면 설정이 맞지만, <b>사람은 Build 버튼을 누른다.</b>
    /// 그때 프로젝트 설정은 <b>마지막에 만진 게임 것</b>이라 방향·패키지가 그대로 나간다 —
    /// 실제로 가로 게임이 세로로 빌드됐다.
    /// </para>
    ///
    /// <para>
    /// <b>어느 게임인지는 「시작 씬」이 알려 준다.</b> 빌드에 들어가기 직전,
    /// 첫 번째로 켜진 씬의 경로에서 게임 폴더를 읽어 그 게임의 확정표를 적용한다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>조용히 고치지 않는다</b> — 무엇을 어떻게 바꿨는지 로그로 남긴다.
    /// 알 수 없는 씬이면 <b>고치지 않고 경고만</b> 한다. 임의로 정하는 것이 더 위험하다.
    /// </para>
    /// </summary>
    public sealed class BuildPreprocess : IPreprocessBuildWithReport
    {
        /// <summary>다른 전처리보다 먼저 돈다 — 설정을 맞춘 뒤에 나머지가 그것을 읽어야 한다.</summary>
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            EditorBuildSettingsScene first = EditorBuildSettings.scenes.FirstOrDefault(s => s.enabled);
            if (first == null)
            {
                Log.Warning("[BuildPreprocess] 켜진 씬이 없다. 확정표를 적용하지 않았다.");
                return;
            }

            if (!TryResolve(first.path, out GameProfile profile))
            {
                Log.Warning($"[BuildPreprocess] 시작 씬 `{first.path}` 이 어느 게임인지 알 수 없다.\n"
                            + "  확정표를 적용하지 않았다 — 임의로 정하지 않는다.\n"
                            + "  게임별로 구우려면 `GameBuild.Build<게임명>()` 을 쓴다.");
                return;
            }

            UIOrientation before = PlayerSettings.defaultInterfaceOrientation;
            string beforePkg = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);

            PlatformSetup.Apply(profile);

            bool changed = before != profile.Orientation || beforePkg != profile.PackageName;
            string head = changed
                ? $"[BuildPreprocess] ★ 설정을 «바꿨다» — 시작 씬이 {profile.ProductName} 이다"
                : $"[BuildPreprocess] {profile.ProductName} 확정표 적용 (이미 맞아 있었다)";

            Log.Success($"{head}\n"
                        + $"  방향   : {before} → {profile.Orientation}\n"
                        + $"  패키지 : {beforePkg} → {profile.PackageName}\n"
                        + $"  해상도 : {profile.DefaultWidth}×{profile.DefaultHeight}");

            // 되읽어 확인한다 — 적용했다고 믿지 않는다.
            if (PlayerSettings.defaultInterfaceOrientation != profile.Orientation)
            {
                throw new BuildFailedException(
                    $"[BuildPreprocess] 방향이 안 들어갔다 — "
                    + $"확정 {profile.Orientation} · 실제 {PlayerSettings.defaultInterfaceOrientation}");
            }
        }

        /// <summary>
        /// 씬 경로에서 게임을 가른다 — <c>Assets/&lt;게임폴더&gt;/Scenes/….unity</c>.
        /// <b>폴더 이름이 곧 게임 이름</b>이라 확정표의 <c>AssetFolder</c> 와 맞대면 된다.
        /// </summary>
        private static bool TryResolve(string scenePath, out GameProfile profile)
        {
            profile = null;
            if (string.IsNullOrEmpty(scenePath))
                return false;

            foreach (EGameProfile id in Enum.GetValues(typeof(EGameProfile)))
            {
                GameProfile candidate = PlatformSetup.Get(id);
                string prefix = $"Assets/{candidate.AssetFolder}/";
                if (scenePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    profile = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
