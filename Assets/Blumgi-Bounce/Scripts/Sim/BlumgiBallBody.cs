using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 공의 <b>물리 바디</b>. 프리팹(<c>BlumgiBall</c>)에 굽혀 붙는다.
    ///
    /// <para>
    /// ★ 여기서 하는 일은 둘뿐이다 — ① 강체·콜라이더를 «찾아 두는 것» ② <b>충돌 횟수를 세는 것</b>.
    /// 충돌 횟수는 연출(임팩트·사운드)이 쓰고, 채점의 「첫 반발 이전만」 규칙도 이 값을 본다
    /// (원장 합격 기준 5-b). <b>채점 전용 진입점이 아니다</b> — 게임이 실제로 쓰는 값이다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>발사는 «텔레포트»다</b> [4회차 실측 — 원본 채록에서 발사 프레임이 위치 도약으로 나타난다].
    /// 대기 중에는 <c>simulated = false</c> 로 물리에서 빠져 있고, 발사 순간 위치·속도를 «세워» 넣는다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlumgiBallBody : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D _body;
        [SerializeField] private CircleCollider2D _collider;

        /// <summary>
        /// ★★★ <b>이 공과 «부딪히지 않는» 상대</b> — 발사대 블롭이다 [16회차 실측].
        ///
        /// <para>
        /// 원본에서 <b>«공 ↔ 발사대» 쌍 하나만</b> 충돌에서 빠진다. 블롭 자체는 살아 있고
        /// <b>깔고 앉은 블록과 계속 접촉한다</b>(관통 111 프레임 내내 블롭의 접촉 목록은 블록 3개뿐).
        /// 그래서 <b>블롭을 끄는 것도 · 정적으로 만드는 것도 · 센서로 만드는 것도 답이 아니다</b> —
        /// <b>쌍 하나만</b> 빼야 한다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>기전은 «미측정»이다.</b> 원본이 어느 값으로 그 쌍을 거르는지는 못 읽었다
        /// (b2Filter · 센서 · 활성 · 조인트 · world · 레이어 · body type 은 전부 배제됐고
        /// 남은 자리는 난독화된 <c>b2ContactFilter</c> 콜백 하나다). <b>우리는 «결과»를 재현한다.</b>
        /// </para>
        /// </summary>
        private Collider2D _excludedLauncher;

        private int _collisionCount;

        /// <summary>강체. 프리팹이 안 구워졌으면 <c>null</c> 이다 — 조용히 대체하지 않는다.</summary>
        public Rigidbody2D Body
        {
            get { return _body; }
        }

        public CircleCollider2D Collider
        {
            get { return _collider; }
        }

        /// <summary>이 공이 무언가에 «닿기 시작»한 횟수. <see cref="ResetCollisionCount"/> 로만 0 이 된다.</summary>
        public int CollisionCount
        {
            get { return _collisionCount; }
        }

        /// <summary>가장 마지막 충돌의 법선 방향 상대 속도(유닛/s). 진단·연출용이다.</summary>
        public float LastImpactSpeed { get; private set; }

        public void ResetCollisionCount()
        {
            _collisionCount = 0;
            LastImpactSpeed = 0f;
        }

        /// <summary>
        /// ★★★ <b>「이 상대와는 안 부딪힌다」를 세운다</b> — 레벨을 «세울 때» <see cref="BlumgiPhysicsWorld"/> 가 부른다.
        ///
        /// <para>
        /// <b>왜 <c>Physics2D.IgnoreCollision</c> 인가</b> — 원본에서 빠지는 것이 «클래스»가 아니라
        /// <b>«쌍 하나»</b>라서다. 레이어 충돌 매트릭스는 ① <b>프로젝트 전역 설정</b>이고
        /// (이 저장소는 게임을 여러 개 담는다 — 재발방지 #43) ② 레이어 이름·인덱스를 <b>전역에서 소비</b>하며
        /// ③ 배제가 «레이어 대 레이어»라 <b>쌍보다 넓다</b>. <c>IgnoreCollision</c> 은
        /// <b>정확히 그 두 콜라이더</b>만 빼고 전역에 아무것도 안 남긴다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>엔진이 이 상태를 «지울» 수 있다</b> — 콜라이더가 시뮬에서 빠졌다 돌아오면
        /// (<c>Rigidbody2D.simulated</c> 토글 · 콜라이더 비활성) Box2D fixture 가 다시 만들어진다.
        /// 이 공은 <see cref="Freeze"/>(골인) 뒤 <see cref="Park"/> 로 돌아오므로 <b>실제로 그 경로를 지난다.</b>
        /// 그래서 <see cref="Park"/> · <see cref="Launch"/> 가 <b>매번 다시 세운다</b> — 멱등이라 공짜다.
        /// </para>
        /// </summary>
        public void ExcludeFromCollision(Collider2D other)
        {
            _excludedLauncher = other;
            ApplyExclusions();
        }

        /// <summary>세워 둔 배제를 엔진에 «다시» 넣는다. 비어 있으면 아무것도 안 한다.</summary>
        private void ApplyExclusions()
        {
            if (_collider == null || _excludedLauncher == null)
                return;

            Physics2D.IgnoreCollision(_collider, _excludedLauncher, true);
        }

        /// <summary>
        /// <b>경기장 밖 대기 자리로 되돌린다</b> — 원본은 발사 전 공을 <c>(0, 10000)</c> 에 두고
        /// <b>그대로 자유낙하시킨다</b> [6회차 실측 · 20초 관찰에서 y 32520 → 248293].
        ///
        /// <para>
        /// ★ <b>물리에서 빼지 않는다.</b> 원본에서 미발사 공은 «멈춰 있는» 것이 아니라 «떨어지는» 중이다.
        /// 경기장 안에 세워 두면 그 공이 굴러다니다 골이 된다 — 패스 ①-c 위양성 2건의 뿌리다.
        /// </para>
        ///
        /// <para>
        /// ⚠ 블롭 머리 위에 <b>보이는</b> 공은 별개 오브젝트(<c>spr_BallVisu</c>)다. 이 물리 공은 안 보이는 자리에 있다.
        /// </para>
        /// </summary>
        public void Park(Vector2 position)
        {
            if (_body == null)
                return;

            _body.simulated = true;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _body.rotation = 0f;
            _body.position = position;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            transform.rotation = Quaternion.identity;

            // ★ 시뮬에 «다시 들어오는» 자리다 — 엔진이 배제를 지웠을 수 있다 (위 주석).
            ApplyExclusions();
        }

        /// <summary>
        /// 발사 — 위치·속도·<b>각속도</b>를 «같은 프레임에» 세우고 물리에 넣는다.
        ///
        /// <para>
        /// ★★ <b>공은 발사되는 «그» 프레임에 이미 돌고 있다</b> [13회차 실측 · 직독].
        /// 원본 저작값은 <b>−200 °/s</b>(원본 축) 하나이고 <b>force·레벨과 무관한 상수</b>다 —
        /// 16샷 · 5레벨 · force 2.7배 범위에서 <b>표준편차 0</b>. 발사 «직전» 프레임은 정확히 0 이다.
        /// </para>
        ///
        /// <para>
        /// ⚠⚠ <b>「발사 직후 각속도 0, 첫 충돌에서 처음 붙는다」는 4회차 서술은 오귀속이었다.</b>
        /// 「직후」로 본 프레임이 <b>«발사 전»</b> 이었다 — 발사~첫 충돌 사이에 <c>IsTouching()</c> 이
        /// <b>15샷 전부 0건</b>인데 그 구간 내내 ω 는 −3.49 다 [13회차 실측].
        /// <b>여기 <c>angularVelocity = 0</c> 을 되돌려 놓지 마라.</b>
        /// </para>
        ///
        /// <para>
        /// ⚠ 인자는 <b>유니티 축의 °/s</b> 다 — 원본 축에서의 환산은
        /// <see cref="BlumgiUnits.ToAngularVelocityDegrees"/> «한 곳»이 한다 (y 뒤집기가 부호를 반전시킨다).
        /// </para>
        /// </summary>
        public void Launch(Vector2 position, Vector2 velocity, float angularVelocityDegrees)
        {
            if (_body == null)
                return;

            _body.simulated = true;
            _body.position = position;
            _body.rotation = 0f;
            _body.linearVelocity = velocity;
            _body.angularVelocity = angularVelocityDegrees;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            transform.rotation = Quaternion.identity;
            ResetCollisionCount();

            // ★ 발사도 «시뮬에 다시 들어오는» 자리다 — 배제를 다시 세운다.
            ApplyExclusions();
        }

        /// <summary>골인 뒤 정지. 원본도 골인하면 그 공은 더 이상 굴러다니지 않는다.</summary>
        public void Freeze()
        {
            if (_body == null)
                return;

            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _body.simulated = false;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            _collisionCount++;
            LastImpactSpeed = collision.relativeVelocity.magnitude;
        }

        /// <summary>빌더가 굽는 시점에 참조를 세운다.</summary>
        public void EditorSetReferences(Rigidbody2D body, CircleCollider2D circleCollider)
        {
            _body = body;
            _collider = circleCollider;
        }
    }
}
