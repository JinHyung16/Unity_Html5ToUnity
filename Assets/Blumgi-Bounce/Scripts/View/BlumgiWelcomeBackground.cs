using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// WELCOME 화면의 <c>BG</c> 장식 묶음 — 격자 1 · 야자수 잎 2 · 기둥 2.
    ///
    /// <para>
    /// 좌표 · 크기 · 순서는 <see cref="BlumgiWelcomeBackgroundLayout"/> 의 실측표를 읽어
    /// <b>빌더가 굽는다</b> (확정표 10-b). 여기는 <b>«색을 갈아 끼울 자리»</b>만 든다 —
    /// 원본이 <b>순백 마스크에 런타임 틴트</b>를 입히는 구조라(재발방지 #94)
    /// 색은 코드가 넣는 것이 «기전을 옮긴 것»이다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>인게임 <see cref="BlumgiLevelBackground"/> 와 «다른 것»이다.</b> 개수 · 좌표 · 틴트가 전부 다르다.
    /// 웰컴 값을 인게임에 흘리지 않으려고 클래스를 나눠 뒀다.
    /// </para>
    /// </summary>
    public sealed class BlumgiWelcomeBackground : MonoBehaviour
    {
        [Header("격자 — 흰색 마스크 · 불투명도 0.5 [실측]")]
        [SerializeField] private Image _grid;

        [Header("야자수 — 잎 2 · 기둥 2 · 틴트 (58,177,228) [실측]")]
        [SerializeField] private Image[] _palms;

        /// <summary>격자 색. 원본은 <b>흰색 · op 0.5</b> 하나뿐이라 프리팹 값이 곧 정답이다.</summary>
        public void SetGridColor(Color color)
        {
            if (_grid != null)
                _grid.color = color;
        }

        /// <summary>야자수(잎 + 기둥) 색. 원본 4개가 <b>전부 같은 틴트</b>다 [실측].</summary>
        public void SetPalmColor(Color color)
        {
            if (_palms == null)
                return;

            for (int i = 0; i < _palms.Length; i++)
            {
                if (_palms[i] != null)
                    _palms[i].color = color;
            }
        }
    }
}
