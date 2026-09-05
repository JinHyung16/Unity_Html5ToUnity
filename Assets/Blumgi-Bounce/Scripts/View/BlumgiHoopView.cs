using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 골대. ★ <b>통짜가 아니라 6부품의 «묶음»</b>이고, 묶음 내부 상대 위치가 <b>전 레벨에서 동일</b>하다 [UIUX 2-d-3].
    /// 그래서 레벨 데이터는 <b>림 중심 하나</b>만 갖고 나머지는 고정 오프셋이다.
    ///
    /// <para>
    /// 그리는 것은 <b>림 · 그물 · 화살표</b> 셋뿐이다 —
    /// 충돌 기둥(<c>spr_BasketCollision</c>)과 감지기(<c>spr_ballDetector</c>)는 <b>안 그린다</b> [UIUX 2-g].
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>화살표는 고정 오프셋이 아니다</b> [실측 — 검산에서 잡았다].
    /// x 는 항상 림과 같지만 y 오프셋이 −76.5 ~ −175.5 로 흩어진다 ⇒ <b>위아래로 떠다니는 애니메이션</b>이고
    /// 덤프 값은 «그 시점의 위상»일 뿐이다. 두 장의 간격 <b>25 는 확정</b>.
    /// </para>
    ///
    /// <para>
    /// ★ <b>[해소 · 8회차] 부유 상수가 닫혔다</b> — Sine 비헤이비어 <b>수직 · 정현파 ·
    /// 주기 0.25 s · 진폭 ±10 px · 기준 y 788</b> [실측 · <c>_sdkInst</c> 직독 + 화면 실측 일치].
    /// 값은 <see cref="BlumgiLevelPresenter"/> 가 넣는다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>골인하면 사라진다</b> [8회차 실측 · <c>ArrowRestart.SetVisible(0)</c> — 골 프레임].
    /// </para>
    /// </summary>
    public sealed class BlumgiHoopView : MonoBehaviour
    {
        [Header("그리는 부품")]
        [SerializeField] private SpriteRenderer _rim;
        [SerializeField] private SpriteRenderer _net;

        [Header("골인 애니메이션 (Animation 3) — 17회차 실측")]
        [SerializeField] private BlumgiHoopGoalAnimation _rimGoalAnimation;

        [SerializeField] private BlumgiHoopGoalAnimation _netGoalAnimation;

        [Header("골 지시 화살표 — 2장 겹침 (간격 25 world 는 확정)")]
        [SerializeField] private Transform _arrowRoot;
        [SerializeField] private BlumgiFloatMotion _arrowFloat;

        /// <summary>
        /// 그물 색. 림의 두 색(<c>#FF3333</c>/<c>#CC3366</c>)은 <b>전 레벨 동일</b>이라 텍스처에 구웠고,
        /// 그물만 흰색이라 tint 로 받는다 [05_연출 §1].
        /// </summary>
        public void SetNetColor(Color net)
        {
            if (_net != null)
                _net.color = net;
        }

        /// <summary>림 색을 «데이터가 바뀌면» 갈아끼울 자리. 지금 데이터는 전 레벨 동일하다.</summary>
        public void SetRimTint(Color tint)
        {
            if (_rim != null)
                _rim.color = tint;
        }

        /// <summary>
        /// 골 화살표를 켜고 끈다 — <b>골인 프레임에 사라진다</b> [8회차 실측 · <c>SetVisible(0)</c>].
        /// 다음 레벨이 올라오면 다시 켜진다.
        /// </summary>
        public void SetArrowVisible(bool visible)
        {
            if (_arrowRoot != null)
                _arrowRoot.gameObject.SetActive(visible);
        }

        /// <summary>지금 화살표가 보이나. <b>재생 검사가 이것을 본다</b>.</summary>
        public bool IsArrowVisible
        {
            get { return _arrowRoot != null && _arrowRoot.gameObject.activeSelf; }
        }

        /// <summary>
        /// ★★ <b>골인 «그 프레임»에 부른다</b> — 골대가 평상시 <c>Animation 2</c> 에서
        /// <b>«골인 전용» <c>Animation 3</c> 4프레임</b>으로 갈아탄다 [17회차 실측].
        /// 「림 튕김」과 별개다 — 없으면 골 순간이 밋밋해진다.
        /// </summary>
        public void PlayGoalAnimation()
        {
            _rimGoalAnimation?.Play();
            _netGoalAnimation?.Play();
        }

        /// <summary>레벨이 갈릴 때 기본 크기로 되돌린다.</summary>
        public void ResetGoalAnimation()
        {
            _rimGoalAnimation?.ResetPose();
            _netGoalAnimation?.ResetPose();
        }

        /// <summary>지금 림 쪽 골인 애니 프레임 (−1 = 안 돈다). <b>재생 검사가 이것을 본다</b>.</summary>
        public int RimGoalFrame
        {
            get { return _rimGoalAnimation == null ? -1 : _rimGoalAnimation.Frame; }
        }

        /// <summary>지금 그물 쪽 골인 애니 프레임 (−1 = 안 돈다).</summary>
        public int NetGoalFrame
        {
            get { return _netGoalAnimation == null ? -1 : _netGoalAnimation.Frame; }
        }

        /// <summary>지금 림 표시 크기 (원본 world px).</summary>
        public Vector2 RimSizeWorld
        {
            get { return _rimGoalAnimation == null ? Vector2.zero : _rimGoalAnimation.SizeWorld; }
        }

        /// <summary>림 골인 애니의 프레임 «표». <b>검사가 «둘레 보존 267»을 여기서 잰다</b>.</summary>
        public Vector2[] RimGoalFrameSizes
        {
            get { return _rimGoalAnimation == null ? new Vector2[0] : _rimGoalAnimation.FrameSizesWorld; }
        }

        /// <summary>화살표 부유 애니메이션의 기준 y (림 중심 기준 오프셋, world). 배선이 넣는다.</summary>
        public void SetArrowBaseOffset(float worldOffsetY)
        {
            if (_arrowRoot == null)
                return;

            Vector3 local = _arrowRoot.localPosition;
            local.y = BlumgiUnits.ToUnits(-worldOffsetY);
            _arrowRoot.localPosition = local;

            // ★ 부유가 «옮기기 전» 자리를 기준으로 잡고 있으면 매 프레임 도로 끌어당긴다 —
            //   자리를 바꾼 «그 자리»에서 기준을 다시 잡아야 한다.
            _arrowFloat?.ResetOrigin();
        }

        public BlumgiFloatMotion ArrowFloat
        {
            get { return _arrowFloat; }
        }
    }
}
