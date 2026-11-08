using System;
using System.Collections.Generic;
using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.FrontWarsIo
{
    /// <summary>튜토리얼 화면에 이번 프레임 그릴 것 — 관리자가 엔진 상태에서 채운다 `[소스] QPQdQ6Sy.js:function Er(){`.</summary>
    public sealed class FrontWarsTutorialView
    {
        public EFrontWarsTutorialStage Stage;

        // 목표 칩 [소스 fr]
        public int ObjectiveCount;
        public int Index;
        public string Icon;
        public string ChipText;
        public string Progress;
        public bool Transitioning;

        /// <summary>안내 카드 [소스 mr] — 같은 키면 이어서, 키가 바뀌면 다시 뜬다.</summary>
        public string TipKey;
        public string TipText;

        /// <summary>안내 카드가 10초 뒤 사라지나 [소스 hr pr=1e4]. 건설 안내 첫 단계 카드는 계속 뜬다.</summary>
        public bool TipTimed;

        /// <summary>비추기 [소스 nr] — 키가 바뀌면 시간을 새로 잰다. 대상이 없으면 가리개만 깔고 카드는 26% 자리에.</summary>
        public string SpotKey;
        public Rect? SpotTarget;
        public Rect? SpotBoundary;
        public string SpotText;
        public float SpotMask;

        /// <summary>「알겠습니다!」 버튼과 시간 막대 — null 이면 버튼 없음.</summary>
        public string SpotGotIt;
        public float SpotSeconds;

        public bool SpawnMarker;
        public string SpawnLabel;

        /// <summary>조준경 [소스 sr] — 화면 좌표(왼쪽 위 기준 px).</summary>
        public bool Reticle;
        public Vector2 ReticleScreen;
        public bool ReticleFriendly;
        public string ReticleLabel;

        public bool Alert;
        public int CelebrationSeq;
        public int VictoryTiles;
    }

    /// <summary>
    /// 튜토리얼 화면 — 목표 칩 · 안내 카드 · 비추기 · 스폰 표시 · 조준경 · 경고 테두리 · 색종이 · 승리 카드 · 사망 화면
    /// `[소스] QPQdQ6Sy.js: fr · mr · hr · gr · vr · yr · nr · qn · sr · Sr · br · xr · Cr · Tr · Er`.
    /// </summary>
    public sealed class FrontWarsTutorialWindow : BaseWindow
    {
        public static readonly WindowKey<FrontWarsTutorialWindow> Key = new WindowKey<FrontWarsTutorialWindow>("UI/Window/FrontWarsTutorialWindow");

        /// <summary>테마 강조색 — 게임 안 주황 `[실측] 목표 문구 색`.</summary>
        public static readonly Color AccentColor = new Color32(242, 149, 32, 255);

        /// <summary>완료 중 칩 색 — green-400.</summary>
        public static readonly Color DoneColor = new Color32(74, 222, 128, 255);

        /// <summary>칩 윗변 = 광고 띠 아래 + 10 · 안내 카드 = 그 + 78 `[소스] h=i+10 · top:h+78` (PC 는 띠가 없다).</summary>
        public const float ChipTop = 10f;
        public const float TipTop = ChipTop + 78f;

        public const float TipSeconds = 10f;

        /// <summary>비추기 여백 12 · 카드 폭 300 · 카드 간격 12+14 `[소스] Jn=12 · Yn=300`.</summary>
        public const float SpotPad = 12f;
        public const float SpotCardWidth = 300f;
        private const float SpotCardGap = SpotPad + 14f;
        private const float SpotPulseSeconds = 1.4f;

        /// <summary>구멍 · 테두리 모서리 둥글기 `[소스] nr rect rx:"10"`.</summary>
        public const float SpotHoleRadius = 10f;

        /// <summary>모서리 호 굵기 — 막대(2 ↔ 3.5)의 가운데 값으로 둔다.</summary>
        public const float SpotArcWidth = 2.75f;

        /// <summary>나타나기 — 비추기 카드 · 안내 카드 0.25초, 승리 카드 0.35초 `[소스] tut2-spot-card-in · tut2-tip-in · tut2-victory-in`.</summary>
        private const float CardInSeconds = 0.25f;
        private const float VictoryInSeconds = 0.35f;
        private const float TipInRise = 6f;
        private const float VictoryInRise = 14f;
        private const float VictoryInScale = 0.96f;

        /// <summary>스폰 표시 — 화면 (50%, 45%) · 고리 120 · 1.7초 · 점 18 · 이름표 +74 `[소스] qn · Kn=120`.</summary>
        public const float SpawnTopRatio = 0.45f;
        private const float SpawnRingSize = 120f;
        private const float SpawnRingSeconds = 1.7f;
        private static readonly float[] SpawnRingDelays = { 0f, 0.5f, 1f };

        /// <summary>조준경 — 고리 96 · 1.5초 · 회전 7초 · 점 16 · 이름표 +60 · 가장자리 24 안이면 숨김 `[소스] sr · ar=96 · rr=24`.</summary>
        private const float ReticleSize = 96f;
        private const float ReticleEdge = 24f;
        private const float ReticleRingSeconds = 1.5f;
        private const float ReticleSpinSeconds = 7f;
        private static readonly float[] ReticleRingDelays = { 0f, 0.6f };

        public static readonly Color EnemyColor = new Color32(0xff, 0x3b, 0x30, 255);
        public static readonly Color EnemyDot = new Color32(0xff, 0x61, 0x57, 255);
        public static readonly Color FriendlyColor = new Color32(0x34, 0xd3, 0x99, 255);
        public static readonly Color FriendlyDot = new Color32(0x6e, 0xe7, 0xb7, 255);

        /// <summary>경고 테두리 — 0.35 ↔ 1 을 1초에 한 번 `[소스] Sr tut2-alert 1s`.</summary>
        private const float AlertSeconds = 1f;

        /// <summary>색종이 — 화면 (50%, 30%) · 26장 8×8 · 1.1초 · ✅ 1.3초 · 1.5초 뒤 치운다 `[소스] br · xr`.</summary>
        public const float ConfettiTopRatio = 0.3f;
        public const int ConfettiCount = 26;
        private const float ConfettiSeconds = 1.1f;
        private const float CheckSeconds = 1.3f;
        private const float CelebrationSeconds = 1.5f;

        private static readonly Color[] ConfettiColors =
        {
            new Color32(0xff, 0xd1, 0x66, 255),
            new Color32(0xef, 0x47, 0x6f, 255),
            new Color32(0x06, 0xd6, 0xa0, 255),
            new Color32(0x4c, 0xc9, 0xf0, 255),
            new Color32(0xf8, 0xf9, 0xfa, 255)
        };

        /// <summary>사망 뒤 저절로 다시 시작 `[소스] wr=3`.</summary>
        public const int RestartSeconds = 3;

        [Header("Chip")]
        [SerializeField] private RectTransform _chipGroup;
        [SerializeField] private Image[] _dots;
        [SerializeField] private Outline _chipBorder;
        [SerializeField] private TMP_Text _chipIcon;
        [SerializeField] private TMP_Text _chipText;
        [SerializeField] private TMP_Text _chipProgress;

        [Header("Tip")]
        [SerializeField] private RectTransform _tipCard;
        [SerializeField] private TMP_Text _tipText;

        [Header("Spotlight")]
        [SerializeField] private RectTransform _spotRoot;
        [SerializeField] private RectTransform[] _spotMasks;
        [SerializeField] private RectTransform[] _spotFrame;
        [SerializeField] private Image[] _spotCorners;
        [SerializeField] private Image[] _spotArcs;
        [SerializeField] private CanvasGroup _spotCardGroup;
        [SerializeField] private CanvasGroup _tipGroup;
        [SerializeField] private CanvasGroup _victoryGroup;
        [SerializeField] private RectTransform _spotCard;
        [SerializeField] private TMP_Text _spotText;
        [SerializeField] private Button _gotIt;
        [SerializeField] private TMP_Text _gotItLabel;
        [SerializeField] private RectTransform _gotItTimer;

        [Header("Spawn")]
        [SerializeField] private RectTransform _spawnRoot;
        [SerializeField] private Image[] _spawnRings;
        [SerializeField] private Image _spawnDot;
        [SerializeField] private Image _spawnDotFill;
        [SerializeField] private RectTransform _spawnLabelRoot;
        [SerializeField] private TMP_Text _spawnLabel;

        [Header("Reticle")]
        [SerializeField] private RectTransform _reticleRoot;
        [SerializeField] private Image[] _reticleRings;
        [SerializeField] private RectTransform _reticleSpinner;
        [SerializeField] private Image _reticleDashed;
        [SerializeField] private Image[] _reticleTicks;
        [SerializeField] private Image _reticleDot;
        [SerializeField] private Image _reticleDotFill;
        [SerializeField] private Image _reticleLabelBg;
        [SerializeField] private Outline _reticleLabelBorder;
        [SerializeField] private TMP_Text _reticleLabel;

        [Header("Alert")]
        [SerializeField] private Image _alert;

        [Header("Confetti")]
        [SerializeField] private RectTransform _confettiRoot;
        [SerializeField] private Image[] _confetti;
        [SerializeField] private TMP_Text _check;

        [Header("Victory")]
        [SerializeField] private RectTransform _victoryCard;
        [SerializeField] private TMP_Text _victoryTitle;
        [SerializeField] private TMP_Text _victoryBody;
        [SerializeField] private Button _keepButton;
        [SerializeField] private TMP_Text _keepLabel;
        [SerializeField] private Button _realMatchButton;
        [SerializeField] private TMP_Text _realMatchLabel;

        [Header("Death")]
        [SerializeField] private GameObject _deathRoot;
        [SerializeField] private TMP_Text _deathTitle;
        [SerializeField] private TMP_Text _deathBody;
        [SerializeField] private Button _restartButton;
        [SerializeField] private TMP_Text _restartLabel;

        private Func<string, string> _text;
        private string _tipKey;
        private float _tipLeft;
        private float _tipShownAt;
        private float _spotShownAt;
        private float _victoryShownAt;
        private Vector2 _tipBase;
        private Vector2 _victoryBase;
        private string _spotKey;
        private float _spotLeft;
        private bool _spotDone;
        private float _clock;
        private int _celebrationSeq;
        private float _celebrationTime = -1f;
        private readonly List<Vector3> _confettiMotion = new List<Vector3>();
        private readonly List<float> _confettiDelay = new List<float>();
        private EFrontWarsTutorialStage _stage = EFrontWarsTutorialStage.Objectives;
        private float _deathLeft;
        private bool _restartSent;
        private int _victoryTiles = -1;
        private Vector2 _spawnLabelBase;
        private Vector2 _reticleLabelBase;

        /// <summary>「알겠습니다!」 · 시간 끝 — 인자는 비추기 키.</summary>
        public event Action<string> OnSpotlightDone;
        public event Action OnKeepPlaying;
        public event Action OnRealMatch;
        public event Action OnRestart;

        public bool SpotlightVisible => _spotRoot.gameObject.activeSelf;
        public bool TipVisible => _tipCard.gameObject.activeSelf;
        public bool SpawnMarkerVisible => _spawnRoot.gameObject.activeSelf;
        public string ChipText => _chipText.text;
        public string SpotText => _spotText.text;
        public string TipText => _tipText.text;

        public void Show(Func<string, string> text)
        {
            FrontWarsFonts.ApplyTo(gameObject);
            _text = text;
            _gotItLabel.text = text("tutorial2.got_it");
            _victoryTitle.text = text("tutorial2.victory.title");
            _keepLabel.text = text("tutorial2.victory.keep");
            _realMatchLabel.text = text("tutorial2.victory.real_match");
            _deathTitle.text = text("tutorial2.dead.title");
            _deathBody.text = text("tutorial2.dead.body");

            _tipKey = null;
            _spotKey = null;
            _celebrationSeq = 0;
            _celebrationTime = -1f;
            _stage = EFrontWarsTutorialStage.Objectives;
            _victoryTiles = -1;
            _spawnLabelBase = _spawnLabelRoot.anchoredPosition;
            _tipBase = _tipCard.anchoredPosition;
            _victoryBase = _victoryCard.anchoredPosition;
            _reticleLabelBase = _reticleLabelBg.rectTransform.anchoredPosition;
            ApplySprites();
            HideAll();
        }

        private void ApplySprites()
        {
            foreach (Image dot in _dots)
                dot.sprite = FrontWarsTutorialSprites.Dot();

            foreach (Image ring in _spawnRings)
                ring.sprite = FrontWarsTutorialSprites.Ring(4f / SpawnRingSize);

            foreach (Image ring in _reticleRings)
                ring.sprite = FrontWarsTutorialSprites.Ring(4f / ReticleSize);

            _reticleDashed.sprite = FrontWarsTutorialSprites.DashedRing(3f / ReticleSize);
            // 점 = 흰 원(테두리) 위에 색 원
            _spawnDot.sprite = FrontWarsTutorialSprites.Dot();
            _spawnDotFill.sprite = FrontWarsTutorialSprites.Dot();
            _reticleDot.sprite = FrontWarsTutorialSprites.Dot();
            _reticleDotFill.sprite = FrontWarsTutorialSprites.Dot();
            _alert.sprite = FrontWarsTutorialSprites.InsetGlow();

            foreach (Image corner in _spotCorners)
                corner.sprite = FrontWarsTutorialSprites.CornerOutside();

            foreach (Image arc in _spotArcs)
                arc.sprite = FrontWarsTutorialSprites.CornerArc(SpotHoleRadius, SpotArcWidth);
            _alert.type = Image.Type.Sliced;
        }

        private void HideAll()
        {
            _chipGroup.gameObject.SetActive(false);
            _tipCard.gameObject.SetActive(false);
            _spotRoot.gameObject.SetActive(false);
            _spawnRoot.gameObject.SetActive(false);
            _reticleRoot.gameObject.SetActive(false);
            _alert.gameObject.SetActive(false);
            _confettiRoot.gameObject.SetActive(false);
            _victoryCard.gameObject.SetActive(false);
            _deathRoot.SetActive(false);
        }

        /// <summary>이번 프레임 상태를 그린다 `[소스] Er(){ if(phase==='dead') … if(phase==='victory') … transitioning … briefingPending … }`.</summary>
        public void Render(FrontWarsTutorialView view)
        {
            if (view.Stage != _stage)
                EnterStage(view.Stage);

            bool playing = view.Stage == EFrontWarsTutorialStage.Objectives || view.Stage == EFrontWarsTutorialStage.Extra;
            UpdateCelebration(view.CelebrationSeq, view.Stage != EFrontWarsTutorialStage.Dead && view.Stage != EFrontWarsTutorialStage.Finished);

            _deathRoot.SetActive(view.Stage == EFrontWarsTutorialStage.Dead);
            _victoryCard.gameObject.SetActive(view.Stage == EFrontWarsTutorialStage.Victory);

            if (view.Stage == EFrontWarsTutorialStage.Victory && view.VictoryTiles != _victoryTiles)
            {
                _victoryTiles = view.VictoryTiles;
                SetText(_victoryBody, _text("tutorial2.victory.body").Replace("{0}", FrontWarsNumberFormat.Number(view.VictoryTiles)));
            }

            _chipGroup.gameObject.SetActive(playing);

            if (playing == false)
            {
                _tipCard.gameObject.SetActive(false);
                _spotRoot.gameObject.SetActive(false);
                _spawnRoot.gameObject.SetActive(false);
                _reticleRoot.gameObject.SetActive(false);
                _alert.gameObject.SetActive(false);
                return;
            }

            RenderChip(view);
            RenderTip(view);
            RenderSpot(view);
            _spawnRoot.gameObject.SetActive(view.SpawnMarker);
            if (view.SpawnMarker)
                SetText(_spawnLabel, view.SpawnLabel);
            RenderReticle(view);
            _alert.gameObject.SetActive(view.Alert);
        }

        /// <summary>글 칸에 넣는다 (null 이면 빈 글).</summary>
        private static void SetText(TMP_Text text, string value) => text.text = value ?? string.Empty;

        private void EnterStage(EFrontWarsTutorialStage stage)
        {
            _stage = stage;

            if (stage == EFrontWarsTutorialStage.Victory)
                _victoryShownAt = _clock;

            if (stage == EFrontWarsTutorialStage.Dead)
            {
                _deathLeft = RestartSeconds;
                _restartSent = false;
                _restartLabel.text = RestartText(RestartSeconds);
            }
        }

        private string RestartText(int seconds) => _text("tutorial2.dead.restart").Replace("{0}", seconds.ToString());

        // ── 칩 [소스 fr]

        private void RenderChip(FrontWarsTutorialView view)
        {
            for (int i = 0; i < _dots.Length; i++)
            {
                Image dot = _dots[i];
                bool used = i < view.ObjectiveCount;
                dot.gameObject.SetActive(used);

                if (used == false)
                    continue;

                // 지금 목표 10px 강조색 · 지난 목표 6px 강조색 60% · 남은 목표 6px 흰색 25%
                float size = i == view.Index ? 10f : 6f;
                dot.rectTransform.sizeDelta = new Vector2(size, size);
                var layout = dot.GetComponent<LayoutElement>();
                if (layout != null)
                {
                    layout.preferredWidth = size;
                    layout.preferredHeight = size;
                }

                dot.color = i == view.Index ? AccentColor
                          : i < view.Index ? new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.6f)
                          : new Color(1f, 1f, 1f, 0.25f);
            }

            SetText(_chipIcon, view.Transitioning ? "✅" : view.Icon);
            SetText(_chipText, view.ChipText);
            _chipText.color = view.Transitioning ? DoneColor : AccentColor;
            _chipBorder.effectColor = view.Transitioning ? new Color(DoneColor.r, DoneColor.g, DoneColor.b, 0.7f) : new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.4f);

            bool progress = view.Progress != null && view.Transitioning == false;
            _chipProgress.gameObject.SetActive(progress);
            if (progress)
                SetText(_chipProgress, view.Progress);
        }

        // ── 안내 카드 [소스 mr · hr]

        private void RenderTip(FrontWarsTutorialView view)
        {
            if (view.TipKey != _tipKey)
            {
                _tipKey = view.TipKey;
                _tipLeft = TipSeconds;
                _tipShownAt = _clock;
            }

            bool show = string.IsNullOrEmpty(view.TipText) == false && (view.TipTimed == false || _tipLeft > 0f);
            _tipCard.gameObject.SetActive(show);

            if (show)
                SetText(_tipText, view.TipText);
        }

        // ── 비추기 [소스 nr]

        private void RenderSpot(FrontWarsTutorialView view)
        {
            if (view.SpotKey != _spotKey)
            {
                _spotKey = view.SpotKey;
                _spotLeft = view.SpotSeconds;
                _spotDone = false;
                _spotShownAt = _clock;
            }

            bool show = view.SpotKey != null && _spotDone == false;
            _spotRoot.gameObject.SetActive(show);

            if (show == false)
                return;

            Vector2 screen = CanvasSize;
            SetText(_spotText, view.SpotText);
            var shade = new Color(0f, 0f, 0f, view.SpotMask);

            // 가리개 — 대상 둘레(여백 12)만 뚫는다. 대상이 없으면 한 장으로 다 덮는다
            if (view.SpotTarget.HasValue)
            {
                Rect t = view.SpotTarget.Value;
                var hole = Rect.MinMaxRect(t.xMin - SpotPad, t.yMin - SpotPad, t.xMax + SpotPad, t.yMax + SpotPad);
                SetBox(_spotMasks[0], Rect.MinMaxRect(0f, 0f, screen.x, hole.yMin), shade);
                SetBox(_spotMasks[1], Rect.MinMaxRect(0f, hole.yMax, screen.x, screen.y), shade);
                SetBox(_spotMasks[2], Rect.MinMaxRect(0f, hole.yMin, hole.xMin, hole.yMax), shade);
                SetBox(_spotMasks[3], Rect.MinMaxRect(hole.xMax, hole.yMin, screen.x, hole.yMax), shade);

                // 강조색 테두리 — 1.4초마다 흐려졌다 돌아온다 (굵기 2 ↔ 3.5) · 모서리는 둥근 10
                float wave = 0.5f - 0.5f * Mathf.Cos(_clock / SpotPulseSeconds * Mathf.PI * 2f);
                float width = Mathf.Lerp(2f, 3.5f, wave);
                float r = SpotHoleRadius;
                var frame = new Color(AccentColor.r, AccentColor.g, AccentColor.b, Mathf.Lerp(1f, 0.5f, wave));
                SetBox(_spotFrame[0], Rect.MinMaxRect(hole.xMin + r, hole.yMin - width * 0.5f, hole.xMax - r, hole.yMin + width * 0.5f), frame);
                SetBox(_spotFrame[1], Rect.MinMaxRect(hole.xMin + r, hole.yMax - width * 0.5f, hole.xMax - r, hole.yMax + width * 0.5f), frame);
                SetBox(_spotFrame[2], Rect.MinMaxRect(hole.xMin - width * 0.5f, hole.yMin + r, hole.xMin + width * 0.5f, hole.yMax - r), frame);
                SetBox(_spotFrame[3], Rect.MinMaxRect(hole.xMax - width * 0.5f, hole.yMin + r, hole.xMax + width * 0.5f, hole.yMax - r), frame);

                // 네 모서리 — 안쪽 원 중심(구멍 모서리에서 r 안)을 기준으로 바깥을 가리고, 호를 그린다
                Vector2[] inner =
                {
                    new Vector2(hole.xMin + r, hole.yMin + r), new Vector2(hole.xMax - r, hole.yMin + r),
                    new Vector2(hole.xMax - r, hole.yMax - r), new Vector2(hole.xMin + r, hole.yMax - r)
                };

                for (int i = 0; i < 4; i++)
                {
                    SetCorner(_spotCorners[i], inner[i], r, i, shade);
                    SetCorner(_spotArcs[i], inner[i], r + SpotArcWidth, i, frame);
                }
            }
            else
            {
                SetBox(_spotMasks[0], Rect.MinMaxRect(0f, 0f, screen.x, screen.y), shade);
                for (int i = 1; i < _spotMasks.Length; i++)
                    _spotMasks[i].gameObject.SetActive(false);
                foreach (RectTransform bar in _spotFrame)
                    bar.gameObject.SetActive(false);
                foreach (Image corner in _spotCorners)
                    corner.gameObject.SetActive(false);
                foreach (Image arc in _spotArcs)
                    arc.gameObject.SetActive(false);
            }

            // 카드 — 기준 상자가 화면 위쪽이면 그 아래, 아래쪽이면 그 위 · 가로는 가운데 맞춤 후 화면 안으로
            Rect? boundary = view.SpotBoundary ?? view.SpotTarget;
            if (boundary.HasValue)
            {
                Rect b = boundary.Value;
                float left = Mathf.Max(8f, Mathf.Min(b.xMin + b.width * 0.5f - SpotCardWidth * 0.5f, screen.x - SpotCardWidth - 8f));
                bool below = b.yMin < screen.y * 0.5f;
                _spotCard.pivot = new Vector2(0f, below ? 1f : 0f);
                _spotCard.anchoredPosition = TopLeftToLocal(new Vector2(left, below ? b.yMax + SpotCardGap : b.yMin - SpotCardGap));
            }
            else
            {
                _spotCard.pivot = new Vector2(0.5f, 1f);
                _spotCard.anchoredPosition = TopLeftToLocal(new Vector2(screen.x * 0.5f, screen.y * 0.26f));
            }

            bool dismissible = view.SpotGotIt != null;
            _gotIt.gameObject.SetActive(dismissible);

            if (dismissible)
            {
                _gotItLabel.text = view.SpotGotIt;
                float progress = view.SpotSeconds > 0f ? 1f - Mathf.Clamp01(_spotLeft / view.SpotSeconds) : 0f;
                _gotItTimer.localScale = new Vector3(progress, 1f, 1f);
            }
        }

        private void SetBox(RectTransform box, Rect topLeftRect, Color color)
        {
            box.gameObject.SetActive(topLeftRect.width > 0f && topLeftRect.height > 0f);
            box.pivot = new Vector2(0f, 1f);
            box.anchoredPosition = TopLeftToLocal(topLeftRect.position);
            box.sizeDelta = topLeftRect.size;
            box.GetComponent<Image>().color = color;
        }

        /// <summary>
        /// 모서리 조각 — 한 변 <paramref name="size"/>, 안쪽 원 중심 <paramref name="inner"/>(화면 왼쪽 위 기준)에서 바깥쪽으로 놓는다.
        /// 그림은 왼쪽 위 모서리 모양이고, 0 · 1 · 2 · 3 (왼위 · 오위 · 오아래 · 왼아래) 차례로 시계 방향 90° 씩 돌린다.
        /// </summary>
        private void SetCorner(Image image, Vector2 inner, float size, int index, Color color)
        {
            float sx = index == 1 || index == 2 ? 1f : -1f;
            float sy = index >= 2 ? 1f : -1f;
            RectTransform rect = image.rectTransform;
            image.gameObject.SetActive(true);
            image.color = color;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.localEulerAngles = new Vector3(0f, 0f, -90f * index);
            rect.anchoredPosition = TopLeftToLocal(inner + new Vector2(sx, sy) * (size * 0.5f));
        }

        private void HandleGotIt() => FinishSpot();

        private void FinishSpot()
        {
            if (_spotDone || _spotKey == null)
                return;

            _spotDone = true;
            _spotRoot.gameObject.SetActive(false);
            OnSpotlightDone?.Invoke(_spotKey);
        }

        // ── 조준경 [소스 sr]

        private void RenderReticle(FrontWarsTutorialView view)
        {
            bool on = view.Reticle && ScreenToLocal(view.ReticleScreen, out Vector2 local) && InsideEdge(local);
            _reticleRoot.gameObject.SetActive(on);

            if (on == false)
                return;

            ScreenToLocal(view.ReticleScreen, out Vector2 at);
            _reticleRoot.anchoredPosition = at;

            Color color = view.ReticleFriendly ? FriendlyColor : EnemyColor;
            Color dot = view.ReticleFriendly ? FriendlyDot : EnemyDot;

            _reticleDashed.color = color;
            foreach (Image tick in _reticleTicks)
                tick.color = color;
            _reticleDotFill.color = dot;
            _reticleLabelBorder.effectColor = color;
            SetText(_reticleLabel, view.ReticleLabel);

            for (int i = 0; i < _reticleRings.Length; i++)
                _reticleRings[i].color = color;
        }

        private bool InsideEdge(Vector2 local)
        {
            Vector2 half = CanvasSize * 0.5f;
            return Mathf.Abs(local.x) <= half.x - ReticleEdge && Mathf.Abs(local.y) <= half.y - ReticleEdge;
        }

        // ── 색종이 [소스 br · xr]

        private void UpdateCelebration(int seq, bool allowed)
        {
            if (seq == _celebrationSeq)
                return;

            _celebrationSeq = seq;

            if (allowed == false || seq <= 0)
                return;

            _celebrationTime = 0f;
            _confettiRoot.gameObject.SetActive(true);
            _confettiRoot.anchoredPosition = TopLeftToLocal(new Vector2(CanvasSize.x * 0.5f, CanvasSize.y * ConfettiTopRatio));
            _confettiMotion.Clear();
            _confettiDelay.Clear();

            // 번호(seq)로 흩어짐이 정해진다 — 같은 완료는 같은 모양
            for (int r = 0; r < _confetti.Length; r++)
            {
                float a = Frac(Math.Sin(seq * 97 + r * 31) * 1e4);
                float s = Frac(Math.Sin(seq * 53 + r * 17) * 1e4);
                float dx = (a - 0.5f) * 320f;
                float dy = -40f - s * 180f;
                float rot = (a - 0.5f) * 720f;
                _confettiMotion.Add(new Vector3(dx, -dy + 220f, rot));
                _confettiDelay.Add(s * 0.1f);
                _confetti[r].color = ConfettiColors[r % ConfettiColors.Length];
            }
        }

        private static float Frac(double value) => (float)(value - Math.Floor(value));

        private void AnimateCelebration()
        {
            if (_celebrationTime < 0f)
                return;

            _celebrationTime += Time.unscaledDeltaTime;

            if (_celebrationTime >= CelebrationSeconds)
            {
                _celebrationTime = -1f;
                _confettiRoot.gameObject.SetActive(false);
                return;
            }

            for (int i = 0; i < _confetti.Length && i < _confettiMotion.Count; i++)
            {
                float t = Mathf.Clamp01((_celebrationTime - _confettiDelay[i]) / ConfettiSeconds);
                float e = EaseOut(t);
                Vector3 m = _confettiMotion[i];
                RectTransform rect = _confetti[i].rectTransform;
                // 화면 아래로 +y 인 원본 좌표 → 유니티는 위가 +y
                rect.anchoredPosition = new Vector2(m.x * e, -m.y * e);
                rect.localEulerAngles = new Vector3(0f, 0f, -m.z * e);
                Color c = _confetti[i].color;
                c.a = 1f - e;
                _confetti[i].color = c;
            }

            // ✅ — 커졌다가(25%) 떠오르며 사라진다(100%)
            float k = Mathf.Clamp01(_celebrationTime / CheckSeconds);
            float scale;
            float rise;
            float alpha;

            if (k < 0.25f)
            {
                float u = EaseOut(k / 0.25f);
                scale = Mathf.Lerp(0.4f, 1.15f, u);
                rise = 0f;
                alpha = u;
            }
            else if (k < 0.7f)
            {
                float u = EaseOut((k - 0.25f) / 0.45f);
                scale = Mathf.Lerp(1.15f, 1f, u);
                rise = Mathf.Lerp(0f, 10f, u);
                alpha = 1f;
            }
            else
            {
                float u = EaseOut((k - 0.7f) / 0.3f);
                scale = Mathf.Lerp(1f, 0.95f, u);
                rise = Mathf.Lerp(10f, 26f, u);
                alpha = 1f - u;
            }

            _check.rectTransform.localScale = Vector3.one * scale;
            _check.rectTransform.anchoredPosition = new Vector2(0f, rise);
            _check.alpha = alpha;
        }

        /// <summary>CSS ease-out (cubic-bezier 0, 0, 0.58, 1) 근사.</summary>
        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            // 매개변수 u 를 이분법으로 찾아 x(u)=t 에 맞춘다
            float lo = 0f;
            float hi = 1f;
            float u = t;

            for (int i = 0; i < 12; i++)
            {
                float x = Bezier(u, 0f, 0.58f);
                if (x < t)
                    lo = u;
                else
                    hi = u;
                u = (lo + hi) * 0.5f;
            }

            return Bezier(u, 0f, 1f);
        }

        private static float Bezier(float u, float p1, float p2)
        {
            float v = 1f - u;
            return 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u;
        }

        // ── 매 프레임 움직임

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _clock += dt;

            if (_tipLeft > 0f)
                _tipLeft -= dt;

            if (_spotKey != null && _spotDone == false && _spotRoot.gameObject.activeSelf && _gotIt.gameObject.activeSelf)
            {
                _spotLeft -= dt;
                if (_spotLeft <= 0f)
                    FinishSpot();
            }

            AnimateCelebration();
            AnimateCardsIn();

            if (_spawnRoot.gameObject.activeSelf)
                AnimateSpawn();

            if (_reticleRoot.gameObject.activeSelf)
                AnimateReticle();

            if (_alert.gameObject.activeSelf)
            {
                float wave = 0.5f - 0.5f * Mathf.Cos(_clock / AlertSeconds * Mathf.PI * 2f);
                _alert.color = new Color(1f, 40f / 255f, 30f / 255f, 0.45f * Mathf.Lerp(0.35f, 1f, wave));
            }

            if (_stage == EFrontWarsTutorialStage.Dead && _restartSent == false)
            {
                _deathLeft -= dt;
                _restartLabel.text = RestartText(Mathf.Max(0, Mathf.CeilToInt(_deathLeft)));

                if (_deathLeft <= 0f)
                    SendRestart();
            }
        }

        private void AnimateSpawn()
        {
            for (int i = 0; i < _spawnRings.Length; i++)
            {
                float t = Pulse(SpawnRingDelays[i], SpawnRingSeconds);
                float e = EaseOut(t);
                _spawnRings[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(0.45f, 1.6f, e);
                _spawnRings[i].color = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 1f - e);
            }

            _spawnDot.rectTransform.localScale = Vector3.one * Breathe(1f, 1.35f, 1f);
            _spawnLabelRoot.anchoredPosition = _spawnLabelBase + new Vector2(0f, Breathe(0f, 3f, SpawnRingSeconds));
        }

        private void AnimateReticle()
        {
            for (int i = 0; i < _reticleRings.Length; i++)
            {
                float e = EaseOut(Pulse(ReticleRingDelays[i], ReticleRingSeconds));
                _reticleRings[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(0.45f, 1.55f, e);
                Color c = _reticleRings[i].color;
                c.a = 1f - e;
                _reticleRings[i].color = c;
            }

            _reticleSpinner.localEulerAngles = new Vector3(0f, 0f, -(_clock % ReticleSpinSeconds) / ReticleSpinSeconds * 360f);
            _reticleDot.rectTransform.localScale = Vector3.one * Breathe(1f, 1.35f, 1f);
            _reticleLabelBg.rectTransform.anchoredPosition = _reticleLabelBase + new Vector2(0f, Breathe(0f, 3f, ReticleRingSeconds));
        }

        /// <summary>카드 나타나기 — 투명 → 불투명 (안내 카드는 6 위에서, 승리 카드는 14 위 · 0.96 배에서).</summary>
        private void AnimateCardsIn()
        {
            _spotCardGroup.alpha = EaseOut((_clock - _spotShownAt) / CardInSeconds);

            float tip = EaseOut((_clock - _tipShownAt) / CardInSeconds);
            _tipGroup.alpha = tip;
            _tipCard.anchoredPosition = _tipBase + new Vector2(0f, TipInRise * (1f - tip));

            float victory = EaseOut((_clock - _victoryShownAt) / VictoryInSeconds);
            _victoryGroup.alpha = victory;
            _victoryCard.anchoredPosition = _victoryBase + new Vector2(0f, VictoryInRise * (1f - victory));
            _victoryCard.localScale = Vector3.one * Mathf.Lerp(VictoryInScale, 1f, victory);
        }

        /// <summary>지연 뒤 반복하는 0→1 진행.</summary>
        private float Pulse(float delay, float period)
        {
            float t = _clock - delay;
            return t < 0f ? 0f : (t % period) / period;
        }

        /// <summary>ease-in-out 왕복 (0%·100% = from, 50% = to).</summary>
        private float Breathe(float from, float to, float period)
        {
            float wave = 0.5f - 0.5f * Mathf.Cos(_clock / period * Mathf.PI * 2f);
            return Mathf.Lerp(from, to, wave);
        }

        // ── 좌표

        private RectTransform Root => (RectTransform)transform;

        private Vector2 CanvasSize => Root.rect.size;

        /// <summary>화면 왼쪽 위 기준 캔버스 좌표 → 이 창 가운데 기준 좌표.</summary>
        private Vector2 TopLeftToLocal(Vector2 topLeft)
        {
            Vector2 size = CanvasSize;
            return new Vector2(topLeft.x - size.x * 0.5f, size.y * 0.5f - topLeft.y);
        }

        /// <summary>화면 px(왼쪽 위 기준) → 이 창 가운데 기준 좌표.</summary>
        private bool ScreenToLocal(Vector2 screenTopLeft, out Vector2 local)
        {
            var canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, new Vector2(screenTopLeft.x, Screen.height - screenTopLeft.y), cam, out local);
        }

        /// <summary>월드 네 귀퉁이 → 화면 왼쪽 위 기준 캔버스 상자 (비추기 대상 · 기준 상자).</summary>
        public Rect CanvasRectOf(Vector3[] worldCorners)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            Vector2 size = CanvasSize;

            foreach (Vector3 corner in worldCorners)
            {
                Vector3 local = Root.InverseTransformPoint(corner);
                var topLeft = new Vector2(local.x + size.x * 0.5f, size.y * 0.5f - local.y);
                min = Vector2.Min(min, topLeft);
                max = Vector2.Max(max, topLeft);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // ── 버튼

        protected override void OnOpened()
        {
            _gotIt.onClick.AddListener(HandleGotIt);
            _keepButton.onClick.AddListener(HandleKeep);
            _realMatchButton.onClick.AddListener(HandleRealMatch);
            _restartButton.onClick.AddListener(SendRestart);
        }

        protected override void OnClosing()
        {
            _gotIt.onClick.RemoveListener(HandleGotIt);
            _keepButton.onClick.RemoveListener(HandleKeep);
            _realMatchButton.onClick.RemoveListener(HandleRealMatch);
            _restartButton.onClick.RemoveListener(SendRestart);
        }

        private void HandleKeep() => OnKeepPlaying?.Invoke();
        private void HandleRealMatch() => OnRealMatch?.Invoke();

        private void SendRestart()
        {
            if (_restartSent)
                return;

            _restartSent = true;
            OnRestart?.Invoke();
        }
    }

    /// <summary>튜토리얼 그림 — 고리 · 점선 고리 · 점 · 안쪽 붉은 빛. 코드로 한 번 만들어 나눠 쓴다 (프리팹에 못 남으니 켤 때 넣는다).</summary>
    public static class FrontWarsTutorialSprites
    {
        private const int Size = 128;
        private const int DashCount = 24;

        /// <summary>안쪽 빛 두께 — inset 0 0 120px 20px `[소스] Sr`. 20 은 꽉 차고 그 안쪽 60 에 걸쳐 흐려진다.</summary>
        private const int GlowSpread = 20;
        private const int GlowBlur = 60;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>테두리 고리 — 두께 = 지름 비율.</summary>
        public static Sprite Ring(float thicknessRatio) => Get($"ring{thicknessRatio:0.000}", (x, y) =>
        {
            float d = Distance(x, y);
            float outer = Size * 0.5f;
            float inner = outer - Size * thicknessRatio;
            return Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner);
        });

        /// <summary>점선 고리 — 24 토막 반반.</summary>
        public static Sprite DashedRing(float thicknessRatio) => Get($"dash{thicknessRatio:0.000}", (x, y) =>
        {
            float d = Distance(x, y);
            float outer = Size * 0.5f;
            float inner = outer - Size * thicknessRatio;
            float angle = Mathf.Atan2(y + 0.5f - Size * 0.5f, x + 0.5f - Size * 0.5f) / (Mathf.PI * 2f) + 0.5f;
            bool dash = Mathf.FloorToInt(angle * DashCount * 2f) % 2 == 0;
            return dash ? Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner) : 0f;
        });

        /// <summary>꽉 찬 원.</summary>
        public static Sprite Dot() => Get("dot", (x, y) => Mathf.Clamp01(Size * 0.5f - Distance(x, y)));

        /// <summary>둥근 구멍 모서리 바깥 — 왼쪽 위 모서리 모양. 원 중심은 그림 오른쪽 아래 끝, 반지름은 한 변.</summary>
        public static Sprite CornerOutside() => Get("corner", (x, y) =>
        {
            float dx = Size - (x + 0.5f);
            float dy = y + 0.5f;
            return Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) - Size + 0.5f);
        });

        /// <summary>둥근 테두리 호 — 왼쪽 위 모서리 모양. 한 변 = 반지름 + 굵기, 반지름 자리에 굵기만큼 띠.</summary>
        public static Sprite CornerArc(float radius, float width) => Get($"arc{radius:0.##}_{width:0.##}", (x, y) =>
        {
            float unit = Size / (radius + width);
            float dx = Size - (x + 0.5f);
            float dy = y + 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float inner = (radius - width * 0.5f) * unit;
            float outer = (radius + width * 0.5f) * unit;
            return Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner);
        });

        /// <summary>화면 가장자리 안쪽 빛 — 9조각으로 늘린다.</summary>
        public static Sprite InsetGlow()
        {
            const string key = "glow";

            if (Cache.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;

            int border = GlowSpread + GlowBlur;
            int size = border * 2 + 2;
            var texture = NewTexture(size, key);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                    float a = edge < GlowSpread ? 1f : Mathf.Clamp01(1f - (edge - GlowSpread) / (float)GlowBlur);
                    a *= a;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            Cache[key] = sprite;
            return sprite;
        }

        private static float Distance(int x, int y)
        {
            float dx = x + 0.5f - Size * 0.5f;
            float dy = y + 0.5f - Size * 0.5f;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static Sprite Get(string key, Func<int, int, float> alpha)
        {
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;

            var texture = NewTexture(Size, key);
            var pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha(x, y) * 255f));

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            Cache[key] = sprite;
            return sprite;
        }

        private static Texture2D NewTexture(int size, string name) => new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = $"FrontWarsTutorial_{name}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
    }
}
