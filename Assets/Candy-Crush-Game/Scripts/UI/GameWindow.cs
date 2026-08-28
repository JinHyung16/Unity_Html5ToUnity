using System;
using System.Collections.Generic;
using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.CandyCrush
{
    /// <summary>
    /// 게임 화면. 원본 <c>.scoreBoard</c> + <c>.grid</c> (<c>index.html:29~36</c>).
    ///
    /// <para>
    /// 원본은 둘이 <b>같이 켜지고 같이 꺼진다</b> (<c>script.js:190~191</c> · <c>237~238</c>) —
    /// 그래서 창 하나로 묶었다. 원본에서 좌우로 나란히 있던 것을
    /// Portrait 에서는 <b>위아래로</b> 놓는다 (<c>HtmlToUnityLogic/04_UIUX규칙.md</c>) —
    /// <b>바뀐 것은 요소 사이의 관계뿐이고 요소 안은 원본 치수 ×1.6</b> 이다.
    /// </para>
    /// </summary>
    public class GameWindow : BaseWindow
    {
        public static readonly WindowKey<GameWindow> Key =
            new WindowKey<GameWindow>("UI/Window/GameWindow");

        [Header("ScoreBoard")]
        [SerializeField] private TextMeshProUGUI _scoreText;
        [SerializeField] private TextMeshProUGUI _timerText;
        [SerializeField] private Button _changeModeButton;

        [Header("Board")]
        [SerializeField] private RectTransform _boardRoot;

        public event Action OnChangeModeClicked;
        public event Action<int> OnCellDragBegan;
        public event Action<int> OnCellDropped;
        public event Action<int> OnCellDragEnded;

        private readonly List<CellComponent> _cells = new List<CellComponent>(64);
        private IReadOnlyList<Sprite> _candySprites;

        /// <summary>
        /// 칸을 만든다. <b>인스펙터에 미리 박지 않는다</b> — 개수가 데이터(보드 폭)로 갈리기 때문이다.
        /// 고정 격자라 <b>시작할 때 전부 만들고 끝까지 든다</b> (풀링이 필요 없다).
        /// </summary>
        public void BuildCells(int cellCount)
        {
            if (_boardRoot == null || _cells.Count == cellCount)
                return;

            // ⚠ 구독은 «해제와 짝»으로 보여야 한다. ClearEvents 로 한 번에 끊으면
            //    코드에서 짝이 안 보이고, 「해제 없는 구독」 점검에도 안 잡힌다.
            for (int i = 0; i < _cells.Count; i++)
            {
                CellComponent old = _cells[i];

                if (old == null)
                    continue;

                old.OnDragBegan -= HandleCellDragBegan;
                old.OnDropped -= HandleCellDropped;
                old.OnDragEnded -= HandleCellDragEnded;
                old.ClearEvents();
            }

            _cells.Clear();

            for (int i = _boardRoot.childCount - 1; i >= 0; i--)
                DestroyImmediate(_boardRoot.GetChild(i).gameObject);

            for (int i = 0; i < cellCount; i++)
            {
                CellComponent cell = CellComponent.Auto.CreateForUI(_boardRoot);

                if (cell == null)
                    return;

                cell.name = $"Cell_{i:D2}";
                cell.SetIndex(i);
                cell.OnDragBegan += HandleCellDragBegan;
                cell.OnDropped += HandleCellDropped;
                cell.OnDragEnded += HandleCellDragEnded;

                _cells.Add(cell);
            }
        }

        /// <summary>사탕 그림표를 주입받는다. UI 는 주소를 모른다 — 받은 것을 그대로 쓴다.</summary>
        public void SetCandySprites(IReadOnlyList<Sprite> sprites)
        {
            _candySprites = sprites;
        }

        /// <summary>보드 상태를 그린다. <b>보드가 진실 소스</b>이고 여기는 표기만 한다.</summary>
        public void SetBoard(IReadOnlyList<int> cells)
        {
            if (cells == null)
                return;

            int count = Mathf.Min(cells.Count, _cells.Count);

            for (int i = 0; i < count; i++)
            {
                int value = cells[i];
                Sprite sprite = null;

                if (value >= 0 && _candySprites != null && value < _candySprites.Count)
                    sprite = _candySprites[value];

                _cells[i].SetSprite(sprite);
            }
        }

        public void SetScore(int score)
        {
            if (_scoreText != null)
                _scoreText.text = score.ToString();
        }

        /// <summary>
        /// 원본 <c>#timer</c>. Endless 에서는 <b>빈 문자열</b>이다 —
        /// ⚠ <b>GameObject 를 끄지 않는다.</b> 원본에서도 요소는 남아 있고 크기만 0이라
        /// <c>space-between</c> 배분에 계속 참여한다.
        /// </summary>
        public void SetTimerText(string text)
        {
            if (_timerText != null)
                _timerText.text = text ?? string.Empty;
        }

        public void SetInputLocked(bool locked)
        {
            for (int i = 0; i < _cells.Count; i++)
                _cells[i].SetInputLocked(locked);
        }

        protected override void OnOpened()
        {
            if (_changeModeButton == null)
                return;

            _changeModeButton.onClick.RemoveListener(HandleChangeMode);
            _changeModeButton.onClick.AddListener(HandleChangeMode);
        }

        protected override void OnClosing()
        {
            if (_changeModeButton != null)
                _changeModeButton.onClick.RemoveListener(HandleChangeMode);
        }

        private void HandleChangeMode()
        {
            OnChangeModeClicked?.Invoke();
        }

        private void HandleCellDragBegan(int index)
        {
            OnCellDragBegan?.Invoke(index);
        }

        private void HandleCellDropped(int index)
        {
            OnCellDropped?.Invoke(index);
        }

        private void HandleCellDragEnded(int index)
        {
            OnCellDragEnded?.Invoke(index);
        }
    }
}
