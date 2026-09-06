using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// <b>열려 있는 에디터에게 밖에서 일을 시키는 다리.</b>
    ///
    /// <para>
    /// 배치모드가 필요했던 유일한 이유는 «같은 프로젝트를 두 인스턴스가 못 연다»는 것이다.
    /// 그래서 검사 한 번마다 에디터를 <b>닫고 · 돌리고 · 다시 열었다</b> — 사람이 보던 화면이
    /// 매번 사라졌다. 이 다리는 두 번째 인스턴스를 띄우는 대신 <b>이미 열려 있는 에디터가
    /// 직접 실행</b>하게 한다. 에디터를 끌 일이 없어진다.
    /// </para>
    ///
    /// <para>
    /// <b>동작</b> — 셸이 <c>Library/EditorBridge/request.json</c> 에
    /// <c>{"id":"…","method":"Ns.Class.Method"}</c> 를 쓰면, 에디터가 틱마다 보다가
    /// ① 에셋을 새로고침(밖에서 고친 스크립트를 먹는다) ② 컴파일이 끝나길 기다렸다가
    /// ③ 메서드를 실행하고 ④ 잡은 로그와 함께 <c>response-<id>.json</c> 을 쓴다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>재생으로 들어가는 검사</b>는 응답을 «재생이 끝난 뒤» 쓴다. 재생 검사기는
    /// <see cref="IsDriving"/> 이 참이면 <c>EditorApplication.Exit</c> 대신
    /// <b><c>ExitPlaymode</c></b> 를 불러야 한다 — Exit 를 부르면 사람이 쓰던 에디터가 꺼진다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 요청 파일은 <c>Library/</c> 에 둔다 — <c>Temp/</c> 는 유니티가 시작할 때 비운다.
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class EditorCommandBridge
    {
        private const string Dir = "Library/EditorBridge";
        private const string RequestPath = Dir + "/request.json";
        private const string RefreshMarker = Dir + "/refreshed";
        private const string DrivingKey = "JinHyung.EditorBridge.Driving";
        private const string PendingIdKey = "JinHyung.EditorBridge.PendingId";

        /// <summary>
        /// 잡은 로그를 «파일에도» 쌓는다 — 재생으로 들어가면 도메인 리로드가 정적 버퍼를 비운다.
        /// 재생 검사의 판정은 로그가 전부이므로, 리로드를 넘어 살아남는 곳은 파일뿐이다.
        /// </summary>
        private const string CapturePath = Dir + "/capture.log";

        private const string SawCompileKey = "JinHyung.EditorBridge.SawCompile";

        /// <summary>새로고침 뒤 컴파일이 «시작되기까지» 기다리는 틱 수 — 새 스크립트가 있어도 컴파일은 몇 프레임 뒤에 시작된다.</summary>
        private const int CompileGraceTicks = 150;

        private static int _ticksSinceRefresh;

        private static readonly StringBuilder _capturedLog = new StringBuilder();
        private static bool _capturing;

        /// <summary>
        /// 다리가 시킨 실행 중인가 — <b>편집 모드 실행 동안에도 참</b>이고, 재생 검사는 재생이 끝날 때까지 참이다.
        /// 검사기는 이 값(SessionState <c>JinHyung.EditorBridge.Driving</c>)이 참이면 <b>에디터를 끄지 않는다</b>.
        /// <para>⚠ 재생 검사만 참으로 두면 편집 모드 검사기가 끝에서 <c>Exit</c> 를 불러 사람 에디터가 꺼진다 (재발방지 #140 의 두 번째 사고).</para>
        /// </summary>
        public static bool IsDriving
        {
            get { return SessionState.GetBool(DrivingKey, false); }
        }

        static EditorCommandBridge()
        {
            EditorApplication.update += Tick;
            UnityEditor.Compilation.CompilationPipeline.compilationStarted += _ => SessionState.SetBool(SawCompileKey, true);

            // 재생 검사 중 도메인 리로드가 났다 — 캡처를 다시 건다 (파일이 이어 받는다)
            if (IsDriving)
                StartCapture(false);
        }

        private static void Tick()
        {
            // ── 재생 검사의 마무리: 재생이 끝났고 다리가 시킨 것이면 응답을 쓴다.
            if (IsDriving && EditorApplication.isPlayingOrWillChangePlaymode == false)
            {
                string pendingId = SessionState.GetString(PendingIdKey, string.Empty);

                if (string.IsNullOrEmpty(pendingId) == false)
                {
                    SessionState.SetBool(DrivingKey, false);
                    SessionState.SetString(PendingIdKey, string.Empty);
                    StopCapture();

                    // 재생 검사의 판정은 검사기가 남긴 로그가 전부다 — 리로드를 넘어 파일에 쌓인 로그를 넘긴다.
                    WriteResponse(pendingId, true, ReadCapture());
                }
            }

            if (File.Exists(RequestPath) == false)
                return;

            // ① 밖에서 고친 스크립트·에셋을 먼저 먹는다. 요청당 한 번만.
            if (File.Exists(RefreshMarker) == false)
            {
                File.WriteAllText(RefreshMarker, DateTime.Now.ToString("HH:mm:ss"));
                SessionState.SetBool(SawCompileKey, false);
                _ticksSinceRefresh = 0;
                AssetDatabase.Refresh();
                return;
            }

            // ② 컴파일·임포트가 끝날 때까지 기다린다. 도메인 리로드가 나도
            //    요청 파일이 남아 있으므로 리로드 뒤 이 틱이 다시 이어받는다.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                SessionState.SetBool(SawCompileKey, true);
                return;
            }

            // ⚠ 새로고침 직후엔 «아직 컴파일이 시작 안 된» 틱이 있다 — 그때 실행하면 방금 들여온
            //   검사기를 «메서드를 못 찾았다»로 놓친다. 컴파일을 한 번도 못 봤으면 잠깐 기다린다.
            //   (도메인 리로드가 났으면 정적 카운터는 0 이지만 SessionState 가 «봤다»를 들고 있다)
            if (SessionState.GetBool(SawCompileKey, false) == false && _ticksSinceRefresh++ < CompileGraceTicks)
                return;

            string id;
            string method;

            try
            {
                string json = File.ReadAllText(RequestPath);
                id = Extract(json, "id");
                method = Extract(json, "method");
            }
            catch (Exception e)
            {
                File.Delete(RequestPath);
                File.Delete(RefreshMarker);
                Debug.LogError($"[EditorBridge] 요청을 못 읽었다: {e.Message}");
                return;
            }

            File.Delete(RequestPath);
            File.Delete(RefreshMarker);

            Execute(id, method);
        }

        private static void Execute(string id, string method)
        {
            StartCapture();
            SessionState.SetBool(DrivingKey, true);   // 실행 동안 «다리가 시켰다» — 검사기가 Exit 를 안 부르게

            bool ok;
            string error = string.Empty;

            try
            {
                MethodInfo target = FindMethod(method);

                if (target == null)
                    throw new MissingMethodException($"메서드를 못 찾았다: {method}");

                target.Invoke(null, null);
                ok = true;
            }
            catch (Exception e)
            {
                ok = false;
                error = (e.InnerException ?? e).ToString();
            }

            // ── 재생으로 들어갔으면 응답을 미룬다 — 재생이 끝나야 결과가 있다.
            if (ok && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetString(PendingIdKey, id);
                return;   // 캡처는 계속 돈다 — 재생 로그까지 담는다 (Driving 은 재생이 끝날 때 내린다)
            }

            SessionState.SetBool(DrivingKey, false);
            StopCapture();

            string log = ReadCapture();

            if (ok == false)
                log += "\n" + error;

            WriteResponse(id, ok, log);
        }

        private static MethodInfo FindMethod(string fullName)
        {
            int split = fullName.LastIndexOf('.');

            if (split <= 0)
                return null;

            string typeName = fullName.Substring(0, split);
            string methodName = fullName.Substring(split + 1);

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(typeName);

                if (type == null)
                    continue;

                MethodInfo method = type.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                if (method != null)
                    return method;
            }

            return null;
        }

        // ────────────────────────────── 로그 캡처

        private static void StartCapture()
        {
            StartCapture(true);
        }

        private static void StartCapture(bool fresh)
        {
            _capturedLog.Length = 0;

            if (fresh)
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(CapturePath, string.Empty);
            }

            if (_capturing)
                return;

            _capturing = true;
            Application.logMessageReceived += OnLog;
        }

        private static string ReadCapture()
        {
            try
            {
                return File.Exists(CapturePath) ? File.ReadAllText(CapturePath) : _capturedLog.ToString();
            }
            catch (Exception)
            {
                return _capturedLog.ToString();
            }
        }

        private static void StopCapture()
        {
            if (_capturing == false)
                return;

            _capturing = false;
            Application.logMessageReceived -= OnLog;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            _capturedLog.AppendLine(condition);

            try
            {
                File.AppendAllText(CapturePath, condition + Environment.NewLine);
            }
            catch (Exception)
            {
                // 파일이 잠긴 순간은 버린다 — 정적 버퍼가 같은 줄을 들고 있다
            }
        }

        // ────────────────────────────── 응답

        private static void WriteResponse(string id, bool ok, string log)
        {
            Directory.CreateDirectory(Dir);

            // 로그는 별도 파일로 — JSON 이스케이프 지옥을 피한다.
            File.WriteAllText($"{Dir}/response-{id}.log", log ?? string.Empty);
            File.WriteAllText($"{Dir}/response-{id}.json",
                              "{ \"ok\": " + (ok ? "true" : "false") + " }");
        }

        /// <summary>따옴표로 감싼 단순 값 하나를 뽑는다 — 요청 형식이 {"id":"..","method":".."} 로 고정이라 충분하다.</summary>
        private static string Extract(string json, string key)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                json, "\"" + key + "\"\\s*:\\s*\"([^\"]+)\"");

            return match.Success ? match.Groups[1].Value : string.Empty;
        }
    }
}
