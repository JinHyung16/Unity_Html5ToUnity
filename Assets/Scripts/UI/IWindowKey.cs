using System;

namespace JinHyung.UI
{
    /// <summary>
    /// 창 식별자의 비제네릭 얼굴. 창을 <b>종류에 상관없이 한 자료구조에 담기 위해</b> 있다.
    /// </summary>
    public interface IWindowKey
    {
        /// <summary><b>경로가 곧 식별자다.</b> 프리팹 로드 경로와 같은 값이다.</summary>
        string Path { get; }

        /// <summary>이 키가 가리키는 창 타입.</summary>
        Type TargetType { get; }
    }
}
