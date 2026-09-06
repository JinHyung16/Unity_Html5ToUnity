using System;
using System.Collections.Generic;
using JinHyung.Data;
using JinHyung.UI;
using JinHyung.UI.Fx;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 레벨 업 — <b>카드 3장 중 하나</b>.
    ///
    /// <para>
    /// ★★ <b>선택은 «2단계»다</b> [실측] — 카드를 눌러 «고르고», 그다음 하단 <b>「선택」</b> 버튼을 눌러야
    /// 확정된다. 한 번에 확정되게 만들면 원본과 조작이 갈린다 (실제로 자동 채록이 여기서 멈췄었다).
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>이 창이 떠 있는 동안 게임이 «멈춘다»</b> [실측 — 타이머가 40샘플 동안 고정].
    /// 멈추는 것은 매니저의 몫이고(원본 메인 루프의 정지 게이트), 이 창은 그 사실을 <b>알리기만</b> 한다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 원본의 <b>「모두 받기!」는 보상형 광고</b>다 [실측 — 재생 아이콘 + 광고 호출].
    /// <b>이관 범위 밖</b>이라 만들지 않는다 (의도된 차이).
    /// </para>
    /// </summary>
    public sealed class UndeadLevelUpWindow : BaseWindow
    {
        public static readonly WindowKey<UndeadLevelUpWindow> Key =
            new WindowKey<UndeadLevelUpWindow>("UI/Window/UndeadLevelUpWindow");

        [Header("Title")]
        [SerializeField] private TMP_Text _titleText;

        [Header("Cards")]
        [SerializeField] private Button[] _cardButtons;
        [SerializeField] private Image[] _cardFrames;
        [SerializeField] private Image[] _cardIcons;

        /// <summary>
        /// 축별 아이콘 — <b>주소 → 스프라이트</b>. 어드레서블이라 «미리» 받아 둔다 (확정표 10-c).
        /// <para>⚠ 카드가 뜨는 순간에 비동기로 받으면 <b>첫 프레임이 빈 아이콘</b>으로 뜬다.</para>
        /// </summary>
        private IReadOnlyDictionary<string, Sprite> _icons;

        public void BindIcons(IReadOnlyDictionary<string, Sprite> icons)
        {
            _icons = icons;
        }
        [SerializeField] private TMP_Text[] _cardTargetTexts;
        [SerializeField] private TMP_Text[] _cardStatTexts;

        [Header("Confirm")]
        [SerializeField] private Button _confirmButton;
        [SerializeField] private TMP_Text _confirmText;

        /// <summary>고른 카드가 확정됐다. 인자는 <c>UndeadUpgradeTable</c> 의 <c>Id</c> 다.</summary>
        public event Action<int> OnConfirmed;

        private readonly List<UndeadUpgradeData> _offered = new List<UndeadUpgradeData>(3);
        private int _selected = -1;

        // [소스] selectedButtonScale 1.08 · hoverButtonScale 1.04 · 바탕 알파 1/.9 · 「선택」 펄스 1+.025·sin(.008t)
        //   → 값은 프리팹의 UiStateScale · UiHoverScale · UiScalePulse 가 든다 (프리팹 빌더가 소스 값을 넣는다).

        /// <summary>카드를 세운다. 장수는 <b>원본과 같은 3장</b>이다 [실측 — 레벨업 9회 전부].</summary>
        public void Show(IReadOnlyList<UndeadUpgradeData> offered, string title, string confirmLabel)
        {
            _offered.Clear();
            _selected = -1;

            if (_titleText != null)
                _titleText.text = title;

            if (_confirmText != null)
                _confirmText.text = confirmLabel;

            for (int i = 0; i < _cardButtons.Length; i++)
            {
                bool has = offered != null && i < offered.Count;
                _cardButtons[i].gameObject.SetActive(has);

                if (has == false)
                    continue;

                UndeadUpgradeData data = offered[i];
                _offered.Add(data);

                if (_cardTargetTexts[i] != null)
                    _cardTargetTexts[i].text = Text(data.TargetTextKey);

                // ★ 카드 바탕은 «축마다 색이 다르다» [소스 ol[].color → bg.tint]. 표의 ColorHex 를 읽는다.
                if (_cardFrames[i] != null && ColorUtility.TryParseHtmlString("#" + data.ColorHex, out Color tint))
                    _cardFrames[i].color = tint;

                if (_cardStatTexts[i] != null)
                    _cardStatTexts[i].text = data.DisplayPrefix
                        ? $"{data.DisplayPercent} {Text(data.StatTextKey)}"
                        : $"{Text(data.StatTextKey)} {data.DisplayPercent}";

                // ★★ 아이콘은 «축마다 다르다» [실측 — 원본 카드 3장의 아이콘이 서로 달랐다].
                //   ⚠ 이 줄이 없으면 표에 <c>IconAddress</c> 가 «있는데도» 세 장이 전부
                //     프리팹 기본 그림으로 뜬다 — 데이터는 다 옮겼는데 «화면만» 하나로 보이는,
                //     어떤 데이터 검사에도 안 걸리는 종류의 공백이다.
                if (_cardIcons[i] != null && _icons != null
                    && _icons.TryGetValue(data.IconAddress, out Sprite icon) && icon != null)
                {
                    _cardIcons[i].sprite = icon;
                }
            }

            UpdateSelection();

            // ★ 「선택」은 카드를 고르기 «전»에는 없다 [실측 — 고른 뒤에야 나타난다]
            if (_confirmButton != null)
                _confirmButton.gameObject.SetActive(false);
        }

        protected override void OnOpened()
        {
            base.OnOpened();

            for (int i = 0; i < _cardButtons.Length; i++)
            {
                int index = i;
                _cardButtons[i].onClick.RemoveAllListeners();
                _cardButtons[i].onClick.AddListener(() => Select(index));
            }

            if (_confirmButton != null)
            {
                _confirmButton.onClick.RemoveAllListeners();
                _confirmButton.onClick.AddListener(Confirm);
            }
        }

        /// <summary>⚠ 연 곳에서 건 것을 <b>여기서 반드시 끊는다</b> — 안 끊으면 다음 레벨업에 두 번 불린다.</summary>
        protected override void OnClosing()
        {
            for (int i = 0; i < _cardButtons.Length; i++)
                _cardButtons[i].onClick.RemoveAllListeners();

            if (_confirmButton != null)
                _confirmButton.onClick.RemoveAllListeners();

            base.OnClosing();
        }

        private void Select(int index)
        {
            if (index < 0 || index >= _offered.Count)
                return;

            _selected = index;
            UpdateSelection();

            if (_confirmButton != null)
                _confirmButton.gameObject.SetActive(true);
        }

        private void Confirm()
        {
            if (_selected < 0 || _selected >= _offered.Count)
                return;

            OnConfirmed?.Invoke(_offered[_selected].Id);
        }

        /// <summary>
        /// 고른 카드를 강조한다 [소스 <c>updateOptionButtonStates</c>] — 배율·알파는 카드의 <see cref="UiStateScale"/> 이 든다.
        /// 호버(×1.04)는 <see cref="UiHoverScale"/>, 「선택」 펄스는 <see cref="UiScalePulse"/> 가 스스로 돈다.
        /// </summary>
        private void UpdateSelection()
        {
            for (int i = 0; i < _cardButtons.Length; i++)
            {
                if (_cardButtons[i] == null)
                    continue;

                var state = _cardButtons[i].GetComponent<UiStateScale>();

                if (state != null)
                    state.SetSelected(i == _selected);
            }
        }

        private static string Text(string code)
        {
            return GameRoot.Instance.UndeadTextDataContainer.Ko(code);
        }
    }
}
