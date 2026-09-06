using JinHyung.Core;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 열려 있는 에디터를 <b>저장하고 정상 종료</b>한다 — 러너(<c>Tools/unity-batch.sh</c>)가 다리로 부른다.
    ///
    /// <para>
    /// ⚠ <c>taskkill</c> 로 끄면 다음에 열 때 <b>「_recovery backup scene?」</b> 대화상자가 떠서
    /// 사람이 눌러야 한다. 씬·에셋을 저장하고 <c>EditorApplication.Exit</c> 로 나가면 그 대화상자가 안 뜬다.
    /// 그래서 러너는 «먼저» 이 메서드를 시키고, 응답이 없을 때만 강제 종료로 물러난다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 재생 중이면 <b>재생을 먼저 끝내고</b>(재생 중엔 씬을 저장할 수 없다) 다음 틱들에서 저장·종료한다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 이관 중 에디터를 끄는 것 자체가 «예외»다 — 이 파일은 그래도 꺼야 할 때(빌드·배치가 필수인 재생 검사)
    /// «곱게» 끄기 위한 것이다.
    /// </para>
    /// </summary>
    public static class EditorShutdown
    {
        private static bool _pending;

        public static void SaveAndExit()
        {
            if (_pending)
                return;

            _pending = true;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.ExitPlaymode();

            EditorApplication.update += TryFinish;
        }

        private static void TryFinish()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            EditorApplication.update -= TryFinish;

            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Log.Success("에디터 저장 완료 — 정상 종료한다 (복구 대화상자가 뜨지 않는다)");

            // ⚠ delayCall 로 미루면 «재생 종료 뒤 도메인 리로드»에 콜백이 사라져 영영 안 꺼진다 — 바로 나간다.
            //   러너는 응답 파일이 아니라 «프로세스가 끝났는가»를 본다.
            EditorApplication.Exit(0);
        }
    }
}
