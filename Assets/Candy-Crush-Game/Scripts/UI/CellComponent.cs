using System;
using System.Collections;
using JinHyung.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JinHyung.CandyCrush
{
    /// <summary>
    /// 보드의 칸 하나. 원본 <c>.grid div</c> (<c>style.css:25~33</c>) 에 대응한다.
    ///
    /// <para>
    /// <b>칸은 상태를 들지 않는다.</b> 보드 데이터가 진실이고 여기는 표기만 한다 —
    /// 칸이 상태를 들면 원본 배열과 화면이 갈린다.
    /// </para>
    ///
    /// <para>
    /// 입력은 원본 6종을 3개로 접었다 (<c>HtmlToUnityLogic/01_게임플로우.md</c>):
    /// <c>dragstart</c> → <see cref="OnBeginDrag"/> · <c>drop</c> → <see cref="OnDrop"/> ·
    /// <c>dragend</c> → <see cref="OnEndDrag"/>.
    /// <b>유니티도 <c>OnDrop</c> 을 <c>OnEndDrag</c> 보다 먼저 부르므로 원본 순서가 유지된다.</b>
    /// </para>
    /// </summary>
    public class CellComponent : BaseComponent,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        public static readonly PrefabAuto<CellComponent> Auto =
            new PrefabAuto<CellComponent>("UI/Component/CellComponent");

        /// <summary>원본 <c>.grid div:hover</c> 의 <c>scale(1.05)</c> (<c>style.css:36</c>).</summary>
        private const float PressScale = 1.05f;

        /// <summary>원본 <c>transition: transform 0.2s ease</c> (<c>style.css:31</c>).</summary>
        private const float PressDuration = 0.2f;

        [SerializeField] private Image _icon;

        public int Index { get; private set; }

        public event Action<int> OnDragBegan;
        public event Action<int> OnDropped;
        public event Action<int> OnDragEnded;

        private Coroutine _scaleRoutine;
        private bool _inputLocked;

        public void SetIndex(int index)
        {
            Index = index;
        }

        /// <summary>
        /// 사탕 그림을 넣는다. <c>null</c> 이면 빈 칸이다 —
        /// 원본은 <c>backgroundImage = ""</c> 로 두어 <b>그리드 배경이 비쳐 보인다.</b>
        /// </summary>
        public void SetSprite(Sprite sprite)
        {
            if (_icon == null)
                return;

            _icon.sprite = sprite;
            _icon.enabled = sprite != null;
        }

        public void SetInputLocked(bool locked)
        {
            _inputLocked = locked;

            if (locked)
                ResetScale();
        }

        /// <summary>⚠ 재사용 전에 구독을 끊는다. 안 끊으면 다음 사용처에서 두 번 불린다.</summary>
        public void ClearEvents()
        {
            OnDragBegan = null;
            OnDropped = null;
            OnDragEnded = null;
        }

        // ────────────────────────────── 입력

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_inputLocked)
                return;

            OnDragBegan?.Invoke(Index);
        }

        /// <summary>
        /// 원본에 대응 동작이 없다. 다만 <b>이걸 구현하지 않으면 유니티가 드래그 자체를 시작하지 않아</b>
        /// <c>OnDrop</c> · <c>OnEndDrag</c> 가 안 온다 (원본 <c>dragOver</c> 의 <c>preventDefault</c> 와 같은 역할).
        /// </summary>
        public void OnDrag(PointerEventData eventData)
        {
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (_inputLocked)
                return;

            OnDropped?.Invoke(Index);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_inputLocked)
                return;

            OnDragEnded?.Invoke(Index);
        }

        // ────────────────────────────── 누름 연출 (원본 :hover 의 모바일 대체 — 원장 결정 3)

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_inputLocked)
                return;

            StartScale(PressScale);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            StartScale(1f);
        }

        private void StartScale(float target)
        {
            if (isActiveAndEnabled == false)
            {
                CachedTransform.localScale = Vector3.one * target;
                return;
            }

            if (_scaleRoutine != null)
                StopCoroutine(_scaleRoutine);

            _scaleRoutine = StartCoroutine(ScaleTo(target));
        }

        /// <summary>
        /// 원본 값 그대로 — 배율 1.05 · 0.2초 · ease.
        ///
        /// <para>
        /// ⚠ 코루틴은 <b>누른 칸 하나에만</b> 돈다. 칸마다 <c>Animator</c> 를 붙이면
        /// 칸 수만큼 비용이 붙는다 (재생 계층은 개체 수가 고른다).
        /// </para>
        /// </summary>
        private IEnumerator ScaleTo(float target)
        {
            Vector3 from = CachedTransform.localScale;
            Vector3 to = Vector3.one * target;
            float elapsed = 0f;

            while (elapsed < PressDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / PressDuration);

                // CSS 의 ease 에 가장 가까운 곡선
                CachedTransform.localScale = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            CachedTransform.localScale = to;
            _scaleRoutine = null;
        }

        private void ResetScale()
        {
            if (_scaleRoutine != null)
            {
                StopCoroutine(_scaleRoutine);
                _scaleRoutine = null;
            }

            CachedTransform.localScale = Vector3.one;
        }

        private void OnDisable()
        {
            ResetScale();
        }
    }
}
