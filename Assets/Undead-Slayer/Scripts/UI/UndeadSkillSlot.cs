using JinHyung.UndeadSlayer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.UI
{
    /// <summary>
    /// HUD 의 스킬 슬롯 하나 [소스 <c>Wd</c>].
    ///
    /// <para>
    /// 원본 규격 — 상자 <b>54×54</b> · 바탕 <c>skill_bg</c>(9슬라이스 2) ·
    /// 아이콘은 <b>54×54 에 맞춰</b> 가운데(27,27), 데스크톱은 <b>y 를 8 내린다</b> ·
    /// 쿨다운 글자 20 굵게 흰색 + 검정 테두리 4, 자리 (29,29) ·
    /// 키 뱃지는 <c>Space</c> 면 폭 48, 아니면 24 · 높이 16 · 모서리 3 · 채움 <c>#F5E8C8</c> 알파 .95.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>보이는 규칙이 셋 다 다르다</b> [소스 <c>applySnapshot</c>] —
    /// 쿨다운 중이면 <b>전체 알파 0.5</b> + 숫자를 띄우고, <b>쓸 수 있을 때만</b> 키 뱃지를 띄운다.
    /// 「가지고 있다」와 「지금 쓸 수 있다」를 한 표시로 합치면 원본과 달라진다.
    /// </para>
    /// </summary>
    public sealed class UndeadSkillSlot : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _item;
        [SerializeField] private Image _background;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _cooldownText;
        [SerializeField] private Image _keyBadge;
        [SerializeField] private TMP_Text _keyText;

        /// <summary>쿨다운 중 흐려지는 정도 [소스 <c>itemAlpha: i ? .5 : 1</c>].</summary>
        private const float DimmedAlpha = 0.5f;

        /// <summary>
        /// 이 슬롯이 맡은 스킬 — <b>굽는 쪽이 박는다</b>.
        /// <para>⚠ 자동 프로퍼티로 두면 «직렬화가 안 돼» 프리팹에 안 남는다 (재발방지 <c>#171</c> 과 같은 뿌리).</para>
        /// </summary>
        [SerializeField] private EUndeadSkill _skill;

        public EUndeadSkill Skill
        {
            get { return _skill; }
        }

        /// <summary>
        /// 매 프레임 상태를 얹는다.
        /// <para>⚠ 쿨다운 숫자는 <b>올림</b>이다 [소스 — <c>cooldownSeconds</c> 는 초 단위 정수].</para>
        /// </summary>
        public void Apply(bool owned, double cooldownRemaining, bool ready)
        {
            if (gameObject.activeSelf != owned)
                gameObject.SetActive(owned);

            if (owned == false)
                return;

            bool cooling = cooldownRemaining > 0.0;

            if (_item != null)
                _item.alpha = cooling ? DimmedAlpha : 1f;

            if (_cooldownText != null)
            {
                _cooldownText.enabled = cooling;
                _cooldownText.text = cooling ? Mathf.CeilToInt((float)cooldownRemaining).ToString() : string.Empty;
            }

            // ★ 키 뱃지는 «쓸 수 있을 때만» 뜬다 [소스 showKeyBadge: !mobile && isReady]
            if (_keyBadge != null)
                _keyBadge.enabled = ready;

            if (_keyText != null)
                _keyText.enabled = ready;
        }
    }
}
