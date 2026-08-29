using System;
using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.PacMan
{
    /// <summary>
    /// 원본 <c>.header</c> + <c>.hint</c>. <b>화면은 이것 하나뿐</b>이다 —
    /// 원본에 시작 화면도 결과 화면도 없다 (<c>01_게임플로우.md</c>).
    ///
    /// <para>⚠ 여기는 <b>표기만</b> 한다. 상태는 <see cref="MazeManager"/> 가 든다.</para>
    /// </summary>
    public class PacGameWindow : BaseWindow
    {
        public static readonly WindowKey<PacGameWindow> Key =
            new WindowKey<PacGameWindow>("UI/Window/PacGameWindow");

        [SerializeField] private TextMeshProUGUI _scoreText;
        [SerializeField] private TextMeshProUGUI _livesText;
        [SerializeField] private TextMeshProUGUI _hintText;
        [SerializeField] private Button _restartButton;
        [SerializeField] private SwipeInput _swipe;

        public event Action OnRestartClicked;
        public event Action<int, int> OnDirection;
        public event Action OnFirstInput;

        protected override void OnOpened()
        {
            if (_restartButton != null)
                _restartButton.onClick.AddListener(HandleRestart);

            if (_swipe != null)
            {
                _swipe.OnDirection += HandleDirection;
                _swipe.OnFirstInput += HandleFirstInput;
            }
        }

        protected override void OnClosing()
        {
            if (_restartButton != null)
                _restartButton.onClick.RemoveListener(HandleRestart);

            if (_swipe != null)
            {
                _swipe.OnDirection -= HandleDirection;
                _swipe.OnFirstInput -= HandleFirstInput;
            }

            OnRestartClicked = null;
            OnDirection = null;
            OnFirstInput = null;
        }

        /// <summary>원본 <c>updateUI</c> (<c>script.js:198~201</c>) — 숫자만 바꾼다.</summary>
        public void SetState(int score, int lives)
        {
            if (_scoreText != null)
                _scoreText.text = score.ToString();

            if (_livesText != null)
                _livesText.text = lives.ToString();
        }

        /// <summary>
        /// 안내 문구. ⚠ <b>원본은 키보드를 전제한다</b>
        /// (<c>Use arrow keys or WASD to move…</c>) — 입력을 스와이프로 바꿨으므로
        /// 문구도 바뀐다. <b>「의도된 차이」로 등재</b>돼 있다.
        /// </summary>
        public void SetHint(string text)
        {
            if (_hintText != null)
                _hintText.text = text;
        }

        private void HandleRestart()
        {
            OnRestartClicked?.Invoke();
        }

        private void HandleDirection(int dx, int dy)
        {
            OnDirection?.Invoke(dx, dy);
        }

        private void HandleFirstInput()
        {
            OnFirstInput?.Invoke();
        }
    }
}
