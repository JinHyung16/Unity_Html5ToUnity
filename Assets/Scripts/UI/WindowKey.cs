using System;

namespace JinHyung.UI
{
    /// <summary>
    /// 창 식별자. <b>경로가 곧 식별자다.</b>
    ///
    /// <para>
    /// 창마다 <c>static readonly</c> 로 하나 선언해 두고 그것만 쓴다 —
    /// 경로 문자열이 호출부마다 흩어지면 오타가 런타임까지 살아남는다.
    /// </para>
    ///
    /// <code>
    /// public class ModeSelectWindow : BaseWindow
    /// {
    ///     public static readonly WindowKey&lt;ModeSelectWindow&gt; Key =
    ///         new WindowKey&lt;ModeSelectWindow&gt;("UI/Window/ModeSelectWindow");
    /// }
    /// </code>
    ///
    /// <para>
    /// 경로는 <b><c>Resources</c> 기준</b>이다 (확정표 10-c — UI 프리팹은 Resources,
    /// 그 밖의 아트는 Addressables). 어드레서블 키는 이름에 <c>~Address</c> 를 붙여 구분한다.
    /// </para>
    /// </summary>
    public sealed class WindowKey<T> : IWindowKey
        where T : BaseWindow
    {
        public string Path { get; }

        public Type TargetType
        {
            get { return typeof(T); }
        }

        public WindowKey(string path)
        {
            Path = path;
        }
    }
}
