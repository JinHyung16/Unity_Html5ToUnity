using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// ★★★ 골인 순간 <b>골대에 붙는</b> 두 연출 — 원본 <c>FXwinLight</c> ×2 · <c>FXteleport</c> ×1.
    ///
    /// <para>
    /// <b>왜 새로 만드나.</b> 23회차가 골 순간 FX 를 훅으로 붙잡아 텍스처·알파·크기를 전부 실측했는데
    /// (<c>재발방지 #115</c> — 인스턴스가 없으면 클래스에도 애니가 안 보인다),
    /// <b>우리 이관본에는 이 두 오브젝트 «자체»가 없었다.</b> 있던 것은 <c>FXflash</c>(흰 플래시) ·
    /// <c>FXconfettis</c>(컨페티) · <c>Sprite</c>(<c>YES!</c>) 셋뿐이다 [읽어서 확인 · 패스 ②-h].
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>컨페티와 달리 이 둘은 «골대»에 붙는다.</b> 캐논 2문은 좌표가 고정이지만
    /// 이 둘은 <b>레벨마다 다른 골대 좌표</b>를 따라간다 — 5레벨 전수 대조로 확증했다 (아래).
    /// </para>
    ///
    /// <list type="table">
    /// <item><term>레이어</term><description>둘 다 원본 <c>FXBottom</c>(레이어 index 1) —
    /// <b><c>Collisions</c>(블록·공·골대)보다 «아래»</b>다 [실측 · 23회차 <c>layers_A.json</c>].
    /// 그래서 정렬 순서를 <c>ArrowRestart</c>(같은 <c>FXBottom</c>) 옆에 둔다.</description></item>
    /// <item><term>텍스처</term><description><c>FXwinLight</c> 는 <b>1 × 1 순백 불투명 1픽셀</b>(TiledBackground) ⇒
    /// <b>그림을 굽지 않는다</b> — <c>solid</c> 를 늘려 쓴다. <c>FXteleport</c> 만 6프레임이다.</description></item>
    /// <item><term>색</term><description>둘 다 <b>틴트가 «없다»</b> [실측 — 채록의 색 필드가 전부 <c>null</c>] ⇒
    /// 순백 그대로다. 결과 색을 여러 벌 굽지 않는다 (<c>재발방지 #94</c>).</description></item>
    /// </list>
    ///
    /// <para>
    /// ★★ <b>「어디에 붙나」는 «유도»했다 — 대응표가 아니다</b> (<c>재발방지 #53</c>).
    /// 23회차 5레벨 채록의 생성 프레임 bbox 를 그 레벨의 <c>GoalRimX/Y</c> 와 맞대니
    /// <b>5/5 가 소수점까지 떨어졌다</b>:
    /// </para>
    ///
    /// <code>
    /// FXwinLight : «아래 모서리»가 (GoalRimX, GoalRimY)      ← 5레벨 전부 bottom = cy + h/2 = GoalRimY
    /// FXteleport : «중심»이     (GoalRimX, GoalRimY − 70)    ← 5레벨 전부 정확히 −70
    ///
    ///   레벨 | 우리 GoalRim | winLight bottom | teleport 중심
    ///   W1L1 |  825, 977    |  825, 977       |  825, 907
    ///   W1L2 |  875, 1050   |  875, 1050      |  875, 980
    ///   W1L3 |  450, 775    |  450, 775       |  450, 705
    ///   W1L4 |  475, 925    |  475, 925       |  475, 855
    ///   W1L5 |  950, 1050   |  950, 1050      |  950, 980
    /// </code>
    ///
    /// <para>
    /// ⇒ <b>레벨마다 좌표를 박지 않는다.</b> 골대 좌표 하나만 받아 두 오프셋으로 세운다.
    /// </para>
    /// </summary>
    public sealed class BlumgiGoalBurst : MonoBehaviour
    {
        /// <summary>빛줄기 개수 — <b>2</b> [실측 5레벨 전부 2개].</summary>
        public const int WinLightCount = 2;

        /// <summary>
        /// <c>FXteleport</c> 애니 프레임 수 — <b>6 @ speed 20</b> [실측 · 프레임 전환이 ≈50 ms 등간격].
        /// 6 / 20 = <b>0.300 s</b> 이고 실측 소멸이 <b>307.4 ~ 309.3 ms</b> 다 (rAF 한 프레임 뒤에 사라진다).
        /// </summary>
        public const int TeleportFrameCount = 6;

        /// <summary>
        /// 굽는 프레임 수 — <b>5</b>. <b>f5 는 «완전 투명»(불투명 픽셀 0)</b>이라 그림이 없다 [실측] ⇒
        /// 마지막 50 ms 는 «수명은 살아 있는데 안 보이는» 구간이고, 그것이 원본 그대로다.
        /// </summary>
        public const int TeleportSpriteCount = 5;

        /// <summary>
        /// <c>FXteleport</c> 표시 배율 — <b>×2</b> [실측 · 소스 128 → 표시 256 · 소스 256 → 표시 512].
        /// <b>프레임마다 다른 «크기 트윈»이 아니다</b> — 소스 크기가 f0 만 128 이라 그렇게 보였던 것이다.
        /// </summary>
        public const float TeleportImageScale = 2f;

        [Header("요소")]

        /// <summary>빛줄기 2개. 원본이 <b>1×1 순백</b>이라 <c>solid</c> 를 <c>Sliced</c> 로 늘린다.</summary>
        [SerializeField] private SpriteRenderer[] _winLights;

        [SerializeField] private SpriteRenderer _teleport;

        /// <summary>텔레포트 6프레임 중 «그림이 있는» 5장. f5 는 없다 (완전 투명).</summary>
        [SerializeField] private Sprite[] _teleportFrames;

        [Header("FXwinLight [실측 · 23회차 life_B2 61행 + 5레벨 생성 프레임]")]

        /// <summary>
        /// ★ 불투명도 — <b>0.300 «고정»</b> [실측 · 수명 61행 전부 0.3000 · 변동 0].
        /// <b>사라지는 것은 알파가 아니라 «크기가 닫히기» 때문</b>이다.
        /// </summary>
        [SerializeField] private float _winLightAlpha = 0.300f;

        /// <summary>폭 <b>50 → 0</b> [실측 · 마지막 표본 0.066 · 0.013].</summary>
        [SerializeField] private float _winLightWidthStartWorld = 50f;

        [SerializeField] private float _winLightWidthEndWorld;

        /// <summary>
        /// 높이 <b>1550 → 1000</b> [실측].
        /// ⚠ <c>05_연출</c> 의 「<c>Tween size 0 → 1000</c>」은 <b>방향이 반대였다</b> —
        /// 그대로 옮기면 <b>광선이 «커지면서» 나타난다</b>.
        /// </summary>
        [SerializeField] private float _winLightHeightStartWorld = 1550f;

        [SerializeField] private float _winLightHeightEndWorld = 1000f;

        /// <summary>
        /// 트윈 시간 — <b>0.4974 s</b> [실측 · <c>FXflash</c> 와 <b>같은 값</b>].
        /// 실측 소멸이 골 <b>+499.3 ~ 509.1 ms</b>(1번) · <b>+691.7 ~ 700.5 ms</b>(2번 · 200 ms 늦게 뜬다) 다.
        /// </summary>
        [SerializeField] private float _winLightSeconds = 0.4974f;

        [Header("FXteleport [실측 · 소스 픽셀 직독 + 프레임 전환 시각]")]

        /// <summary>프레임 속도 — <b>20 fps</b> [실측 · 전환 55.8 / 106.4 / 155.9 / 211.0 / 257.4 ms].</summary>
        [SerializeField] private float _teleportFramesPerSecond = 20f;

        /// <summary>
        /// 골대 기준 y 오프셋 — <b>−70</b> (원본 y-down 이라 «위»로 70) [실측 5/5 · 오차 0].
        /// </summary>
        [SerializeField] private float _teleportOffsetYWorld = -70f;

        /// <summary>원본 world 좌표를 얹을 좌표 프레임 (블록·골대와 같은 것). 배선이 넣는다.</summary>
        private Transform _worldFrame;

        private double _anchorWorldX;
        private double _anchorWorldY;

        /// <summary>빛줄기별 경과 시간(초). 음수면 «아직 안 떴다».</summary>
        private readonly float[] _winLightTimer = { -1f, -1f };

        private float _teleportTimer = -1f;

        /// <summary>골 이후 지난 시간(초). 검사가 이것을 본다.</summary>
        public float ElapsedSeconds { get; private set; } = -1f;

        /// <summary>지금 «보이는» 빛줄기 수. 검사가 「0 → 1 → 2 → 1 → 0」을 이것으로 본다.</summary>
        public int LiveWinLightCount
        {
            get
            {
                if (_winLights == null)
                    return 0;

                int n = 0;

                for (int i = 0; i < _winLights.Length; i++)
                {
                    if (_winLights[i] != null && _winLights[i].gameObject.activeSelf)
                        n++;
                }

                return n;
            }
        }

        /// <summary>
        /// 텔레포트가 «살아 있나». ⚠ <b>보이는 것과 다르다</b> — f5(마지막 50 ms)는 살아 있는데 안 보인다.
        /// </summary>
        public bool IsTeleportLive
        {
            get { return _teleportTimer >= 0f; }
        }

        /// <summary>텔레포트의 지금 프레임 (0~5). 안 살아 있으면 −1.</summary>
        public int TeleportFrame
        {
            get
            {
                if (_teleportTimer < 0f)
                    return -1;

                return Mathf.Clamp(Mathf.FloorToInt(_teleportTimer * _teleportFramesPerSecond),
                                   0, TeleportFrameCount - 1);
            }
        }

        /// <summary>텔레포트 표시 한 변 (원본 world px) — f0 만 256 이고 나머지는 512 다 [실측].</summary>
        public float TeleportDisplayWorld
        {
            get
            {
                if (_teleport == null || _teleport.sprite == null)
                    return 0f;

                return _teleport.sprite.rect.width * TeleportImageScale;
            }
        }

        /// <summary>
        /// 텔레포트 중심의 원본 world y. <b>골대 y − 70 에서 안 움직여야 한다</b> [실측 5레벨 5/5].
        /// </summary>
        public double TeleportCenterWorldY
        {
            get
            {
                if (_teleport == null || _teleport.gameObject.activeSelf == false || _worldFrame == null)
                    return double.NaN;

                Vector3 local = _worldFrame.InverseTransformPoint(_teleport.transform.position);
                return -local.y * (double)BlumgiUnits.WorldPixelsPerUnit;
            }
        }

        /// <summary>빛줄기 i 가 «보이나».</summary>
        public bool IsWinLightLive(int index)
        {
            return _winLights != null
                   && index >= 0 && index < _winLights.Length
                   && _winLights[index] != null
                   && _winLights[index].gameObject.activeSelf;
        }

        /// <summary>빛줄기 i 의 지금 폭 (원본 world px). 검사가 <b>트윈 시작/끝</b>을 이것으로 본다.</summary>
        public float WinLightWidthWorld(int index)
        {
            return IsWinLightLive(index) ? (float)BlumgiUnits.ToWorldLength(_winLights[index].size.x) : 0f;
        }

        /// <summary>빛줄기 i 의 지금 높이 (원본 world px).</summary>
        public float WinLightHeightWorld(int index)
        {
            return IsWinLightLive(index) ? (float)BlumgiUnits.ToWorldLength(_winLights[index].size.y) : 0f;
        }

        /// <summary>빛줄기 i 의 지금 알파. <b>0.300 에서 «안 움직여야» 한다</b> [실측].</summary>
        public float WinLightAlpha(int index)
        {
            return IsWinLightLive(index) ? _winLights[index].color.a : 0f;
        }

        /// <summary>
        /// 빛줄기 i 의 «아래 모서리» 원본 world y. <b>골대 y 에서 안 움직여야 한다</b> [실측 · 61행 전부 977.00].
        /// </summary>
        public double WinLightBottomWorldY(int index)
        {
            if (IsWinLightLive(index) == false || _worldFrame == null)
                return double.NaN;

            Vector3 local = _worldFrame.InverseTransformPoint(_winLights[index].transform.position);
            double centerWorldY = -local.y * (double)BlumgiUnits.WorldPixelsPerUnit;

            return centerWorldY + WinLightHeightWorld(index) * 0.5;
        }

        private void Awake()
        {
            Stop();
        }

        /// <summary>
        /// 원본 world 좌표계를 얹을 프레임. <b>블록·골대와 같은 것</b>이어야 골대에 붙는다.
        /// </summary>
        public void SetWorldFrame(Transform worldFrame)
        {
            _worldFrame = worldFrame;
        }

        /// <summary>
        /// 골대 좌표 — <b>레벨이 정한다</b> (<c>BlumgiLevelData.GoalRimX/Y</c>).
        /// ⚠ 여기에 레벨별 좌표를 «표»로 들고 있지 않다 — 그러면 레벨이 늘 때마다 두 곳을 고쳐야 한다.
        /// </summary>
        public void SetAnchorWorld(double rimWorldX, double rimWorldY)
        {
            _anchorWorldX = rimWorldX;
            _anchorWorldY = rimWorldY;
        }

        /// <summary>
        /// 골 «그 프레임» — <c>FXwinLight</c> 1번 + <c>FXteleport</c> 가 <b>같이</b> 뜬다 [실측 둘 다 0 ms].
        /// ⚠ 2번 빛줄기는 여기서 안 띄운다 — 원본은 <b>컨페티와 «같은 이벤트 행»</b>이라
        /// <see cref="PlaySecondWinLight"/> 를 <see cref="BlumgiClearOverlay"/> 가 그 시각에 부른다.
        /// </summary>
        public void Play()
        {
            ElapsedSeconds = 0f;

            _teleportTimer = 0f;
            ApplyTeleport();

            _winLightTimer[0] = 0f;
            _winLightTimer[1] = -1f;

            ApplyWinLight(0);
            HideWinLight(1);
        }

        /// <summary>
        /// 2번 빛줄기 — 골 <b>+200 ms</b> [실측 5레벨 199.8 ~ 208.7 ms].
        /// 원본에서 이 행은 <b>「컨페티 100 + 콘 2 + <c>FXwinLight</c> 1→2」가 «한 줄»</b>이다.
        /// </summary>
        public void PlaySecondWinLight()
        {
            if (ElapsedSeconds < 0f)
                return;

            _winLightTimer[1] = 0f;
            ApplyWinLight(1);
        }

        /// <summary>전부 지운다. 원본에서 남은 것을 지우는 것은 <b>레벨 전환</b>뿐이다.</summary>
        public void Stop()
        {
            ElapsedSeconds = -1f;
            _teleportTimer = -1f;

            for (int i = 0; i < _winLightTimer.Length; i++)
                _winLightTimer[i] = -1f;

            if (_teleport != null)
                _teleport.gameObject.SetActive(false);

            if (_winLights == null)
                return;

            for (int i = 0; i < _winLights.Length; i++)
                HideWinLight(i);
        }

        private void Update()
        {
            if (ElapsedSeconds < 0f)
                return;

            float dt = Time.unscaledDeltaTime;
            ElapsedSeconds += dt;

            // ── 텔레포트 — 6프레임을 20 fps 로 돌고 «끝나면 사라진다» [실측 297.9 ms 까지 관측].
            if (_teleportTimer >= 0f)
            {
                _teleportTimer += dt;

                if (_teleportTimer * _teleportFramesPerSecond >= TeleportFrameCount)
                {
                    _teleportTimer = -1f;

                    if (_teleport != null)
                        _teleport.gameObject.SetActive(false);
                }
                else
                {
                    ApplyTeleport();
                }
            }

            // ── 빛줄기 2개 — 알파는 안 건드리고 «크기만» 닫는다 [실측].
            for (int i = 0; i < _winLightTimer.Length; i++)
            {
                if (_winLightTimer[i] < 0f)
                    continue;

                _winLightTimer[i] += dt;

                if (_winLightTimer[i] >= _winLightSeconds)
                {
                    _winLightTimer[i] = -1f;
                    HideWinLight(i);
                    continue;
                }

                ApplyWinLight(i);
            }
        }

        /// <summary>
        /// ★★ 빛줄기 한 개의 지금 모습.
        ///
        /// <para>
        /// <b>기전 셋을 그대로 옮긴다</b> (<c>재발방지 #53</c> — 파생 규칙이 아니라 기전이다):
        /// </para>
        ///
        /// <list type="number">
        /// <item><b>아래 모서리가 골대에 «못 박혀 있다»</b> — 실측 61행 전부 <c>bottom = 977.00</c> 으로
        ///       소수점까지 불변이고, 중심 y 가 202 → 476.6 으로 «움직인 것»은 높이가 닫힌 결과다</item>
        /// <item><b>폭과 높이가 «같은 진행값»으로 움직인다</b> — 실측에서 폭 진행률과 높이 진행률이
        ///       61행 전부 소수 4자리까지 같다 ⇒ 트윈 파라미터가 <b>하나</b>다</item>
        /// <item><b>이징은 «사인 ease-in»</b> — 61행에 사인 ease-in 을 맞추면 최대오차 <b>0.7 %</b>,
        ///       선형은 <b>15 %</b>·2차는 <b>3.5 %</b> 로 배제된다 [23회차 원시 채록 재분석]</item>
        /// </list>
        /// </summary>
        private void ApplyWinLight(int index)
        {
            if (_winLights == null || index >= _winLights.Length || _winLights[index] == null)
                return;

            SpriteRenderer renderer = _winLights[index];

            float u = _winLightSeconds <= 0f ? 1f : Mathf.Clamp01(_winLightTimer[index] / _winLightSeconds);
            float s = 1f - Mathf.Cos(u * Mathf.PI * 0.5f);   // 사인 ease-in [실측]

            float widthWorld = Mathf.LerpUnclamped(_winLightWidthStartWorld, _winLightWidthEndWorld, s);
            float heightWorld = Mathf.LerpUnclamped(_winLightHeightStartWorld, _winLightHeightEndWorld, s);

            renderer.size = new Vector2(BlumgiUnits.ToUnits(widthWorld), BlumgiUnits.ToUnits(heightWorld));

            // 아래 모서리를 골대에 붙인 채 «중심»을 올린다 — 원본은 y 가 아래라 중심이 rim − h/2 다.
            SetLocal(renderer.transform, _anchorWorldX, _anchorWorldY - heightWorld * 0.5);

            var color = Color.white;
            color.a = _winLightAlpha;
            renderer.color = color;

            if (renderer.gameObject.activeSelf == false)
                renderer.gameObject.SetActive(true);
        }

        private void ApplyTeleport()
        {
            if (_teleport == null)
                return;

            int frame = TeleportFrame;

            // f5 는 그림이 «없다» — 살아 있지만 안 보이는 것이 원본이다 [실측 불투명 픽셀 0].
            Sprite sprite = _teleportFrames != null && frame >= 0 && frame < _teleportFrames.Length
                ? _teleportFrames[frame]
                : null;

            _teleport.sprite = sprite;
            _teleport.color = Color.white;
            _teleport.transform.localScale = Vector3.one * TeleportImageScale;

            SetLocal(_teleport.transform, _anchorWorldX, _anchorWorldY + _teleportOffsetYWorld);

            if (_teleport.gameObject.activeSelf == false)
                _teleport.gameObject.SetActive(true);
        }

        private void HideWinLight(int index)
        {
            if (_winLights == null || index >= _winLights.Length || _winLights[index] == null)
                return;

            _winLights[index].gameObject.SetActive(false);
        }

        /// <summary>원본 world 좌표 → 좌표 프레임 위의 자리. 프레임이 없으면 자기 자리를 기준으로 쓴다.</summary>
        private void SetLocal(Transform target, double worldX, double worldY)
        {
            Vector3 local = BlumgiUnits.ToPosition(worldX, worldY);

            target.position = _worldFrame == null
                ? transform.TransformPoint(local)
                : _worldFrame.TransformPoint(local);
        }
    }
}
