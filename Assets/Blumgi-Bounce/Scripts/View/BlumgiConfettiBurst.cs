using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 클리어 컨페티.
    ///
    /// <para>
    /// ★★★ <b>[정정 · 8회차] 「골대 부근에서 위로 솟구친다」가 아니다 — «화면 밖 좌우 바닥의 캐논 2문»이다.</b>
    /// 클리어 순간 런타임 인스턴스를 세어 통째로 닫았다 [실측 §3-e]:
    /// </para>
    ///
    /// <list type="table">
    /// <item><term>언제</term><description><b>골 + 200 ms</b> (시트 <c>Wait 0.2</c> 직후)</description></item>
    /// <item><term>무엇이</term><description><b><c>FXconfettis</c> 100 개</b> + <c>FXConfettisCones</c> 2 개(캐논 본체)</description></item>
    /// <item><term>어디서</term><description>화면 <b>밖</b> 좌우 바닥 — 좌 <b>(−455, 1247)</b> · 우 <b>(1732, 1247)</b></description></item>
    /// <item><term>발사각</term><description>좌 <b>300°</b> · 우 <b>240°</b> (원본 y-down 기준 = 각각 안쪽 위로)</description></item>
    /// <item><term>소멸</term><description><b>없다</b> — 골 +2 213 ms 의 <b>레벨 전환이 통째로 지운다</b> (100 → 0)</description></item>
    /// </list>
    ///
    /// <para>
    /// ⇒ 1회차의 「이미터 4종 중 어느 것인지 미측정」은 <b><c>FXconfettis</c> 하나</b>로 닫혔고
    /// (<c>~Special</c>·<c>~360</c> 는 인스턴스 0), 「소멸 시점 미측정」의 답은 <b>«소멸이 없다»</b> 였다 —
    /// 가려진 것이 아니라 <b>전환이 곧 소멸</b>이다.
    /// </para>
    ///
    /// <para>
    /// 색 <b>4종</b> [실측] — <c>#FF709B</c> 분홍 · <c>#FCFF47</c> 노랑 · <c>#7DFF69</c> 연두 · <c>#7D64FF</c> 보라.
    /// 조각은 <b>흰색으로 굽고 tint</b> 한다 (확정 F).
    /// </para>
    ///
    /// <para>
    /// ★★ <b>[해소 · 23회차] 조각 100개를 «전수»로 240프레임씩 추적</b>해 크기·속도가 닫혔다:
    /// </para>
    ///
    /// <list type="bullet">
    /// <item>소스 텍스처 <b>52 × 52 · 6f@10 · 순백 단색 마스크</b> — 색 4종은 <b>전부 런타임 틴트</b>다</item>
    /// <item>표시 크기 <b>정사각 (w = h) · 26.1 ~ 36.8 world px · 조각마다 다르고 수명 내내 «불변»</b></item>
    /// <item>초기 수직 속도 <b>중앙 −1342 px/s (위로)</b> · 범위 −1872 ~ −907</item>
    /// <item>수평 속도 <b>중앙 +363 px/s</b> · 범위 −1206 ~ +1255 (좌우로 부챗살)</item>
    /// <item><b>낙하 가속 중앙 +400 px/s²</b> (범위 349 ~ 460) — <b>중력 1500 이 «아니다»</b>. 컨페티는 물리체가 아니다</item>
    /// <item>★ <b>각도는 조각마다 «고정»이고 «회전하지 않는다»</b> — 관측값이 <b>0 / 2 / 4 / 6 rad</b> 네 값에 몰린다</item>
    /// </list>
    ///
    /// <para>
    /// ★★★ <b>[해소 · 25회차] 「구동 비헤이비어 상수 직독」이 닫혔다 — 컨페티는 «파티클 이미터»가 낸다.</b>
    /// </para>
    ///
    /// <para>
    /// <c>FXConfettisCones</c> 는 <b>스프라이트가 아니라 Construct 「Particles」 플러그인</b>이다
    /// (<c>GetParticleEngine</c>·<c>_SetParticleObjectClass</c> 가 있고 <c>_animations</c> 가 «없다» —
    /// 23회차가 「소스 없음」으로 막혔던 이유가 이것이다). 소스 텍스처는 <b>1 × 1 (255,255,255,128)</b> 이고
    /// <c>_hasAnyDefaultParticle = false</c> 라 <b>쓰이지도 않는다</b> ⇒ <b>그릴 것이 없는 «이미터 설정»</b>이다.
    /// ⇒ 우리 쪽에서 «캐논 그림»을 굽는 것이 아니라 <b>이 클래스가 곧 그 이미터</b>다.
    /// </para>
    ///
    /// <para>엔진 파라미터 전수 [실측 · <c>GetDebuggerProperties()</c> + <c>GetParticleEngine()</c> · 두 문 동일]:</para>
    ///
    /// <list type="bullet">
    /// <item><b>one-shot</b> · <b>Rate 50</b> ⇒ <b>컨페티 100 개 = 50 × 2 문</b>이다 (시트가 따로 만들지 않는다)</item>
    /// <item><b>Spray cone 30.0°</b> ⇒ 분사각 <b>±15°</b> — 실측 파티클 각 288.7° ~ 303.3° 와 맞는다</item>
    /// <item><b>Speed 1500</b> · <b>Initial speed randomiser 800</b> ⇒ <b>1500 ± 400</b>
    ///       (실측 파티클 속력 1279 ~ 1696 이 그 안에 든다)</item>
    /// <item><b>Gravity 400</b> ⇒ ★ 23회차의 「낙하 가속 «중앙» 400 (349~460 산포)」은
    ///       <b>저작값 그대로였고 산포는 측정 잡음</b>이었다</item>
    /// <item><b>Timeout 5 s</b> — 다만 <b>레벨 전환(골 +2.2 s)이 먼저 온다</b>. 화면에서는 도달하지 않는다</item>
    /// <item>spawn object = <c>FXconfettis</c> · Angle/Opacity/Size randomiser <b>0</b> · Acceleration <b>0</b></item>
    /// </list>
    ///
    /// <para>
    /// ⚠ 엔진의 <c>Size 100</c> 은 <b>기본 파티클</b>의 크기라 여기 안 쓴다 —
    /// 화면에 뜨는 것은 <c>FXconfettis</c> 스프라이트이고 그 표시 크기는 <b>실측 26.1 ~ 36.8 world</b> 다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>자연 소멸 시각은 여전히 미측정</b>이다 — 레이아웃 전이가 먼저 와서 잘린다.
    /// <c>Timeout 5 s</c> 는 <b>파티클</b> 수명이지 «오브젝트» 수명이 아니다.
    /// </para>
    /// </summary>
    public sealed class BlumgiConfettiBurst : MonoBehaviour
    {
        /// <summary>조각 수 [8회차 실측 · 클리어 직후 <c>FXconfettis</c> 인스턴스 100].</summary>
        public const int OriginPieceCount = 100;

        /// <summary>
        /// 캐논 2문의 원본 world 좌표 [실측 8회차]. <b>화면(1280×1280) 밖</b>이라 캐논 자체는 안 보인다.
        ///
        /// <para>
        /// ⚠ 25회차 재측정은 <b>(−443, 1255) · (1731, 1255)</b> 로 나왔다 — 차이 12 px 는
        /// <b>골 셰이크(±10~19 px)</b> 안이라 «다른 값»이 아니다. 어느 쪽도 셰이크가 섞인 실측이라
        /// <b>점수를 근거로 고르지 않는다</b> — 기존 값을 유지한다 (재발방지 #48 · #100).
        /// </para>
        /// </summary>
        private static readonly Vector2[] CannonWorld =
        {
            new Vector2(-455f, 1247f),
            new Vector2(1732f, 1247f),
        };

        /// <summary>캐논 발사각 (원본 y-down 도) [실측]. 좌 300° · 우 240°.</summary>
        private static readonly float[] CannonAngleDeg = { 300f, 240f };

        /// <summary>조각이 캐논 입구에 뭉쳐 있는 범위 (원본 world px) [실측 x ≈ 5 · y ≈ 7].</summary>
        private const float MouthSpreadXWorld = 5f;

        private const float MouthSpreadYWorld = 7f;

        /// <summary>조각 팔랑임 — <c>Sine</c> + <c>Sine2</c> [실측 <c>period 4</c> · <c>mag 50</c>].</summary>
        private const float FlutterPeriodSeconds = 4f;

        private const float FlutterMagnitudeWorld = 50f;

        /// <summary>
        /// ★★ 조각 «뒤집기» 애니의 프레임별 <b>폭 배율</b> [실측 · 23회차 소스 텍스처 6프레임 직독].
        ///
        /// <para>
        /// 소스는 <b>52 × 52 순백 6프레임</b>인데 <b>세로는 6프레임 전부 52 로 안 변하고 «가로»만</b>
        /// <c>52 · 52 · 52 · 30 · 6 · 26</c> 으로 변한다 — <b>세로로 세운 카드가 도는 그림</b>이다.
        /// ⇒ 「팔랑인다」의 정체가 <b>회전이 아니라 이 6프레임</b>이었다.
        /// </para>
        /// </summary>
        private static readonly float[] FlipWidthRatio =
        {
            52f / 52f, 52f / 52f, 52f / 52f, 30f / 52f, 6f / 52f, 26f / 52f,
        };

        /// <summary>뒤집기 재생 속도 [실측 <c>Animation 1 · 6f @ speed 10</c>].</summary>
        private const float FlipFramesPerSecond = 10f;

        /// <summary>
        /// 굽는 조각 텍스처의 한 변 (픽셀) — <b>실측 소스와 같은 52</b> (<c>BlumgiSpriteBuilder</c>).
        /// <c>localScale</c> 로 크기를 정하려면 <b>스프라이트의 «자연 크기»를 되돌려야</b> 한다.
        /// </summary>
        private const float PieceSourcePixels = 52f;

        /// <summary>
        /// ★ 조각 각도 — <b>조각마다 고정이고 «회전하지 않는다»</b> [실측 · 23회차].
        /// 관측값이 <b>0 / 2 / 4 / 6 rad</b> 네 값에 몰린다 ⇒ 도(°)로 <b>0 / 114.59 / 229.18 / 343.77</b>.
        /// </summary>
        private static readonly float[] FixedAngleDegrees =
        {
            0f,
            2f * Mathf.Rad2Deg,
            4f * Mathf.Rad2Deg,
            6f * Mathf.Rad2Deg,
        };

        [Header("조각 — 흰색으로 굽고 tint 한다")]
        [SerializeField] private SpriteRenderer[] _pieces;

        [Header("색 4종 [실측]")]
        [SerializeField] private Color[] _colors;

        [Header("발사 · 낙하 [실측 · 23회차 · 조각 100개 전수 240프레임]")]

        /// <summary>
        /// 발사 속력 (원본 world px/s) — ★ <b>저작값 <c>Speed = 1500</c></b> [실측 25회차 · 파티클 엔진 직독].
        ///
        /// <para>
        /// ⚠ 예전 값 <b>1550</b> 은 <b>실측 v0y 중앙 −1342 ÷ sin 60°</b> 로 «유도»한 것이었다 —
        /// 유도가 저작값의 <b>3.3 %</b> 안에 들었지만, <b>저작값이 나왔으면 저작값을 쓴다</b>.
        /// </para>
        /// </summary>
        [SerializeField] private float _launchSpeedWorld = 1500f;

        /// <summary>
        /// 속력 산포 (원본 world px/s · <b>전폭</b>) — ★ 저작값 <c>Initial speed randomiser = 800</c>
        /// ⇒ <b>1500 ± 400</b>. 실측 파티클 속력 <b>1279 ~ 1696</b> 이 그 안에 든다.
        /// </summary>
        [SerializeField] private float _speedRandomiserWorld = 800f;

        /// <summary>
        /// 확산 각도 (<b>전폭</b> 도) — ★ 저작값 <c>Spray cone = 30.0°</c> (0.5236 rad) ⇒ <b>±15°</b>.
        ///
        /// <para>
        /// ⚠ 예전 값 <b>70</b> 은 «관측 <c>vx</c> 분포를 덮는 폭»이었다. 실측 파티클 각이
        /// <b>288.7° ~ 303.3° = 300° ± 15°</b> 라 저작값과 정확히 맞는다.
        /// </para>
        /// </summary>
        [SerializeField] private float _spreadDegrees = 30f;

        /// <summary>
        /// ★ <b>낙하 가속 400 px/s²</b> — <b>저작값 <c>Gravity = 400</c></b> [실측 25회차 · 엔진 직독].
        ///
        /// <para>
        /// 23회차가 «중앙값 400 · 범위 349~460» 으로 잡아 둔 것이 <b>저작값 그대로</b>였고
        /// <b>산포는 측정 잡음</b>이었다 — 값은 이미 맞았으므로 <b>고치지 않았다</b>.
        /// ⚠ 그 전 값 <b>1500</b> 은 «물리 중력»을 가져다 쓴 것이었다 — 컨페티는 물리체가 아니다.
        /// </para>
        /// </summary>
        [SerializeField] private float _gravityWorld = 400f;

        /// <summary>
        /// 조각 표시 크기 (원본 world px · 정사각) — 조각마다 이 범위에서 «고정»된다 [실측 26.1 ~ 36.8].
        /// </summary>
        [SerializeField] private float _pieceSizeMinWorld = 26.1f;

        [SerializeField] private float _pieceSizeMaxWorld = 36.8f;

        private Vector3[] _velocity;
        private Vector3[] _anchor;

        /// <summary>조각별 표시 크기 (유니티 유닛 · 정사각) — <b>수명 내내 불변</b> [실측].</summary>
        private float[] _pieceUnits;

        /// <summary>조각별 애니 «위상» (프레임) — 조각마다 다른 데서 시작한다 [실측].</summary>
        private float[] _framePhase;

        /// <summary>원본 world 좌표를 얹을 좌표 프레임 (블록·골대와 같은 것). 배선이 넣는다.</summary>
        private Transform _worldFrame;

        private float _timer = -1f;

        /// <summary>터진 뒤 지난 시간(초). 음수면 안 터졌다. <b>검사가 이것을 본다</b>.</summary>
        public float ElapsedSeconds
        {
            get { return _timer; }
        }

        /// <summary>지금 화면에 떠 있는 조각 수. <b>검사가 「100개인가 · 안 사라지는가」를 이것으로 본다</b>.</summary>
        public int LivePieceCount
        {
            get
            {
                if (_pieces == null)
                    return 0;

                int n = 0;

                for (int i = 0; i < _pieces.Length; i++)
                {
                    if (_pieces[i] != null && _pieces[i].gameObject.activeSelf)
                        n++;
                }

                return n;
            }
        }

        private void Awake()
        {
            int n = _pieces == null ? 0 : _pieces.Length;

            _velocity = new Vector3[n];
            _anchor = new Vector3[n];
            _pieceUnits = new float[n];
            _framePhase = new float[n];

            Stop();
        }

        /// <summary>
        /// 원본 world 좌표계를 얹을 프레임. <b>블록·골대와 같은 것을 넣어야</b> 캐논이 화면 밖 제자리에 선다.
        /// </summary>
        public void SetWorldFrame(Transform worldFrame)
        {
            _worldFrame = worldFrame;
        }

        /// <summary>
        /// 터뜨린다. <b>자리를 인자로 받지 않는다</b> — 캐논 2문의 좌표가 <b>원본에서 고정</b>이기 때문이다 [실측].
        /// ⚠ 예전 서명 <c>Play(Vector3 origin)</c> 은 「골대에서 솟구친다」는 <b>1회차 오독</b>의 흔적이다.
        /// </summary>
        public void Play()
        {
            if (_pieces == null || _pieces.Length == 0)
                return;

            _timer = 0f;

            for (int i = 0; i < _pieces.Length; i++)
            {
                SpriteRenderer piece = _pieces[i];

                if (piece == null)
                    continue;

                // 앞 절반이 좌 캐논, 뒤 절반이 우 캐논 [실측 — 50 / 50].
                int cannon = i < _pieces.Length / 2 ? 0 : 1;

                float u = Hash(i);
                float v = Hash(i + 977);
                float w = Hash(i + 5231);

                // ── 자리 — 캐논 입구에 뭉쳐 있다.
                Vector2 mouth = CannonWorld[cannon];
                double x = mouth.x + (u - 0.5f) * MouthSpreadXWorld;
                double y = mouth.y + (v - 0.5f) * MouthSpreadYWorld;

                Vector3 spawn = ToFrame(x, y);

                _anchor[i] = spawn;
                piece.transform.position = spawn;

                // ── 속도 — 캐논 각도 ± 분사콘. 원본은 y 가 아래라 유니티에서 부호를 뒤집는다.
                //    ★ [정정 · 25회차] 셋 다 «저작값»이다 — Speed 1500 · randomiser 800(=±400) · cone 30°(=±15°).
                //      예전 값(1550 · ±33 % · 70°)은 «관측 분포를 덮는 폭»이었다.
                float angle = (CannonAngleDeg[cannon] + (w - 0.5f) * _spreadDegrees) * Mathf.Deg2Rad;
                float speed = BlumgiUnits.ToUnits(_launchSpeedWorld + (v - 0.5f) * _speedRandomiserWorld);

                _velocity[i] = new Vector3(Mathf.Cos(angle) * speed, -Mathf.Sin(angle) * speed, 0f);

                // ── ★★ [정정 · 23회차] 조각은 «회전하지 않는다».
                //    각도는 조각마다 «고정»이고 관측값이 0 / 2 / 4 / 6 rad 네 값에 몰린다 [실측].
                //    예전의 `_spin` (초당 240° 회전)은 「개별 회전」이라는 1회차 오독의 흔적이다.
                piece.transform.localRotation =
                    Quaternion.Euler(0f, 0f, FixedAngleDegrees[i % FixedAngleDegrees.Length]);

                // ── ★ 크기 — 정사각이고 조각마다 «고정»이다 [실측 26.1 ~ 36.8 world px].
                float naturalUnits = PieceSourcePixels / BlumgiUnits.WorldPixelsPerUnit;

                _pieceUnits[i] = BlumgiUnits.ToUnits(
                                     Mathf.Lerp(_pieceSizeMinWorld, _pieceSizeMaxWorld, u))
                                 / naturalUnits;

                // ── ★ 애니 위상 — 조각마다 다른 프레임에서 6프레임을 돈다 [실측].
                _framePhase[i] = w * FlipWidthRatio.Length;

                // ⚠ 첫 프레임을 «여기서» 세운다 — Update 를 기다리면 «한 프레임 동안 프리팹 크기»로 보인다.
                int frame0 = Mathf.FloorToInt(_framePhase[i]) % FlipWidthRatio.Length;

                piece.transform.localScale =
                    new Vector3(_pieceUnits[i] * FlipWidthRatio[frame0], _pieceUnits[i], 1f);

                if (_colors != null && _colors.Length > 0)
                    piece.color = _colors[i % _colors.Length];

                piece.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 전부 지운다.
        /// ★ <b>원본에서 이것을 하는 것은 «레벨 전환»뿐</b>이다 [실측 — 자연 소멸이 없다].
        /// 그래서 <see cref="BlumgiClearOverlay.StopAll"/>(전환 뒤 정리) 말고 다른 데서 부르지 않는다.
        /// </summary>
        public void Stop()
        {
            _timer = -1f;

            if (_pieces == null)
                return;

            for (int i = 0; i < _pieces.Length; i++)
            {
                if (_pieces[i] != null)
                    _pieces[i].gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (_timer < 0f)
                return;

            float dt = Time.unscaledDeltaTime;
            _timer += dt;

            // ★ 수명 타이머가 «없다» — 원본에 자연 소멸이 없기 때문이다 [8회차 실측].
            //   예전 구현의 `_lifeSeconds = 2.4` 는 「전환에 가려 미측정」을 «소멸 시각»으로 오독한 것이다.

            float gravity = BlumgiUnits.ToUnits(_gravityWorld);
            float flutter = BlumgiUnits.ToUnits(FlutterMagnitudeWorld);
            float omega = FlutterPeriodSeconds <= 0f ? 0f : Mathf.PI * 2f / FlutterPeriodSeconds;

            for (int i = 0; i < _pieces.Length; i++)
            {
                SpriteRenderer piece = _pieces[i];

                if (piece == null || piece.gameObject.activeSelf == false)
                    continue;

                _velocity[i] += new Vector3(0f, -gravity * dt, 0f);
                _anchor[i] += _velocity[i] * dt;

                // 팔랑임 — Sine 둘(수평·수직)로 얹는다. 축 배정은 `추정(근거: 화면상 «팔랑거리며» 낙하)`.
                float phase = Hash(i + 31) * Mathf.PI * 2f;

                piece.transform.position = _anchor[i]
                                           + new Vector3(Mathf.Sin(_timer * omega + phase) * flutter,
                                                         Mathf.Sin(_timer * omega * 0.5f + phase) * flutter * 0.5f,
                                                         0f);

                // ── ★★ [신규 · 23회차 소스 직독] 「팔랑임」의 정체는 «회전»이 아니라 «6프레임 뒤집기»다.
                //    소스 52×52 순백 6프레임의 «폭»이 52 · 52 · 52 · 30 · 6 · 26 으로 변한다 —
                //    세로로 세운 카드가 도는 그림이다. 세로는 6프레임 전부 52 로 «안 변한다».
                //    ⇒ 프레임 = 폭 배율. 조각마다 «다른 위상»에서 speed 10 으로 돈다 [실측].
                int frame = Mathf.FloorToInt(_framePhase[i] + _timer * FlipFramesPerSecond)
                            % FlipWidthRatio.Length;

                float unit = _pieceUnits[i];

                piece.transform.localScale = new Vector3(unit * FlipWidthRatio[frame], unit, 1f);
            }
        }

        /// <summary>원본 world 좌표 → 유니티 위치. 프레임이 안 들어왔으면 자기 자리를 기준으로 쓴다.</summary>
        private Vector3 ToFrame(double worldX, double worldY)
        {
            Vector3 local = BlumgiUnits.ToPosition(worldX, worldY);
            return _worldFrame == null ? transform.TransformPoint(local) : _worldFrame.TransformPoint(local);
        }

        /// <summary>인덱스로 결정되는 의사난수 — 같은 입력이면 언제나 같은 그림이 나온다.</summary>
        private static float Hash(int i)
        {
            float v = Mathf.Sin(i * 12.9898f) * 43758.5453f;
            return v - Mathf.Floor(v);
        }
    }
}
