using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 야자수 한 그루.
    ///
    /// <para>
    /// ★ <b>줄기와 잎이 «별개 오브젝트»다</b> [런타임 실측] — 줄기(<c>Tlb_PalmTrunk</c>)는
    /// <b>화면 아래로 길게 늘어나는 타일드 배경</b>(표시 폭 75 · 높이 2849~3940)이고
    /// 잎(<c>spr_PalmTree</c>)만 스프라이트다. <b>잎만 만들면 안 된다.</b>
    /// </para>
    ///
    /// <para>
    /// ⚠ 잎은 소스 <b>179×96</b> 인데 표시가 <b>434×233</b> (2.43배)다 [UIUX 2-g] —
    /// 확대 배율은 <see cref="BlumgiUnits.DisplayScale"/> 로 빌더가 건다.
    /// </para>
    /// </summary>
    public sealed class BlumgiPalmView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _leaf;
        [SerializeField] private SpriteRenderer _trunk;

        public void SetColor(Color color)
        {
            if (_leaf != null)
                _leaf.color = color;

            if (_trunk != null)
                _trunk.color = color;
        }
    }
}
