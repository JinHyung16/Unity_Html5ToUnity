using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 레벨 배경 — 단색 판 + 격자 + 야자수.
    ///
    /// <para>
    /// ★ <b>야자수·격자의 world 좌표는 5레벨 전부 «완전히 동일»하다</b> [UIUX §6 실측] ⇒
    /// <b>레벨 데이터가 아니라 공용 배경</b>이다. 색만 레벨 테마를 따라간다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>격자선 색을 배경색에서 파생시키지 않는다</b> — W1L5 만 색조가 돌아(<c>#FF7452</c> → <c>#FD9651</c>)
    /// 「밝기만 올린 값」으로 만들면 그 레벨에서 틀어진다 [05_연출 §1]. <b>두 색을 다 데이터로 받는다.</b>
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>야자수는 테마를 따라가지 않는다</b> — 5레벨 전부 <c>#80E5FF</c> 다 [실측]. 그래서 별도 인자다.
    /// </para>
    ///
    /// <para>
    /// 배경 격자는 <b>스크롤·드리프트가 없다</b> [실측 — 정지 캡처 간 위상 변화 0].
    /// </para>
    /// </summary>
    public sealed class BlumgiLevelBackground : MonoBehaviour
    {
        [Header("판")]
        [SerializeField] private SpriteRenderer _solid;
        [SerializeField] private SpriteRenderer _grid;

        [Header("야자수 — world 좌표가 전 레벨 동일이라 프리팹이 든다")]
        [SerializeField] private BlumgiPalmView[] _palms;

        /// <summary>테마 2색. 전부 원본 sRGB hex 그대로다.</summary>
        public void SetTheme(Color background, Color gridLine)
        {
            if (_solid != null)
                _solid.color = background;

            if (_grid != null)
                _grid.color = gridLine;
        }

        /// <summary>야자수 색. 레벨을 넘어 같은 값이라 <b>테마와 따로</b> 받는다.</summary>
        public void SetPalmColor(Color palm)
        {
            if (_palms == null)
                return;

            for (int i = 0; i < _palms.Length; i++)
            {
                if (_palms[i] != null)
                    _palms[i].SetColor(palm);
            }
        }
    }
}
