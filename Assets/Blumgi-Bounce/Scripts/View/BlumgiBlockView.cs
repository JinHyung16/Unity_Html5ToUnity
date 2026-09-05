using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 블록 한 칸의 <b>그림</b>. ⚠ <b>충돌이 아니다</b> —
    /// 확정 D 가 「충돌(격자 50×50)과 그림(스프라이트 84×58.983)을 분리」로 못박았고,
    /// 런타임 <c>IsVisible()</c> 이 <c>spr_BlockSimple</c>(충돌)에 <c>false</c> 를 돌려줘 확증됐다 [UIUX 2-g].
    ///
    /// <para>
    /// ★★★ <b>[정정 · 17회차] 블록은 «틴트»가 아니다.</b> 예전에는 확정 F(흰색으로 굽고 런타임 tint)를
    /// 블록에도 적용해 <b>몸통 · 무늬 · 좌캡 세 장</b>을 겹쳐 색을 넣었다. 17회차가
    /// <b>5레벨 462 인스턴스의 인스턴스 색을 전부 (255,255,255) = «틴트 없음»</b> 으로 실측했고,
    /// <c>_currentAnimation._frames</c> 를 통째로 떠서 <b>스킨이 «한 장짜리 5프레임»</b>이며
    /// <b>색이 그림에 구워져 있다</b>는 것을 닫았다. 그래서 여기는 <b>프레임 번호만 고른다</b>.
    /// </para>
    ///
    /// <para>
    /// 레벨↔프레임 [실측 462 인스턴스 · <b>예외 0건</b>] — <c>L1→0 · L2→3 · L3→1 · L4→2 · L5→4</c>.
    /// 애니 <c>speed = 0</c> 이라 <b>자동 재생하지 않는다</b> — 프레임은 레벨 팔레트 인덱스일 뿐이다.
    /// </para>
    ///
    /// <para>
    /// ★★ <b>좌측 캡은 «항상» 그린다.</b> 원본이 그렇다 [5회차 실측] — 무리의 맨 왼쪽이 아닌 칸은
    /// «왼쪽 이웃의 몸통»이 덮어서 안 보이는 것이지 안 그려지는 것이 아니다.
    /// 스킨이 한 장이 된 뒤에도 <b>기전은 그대로</b>다 — 왼쪽 이웃(중심 x−50)의 몸통이
    /// <c>[x−60, x−10]</c> 를 덮어 이 칸의 캡 <c>[x−35, x−15]</c> 을 통째로 가린다.
    /// 덮임은 ① 스프라이트 기하(칸막이 오른쪽 −10 + 피치 50 = 바깥 오른쪽 +40)와
    /// ② 그리기 순서(왼쪽이 위)가 «자산 쪽에서» 낸다 — 코드가 판정하지 않는다 (재발방지 #53).
    /// </para>
    /// </summary>
    public sealed class BlumgiBlockView : MonoBehaviour
    {
        [Header("스킨 — «한 장 · 5프레임» [17회차 실측]")]
        [SerializeField] private SpriteRenderer _skin;

        /// <summary>프리팹이 물고 있는 5프레임. 굽는 도구가 넣는다.</summary>
        [SerializeField] private Sprite[] _skinFrames = new Sprite[0];

        /// <summary>지금 서 있는 프레임 번호. <b>검사가 이것을 읽는다</b>.</summary>
        public int SkinFrame { get; private set; } = -1;

        /// <summary>지금 스킨이 물고 있는 스프라이트 이름. 배선·검사가 「모양을 옮겼나」를 본다.</summary>
        public string SkinSpriteName
        {
            get { return _skin == null || _skin.sprite == null ? string.Empty : _skin.sprite.name; }
        }

        /// <summary>
        /// 스킨 프레임을 고른다. <b>색을 넣지 않는다</b> — 색은 프레임에 구워져 있다 [실측].
        /// </summary>
        public void SetSkinFrame(int frame)
        {
            SkinFrame = frame;

            if (_skin == null || _skinFrames == null || _skinFrames.Length == 0)
                return;

            int index = Mathf.Clamp(frame, 0, _skinFrames.Length - 1);
            Sprite sprite = _skinFrames[index];

            if (sprite != null)
                _skin.sprite = sprite;
        }
    }
}
