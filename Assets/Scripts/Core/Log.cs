namespace JinHyung.Core
{
    /// <summary>
    /// 에디터 전용 로그.
    ///
    /// <para>
    /// 세 메서드 전부 <c>[Conditional("UNITY_EDITOR")]</c> 이 걸려 있다.
    /// 빌드에서는 <b>호출부가 C# 컴파일러에 의해 통째로 제거</b>되므로,
    /// 지우는 것을 깜빡한 로그가 남아 있어도 빌드에 들어가지 않는다.
    /// </para>
    ///
    /// <para>
    /// 인자까지 같이 사라진다 — <c>Log.Error($"{무거운계산()}")</c> 를 써도
    /// 빌드에서는 <c>무거운계산()</c> 자체가 호출되지 않는다.
    /// 그래서 <c>Editor/</c> 폴더에 두는 것보다 낫다
    /// (Editor 폴더에 두면 게임 코드에서 아예 못 부른다).
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>빌드에서 남아야 하는 로그에는 쓰지 않는다.</b>
    /// 출시 빌드에서 확인해야 하는 오류(데이터 검증 실패 등)는
    /// 이 클래스가 아니라 별도 경로로 남긴다.
    /// </para>
    /// </summary>
    public static class Log
    {
        /// <summary>이 심볼이 없는 빌드에서는 호출부가 제거된다.</summary>
        private const string EditorOnly = "UNITY_EDITOR";

        /// <summary>성공 로그의 글자색. 유니티 콘솔의 리치 텍스트로 칠한다.</summary>
        private const string SuccessColor = "#3DDC84";

        /// <summary>성공을 알린다. 유니티 콘솔에 초록색 일반 로그로 찍힌다.</summary>
        [System.Diagnostics.Conditional(EditorOnly)]
        public static void Success(object message, UnityEngine.Object context = null)
        {
            UnityEngine.Debug.Log(Colorize(message, SuccessColor), context);
        }

        /// <summary>경고를 알린다. 콘솔의 경고 필터에 잡힌다.</summary>
        [System.Diagnostics.Conditional(EditorOnly)]
        public static void Warning(object message, UnityEngine.Object context = null)
        {
            UnityEngine.Debug.LogWarning(message, context);
        }

        /// <summary>오류를 알린다. 콘솔의 오류 필터에 잡힌다.</summary>
        [System.Diagnostics.Conditional(EditorOnly)]
        public static void Error(object message, UnityEngine.Object context = null)
        {
            UnityEngine.Debug.LogError(message, context);
        }

        /// <summary>리치 텍스트 색 태그로 감싼다.</summary>
        private static string Colorize(object message, string hexColor)
        {
            return string.Concat("<color=", hexColor, ">", message, "</color>");
        }
    }
}
