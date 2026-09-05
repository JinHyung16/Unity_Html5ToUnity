using System.Collections.Generic;
using System.IO;
using JinHyung.BlumgiBounce;
using JinHyung.Core;
using JinHyung.Data;
using JinHyung.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 프리팹을 <b>코드로 굽는다</b> (확정표 10-b). 손으로 고친 것은 다음 실행에 날아간다 —
    /// 고칠 것은 <b>이 스크립트</b>다.
    ///
    /// <para>
    /// ⚠ <b>배선하지 않는다.</b> 여기는 패스 ② 다 — <b>실물과 «주입받을 자리»</b>까지만 만든다.
    /// 「눌러도 아무 일이 없다」는 이 시점에 정상이다. 씬 구성·데이터 연결은 패스 ③ 몫이다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>월드 프리팹의 자식 오프셋·콜라이더 크기는 <see cref="BlumgiUnits.WorldPixelsPerUnit"/> 을
    /// «굽는 시점»에 먹는다.</b> 확정 E 개정(1 유닛 = 1 m = 원본 50 px)이 반영됐으므로
    /// <b>그 상수를 고치면 이 빌더를 반드시 다시 돌린다</b>. 그것이 「빌더 스크립트」 방식의 값어치다.
    /// </para>
    ///
    /// <para>
    /// ★★ <b>물리 콜라이더도 여기서 굽는다</b> (패스 ①-c). 확정 G(자체 적분기)가 폐기되고
    /// 유니티 2D 물리(Box2D)로 갈렸기 때문이다 — 손으로 인스펙터에서 넣으면 다음 굽기에 날아간다.
    /// <b>수치는 코드에 박지 않고 <c>BlumgiConfigTable.json</c> 에서 읽는다</b> —
    /// 실측값의 정본이 두 곳으로 갈리면 다음 회차에 반드시 어긋난다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>UI 프리팹은 스프라이트를 «물지 않는다»</b> — Resources 프리팹이 어드레서블 아트를 물면
    /// 빌드에 중복 편입된다 (ResourceRule). 대신 <see cref="BlumgiUiArtBinder"/> 에 <b>주소</b>를 굽는다.
    /// 월드 프리팹은 <b>자기도 어드레서블</b>이라 그냥 문다.
    /// </para>
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.BlumgiPrefabBuilder.BuildAll</c></para>
    /// </summary>
    public static class BlumgiPrefabBuilder
    {
        private const string GameArt = "Assets/Blumgi-Bounce/Art/Game";
        private const string WorldPrefabRoot = "Assets/Blumgi-Bounce/Prefabs";
        private const string PhysicsMaterialRoot = "Assets/Blumgi-Bounce/Prefabs/Physics";
        private const string DataFolder = "Assets/Blumgi-Bounce/Data";
        private const string UiPrefabRoot = "Assets/Blumgi-Bounce/Resources/UI/Window";
        private const string FontPath = BlumgiFontSetup.FontAssetPath;

        // ── 확정표 3 — 1920 × 1080 (Landscape)
        private const int RefWidth = 1920;
        private const int RefHeight = 1080;

        /// <summary>world → 1920×1080 캔버스 px 배율 = 1080 / 1280 [UIUX 1-c]. <b>세로가 심판이다.</b></summary>
        private const float UiScale = RefHeight / 1280f;   // 0.84375

        /// <summary>
        /// ★★ <b><c>Main</c> 레이어만 배율이 «정확히 2배»다</b> [실측 25회차 · <c>GetViewport()</c>].
        ///
        /// <para>
        /// 기준 레이어(<c>BG</c>·<c>FG</c>·<c>UI</c>) 뷰포트가 <b>2276.766 × 1280</b> world 인데
        /// <c>Main</c> 은 <b>1138.382 × 640</b> 이라 비가 <b>정확히 2.000000</b> 이다 ⇒ <c>1080 / 640 = 1.6875</c>.
        /// 스크린샷 픽셀로 독립 검산됐다 — <c>PLAYERButton</c> 두 장이 <b>440 world = 322 px</b>
        /// (예측 323.1 · 오차 0.35 %).
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>셋을 한 배율로 묶으면 안 된다.</b> <c>WELCOME</c> 글자(흔들림 · 외곽선)와 카드는 <c>Main</c> 이고
        /// <b>크레딧 2줄은 <c>FG</c></b> 라 <see cref="UiScale"/> 를 탄다 — 한 배율로 묶으면 크레딧이 «2배»가 된다.
        /// </para>
        /// </summary>
        private const float MainScale = RefHeight / 640f;   // 1.6875

        /// <summary>
        /// <c>Main</c> 레이어 뷰포트의 world 중심 [실측 — <c>[70.809, 320, 1209.191, 960]</c> 의 중심].
        /// 웰컴 반짝임이 <b>이 점 기준 ± random(−640, +640)</b> 으로 생긴다.
        /// </summary>
        private const float MainViewportCenterWorld = 640f;

        /// <summary>유니티 기본 UI 레이어.</summary>
        private const int UiLayer = 5;

        // ── 정렬 순서. 원본 레이어 순서를 그대로 옮긴다 [05_연출 「레이어 구조」].
        private const int OrderBackground = -100;
        private const int OrderGrid = -90;
        // ★ 야자수 — <b>잎이 먼저, 줄기가 그 위</b> [5회차 실측 §1-b · z 쌍 (잎 z1, 줄기 z2) …].
        //   ⚠ 예전 값은 반대(줄기 −80 · 잎 −79)였고 패스 ③ 이 «런타임에서» 뒤집고 있었다 —
        //     그 보정을 걷어내고 여기서 굳힌다. 그루마다의 z 는 배경 프리팹이 인스턴스별로 얹는다.
        private const int OrderPalmLeaf = BlumgiBackgroundLayout.PalmSortingBase;
        private const int OrderPalmTrunk = BlumgiBackgroundLayout.PalmSortingBase + 1;
        private const int OrderArrow = -10;        // 원본 FXBottom

        // ★ 골인 FX 둘도 «같은 FXBottom» 이다 [실측 · 23회차 layers_A.json — 레이어 index 1].
        //   ⚠ 「연출이니까 위」가 아니다 — FXBottom 은 Collisions(블록·공·골대)보다 «아래»다.
        //   한 레이어 안의 앞뒤는 «생성 순서»가 정한다 [실측 uid 1076 텔레포트 → 1077 빛줄기].
        private const int OrderTeleport = -9;
        private const int OrderWinLight = -8;

        // ★★ 블록 — <b>좌캡이 몸통보다 «아래»</b> 다.
        //   원본은 좌캡을 항상 그리고 «왼쪽 이웃의 몸통»으로 덮는다 [5회차 실측].
        //   유니티에서 정렬 순서는 z 보다 «세다» — 캡이 몸통보다 위 순서에 있으면
        //   이웃을 아무리 앞으로 당겨도 캡이 그 위로 떠오른다. 그래서 순서로 못 박는다.
        //   (뒤에서부터: 캡 8 → 몸통 10 → 무늬 11)
        private const int OrderHoopNet = 5;
        private const int OrderBlockBase = 10;
        private const int OrderHoopRim = 15;
        private const int OrderBlob = 20;
        private const int OrderBlobEyes = 21;
        private const int OrderTrail = 25;
        private const int OrderBall = 30;
        /// <summary>반짝임 소스 텍스처 한 변 (px) — <c>BlumgiSpriteBuilder.BuildSparkle</c> 과 «같은 값»이어야 한다.</summary>
        private const float SparkleSourcePixels = 44f;

        /// <summary>골대 림(<c>spr_BasketTop</c>) 표시 크기의 절반 [실측 212 × 55].</summary>
        private const float RimHalfWidthWorld = 212f * 0.5f;

        private const float RimHalfHeightWorld = 55f * 0.5f;

        /// <summary>
        /// 골대 반짝임 사각의 «깊이» — <b>골대 윗변에서 아래로 247.43 world</b>
        /// [실측 25회차 · <c>y = RND[949.5386 … 1196.9727]</c> vs 림 bbox 윗변 949.5].
        /// </summary>
        private const float HoopSparkleDepthWorld = 247.43f;

        /// <summary>
        /// 웰컴 반짝임 사각의 반폭 — 저작 식 <c>random(−640, +640)</c> 그대로 [실측 · 파라미터 4,000회 재평가].
        /// </summary>
        private const float WelcomeSparkleHalfExtentWorld = 640f;

        // ★ 반짝임(`FXChling`)은 «`FXs` 레이어»다 — 게임 본체(`Collisions`)보다 앞이고
        //   같은 레이어의 컨페티보다 뒤에 둔다 (컨페티는 골 순간에 나중에 생긴다) [04 §1-d].
        private const int OrderSparkle = 50;
        private const int OrderConfetti = 60;

        private static TMP_FontAsset _font;

        /// <summary>물리 실측값의 정본. <b>코드에 박지 않는다</b> — <c>BlumgiConfigTable.json</c> 에서 읽는다.</summary>
        private static BlumgiConfigData _physics;

        public static void BuildAll()
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            if (_font == null)
                Log.Warning($"폰트 에셋이 없다: {FontPath} — 먼저 BlumgiFontSetup.Setup 을 돌린다.");

            _physics = LoadPhysicsConfig();

            if (_physics == null)
            {
                // ⚠ 「콜라이더 없는 프리팹」이 조용히 구워지는 것이 가장 나쁜 실패다 —
                //    화면은 멀쩡하고 공만 블록을 통과한다.
                Log.Error($"설정 데이터를 못 읽었다: {DataFolder}/BlumgiConfigTable.json — 프리팹을 굽지 않는다");
                return;
            }

            EnsureFolder(WorldPrefabRoot);
            EnsureFolder(PhysicsMaterialRoot);
            EnsureFolder(UiPrefabRoot);
            AssetDatabase.Refresh();

            BuildBlockPrefab();
            BuildBallPrefab();
            BuildRestBallPrefab();
            BuildBallTrailPrefab();
            BuildBlobPrefab();
            BuildHoopPrefab();
            BuildPalmPrefab();
            BuildBackgroundPrefab();
            BuildConfettiPrefab();
            BuildGoalBurstPrefab();

            BuildGameWindow();
            BuildWelcomeWindow();
            BuildClearOverlay();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Log.Success($"Blumgi 프리팹 굽기 완료 — 월드 10종 · UI 창 3종 · " +
                        $"콜라이더(공 r{_physics.BallRadius}px · 블록 {_physics.BlockCollisionWidth}×{_physics.BlockCollisionHeight}px " +
                        $"· 골대 r{_physics.HoopPostRadius}px · 블롭 {_physics.BlobCollisionWidth}×{_physics.BlobCollisionHeight}px) " +
                        $" @ PPU {BlumgiUnits.WorldPixelsPerUnit}");
        }

        // ══════════════════════════════════════════════ 월드 프리팹

        /// <summary>
        /// 블록 한 칸 — <b>그림 세 겹 + 정적 콜라이더 하나</b>.
        /// 세 겹(본체 · 무늬 · 좌캡)인 이유는 <b>tint 가 세 색</b>이기 때문이다 (한 스프라이트에 한 색만 곱해진다).
        ///
        /// <para>
        /// ★★ <b>콜라이더는 격자 칸(50)이 아니라 84 × 58.983 이다</b> [4회차 Box2D fixture 실측].
        /// 그래서 <b>이웃 블록끼리 가로 34 px 겹친다</b> — 그게 원본이다.
        /// 격자 크기로 깔면 사이에 틈이 생겨 <b>공이 블록 사이로 빠진다</b> (확정 D 는 «그림»에 관한 확정이었다).
        /// </para>
        ///
        /// <para>
        /// ★★★ <b>[정정 · 17회차] 스킨은 «한 장 · 5프레임»이다.</b> 세 장(캡/몸통/무늬)을 겹쳐
        /// 틴트하던 예전 구성은 화면 합성 결과를 소스로 오인한 것이었다 — 원본 소스 텍스처를
        /// 꺼내 보니 <b>격자점 기준 ±42 대칭인 한 장</b> 안에 캡·칸막이·몸통·사각이 전부 있고
        /// 색은 <b>프레임에 구워져</b> 있다(인스턴스 틴트 = 흰색).
        /// </para>
        ///
        /// <para>
        /// 좌캡을 켜고 끄는 코드가 없는 이유는 그대로다 — 왼쪽 이웃의 몸통이 이 칸의 캡을 덮는다.
        /// 스프라이트 기하가 그 덮임이 정확히 맞도록 구워져 있고
        /// (<c>BlumgiSpriteBuilder</c> 블록 가로 기하 — 칸막이 오른쪽 −10 + 격자 피치 50 = 바깥 오른쪽 +40),
        /// 겹침 순서는 <c>BlumgiLevelPresenter</c> 가 x 로 z 를 주어 «왼쪽이 위»로 만든다.
        /// </para>
        /// </summary>
        private static void BuildBlockPrefab()
        {
            var root = new GameObject("BlumgiBlock");

            var blockCollider = root.AddComponent<BoxCollider2D>();
            blockCollider.size = new Vector2(BlumgiUnits.ToUnits(_physics.BlockCollisionWidth),
                                             BlumgiUnits.ToUnits(_physics.BlockCollisionHeight));
            blockCollider.sharedMaterial = EnsureMaterial("BlumgiBlockPhysics",
                                                          _physics.BlockRestitution, _physics.BlockFriction);

            // ★ 스킨 한 장. 5프레임은 «데이터 종속 아트»라 프리팹에 «고르지 않고» 목록만 물린다 —
            //   어느 프레임을 세울지는 배선(패스 ③)이 레벨을 보고 정한다 (Transfer_Artist 「자리를 비워 둔다」).
            SpriteRenderer skin = NewSprite("Skin", root.transform,
                                            BlumgiArtAddress.BlockSkinAddresses[0], OrderBlockBase);

            var frames = new Sprite[BlumgiArtAddress.BlockSkinAddresses.Length];

            for (int i = 0; i < frames.Length; i++)
                frames[i] = LoadSprite(BlumgiArtAddress.BlockSkinAddresses[i]);

            var view = root.AddComponent<BlumgiBlockView>();
            SetRef(view, "_skin", skin);
            SetRefArray(view, "_skinFrames", frames);

            SavePrefab(root, WorldPrefabRoot + "/BlumgiBlock.prefab");
        }

        /// <summary>
        /// 공 — <b>회전하는 원형 강체</b>.
        ///
        /// <para>
        /// ★★ <b>회전을 잠그지 않는다.</b> 원본 <c>preventRotation = false</c> 이고 각속도가
        /// −7.4 ~ +10.9 rad/s 로 관측된다 [4회차 실측]. 접선 속도비가 −0.56 ~ +3.54 로 흩어지는 것은
        /// <b>회전 에너지가 병진으로 넘어오기 때문</b>이라, 회전을 잠그면 다중 반발이 원리적으로 어긋난다.
        /// </para>
        ///
        /// <para>
        /// ★★ <b>연속 충돌 검출(Continuous)을 «켠다»</b> — [9회차 실측 정정].
        /// 패스 ②-b 까지는 「원본 <c>isBullet = false</c> 니까 Discrete」로 두었는데 <b>그 대응이 틀렸다.</b>
        /// 원본 월드는 <c>world.GetContinuousPhysics() = true</c> 이고, Box2D 에서 그 값이 참이면
        /// <b>총알이 아닌 동적 바디도 «정적 바디»에는 CCD 가 걸린다</b> — 블록·림·(사실상) 블롭이 전부 정적이라
        /// <b>원본 공은 CCD 로 막힌다.</b> <c>isBullet</c> 은 «동적 대 동적»까지 넓히는 추가 플래그일 뿐이다.
        /// </para>
        ///
        /// <para>
        /// 유니티 2D 는 월드 단위 CCD 스위치를 노출하지 않고 <b>바디 단위</b>로만 고른다 —
        /// 그래서 원본의 「정적에 대해 CCD」를 얻는 유일한 자리가 <c>Continuous</c> 다.
        /// ⚠ 유니티 쪽은 «동적 대 동적»까지 함께 켜지므로 <b>엄밀히는 원본의 상위집합</b>이다.
        /// 이 게임에서 동적은 블롭 하나뿐이고 밀도가 공의 100배라 차이가 날 자리가 없다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>이건 「터널링이 걱정되니 켜 두자」가 아니다.</b> Discrete 로 두면 공이 한 프레임에
        /// 블록을 파고든 뒤 밀려나오면서 <b>같은 스침이 여러 접촉으로 쪼개진다</b> —
        /// 원본은 샷당 블록 반발 <b>1~7회 · 같은 블록 재접촉 0건</b> 인데 우리는 22회였다 [9회차 §5-c].
        /// </para>
        /// </summary>
        private static void BuildBallPrefab()
        {
            var root = new GameObject("BlumgiBall");
            NewSprite("Sprite", root.transform, BlumgiArtAddress.BallAddress, OrderBall);

            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 1f;
            body.freezeRotation = false;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.None;
            body.linearDamping = (float)_physics.BallLinearDamping;
            body.angularDamping = (float)_physics.BallAngularDamping;

            // ⚠ 밀도로 질량을 «내는» 것이 원본과 같은 길이다 — 질량을 직접 적으면 반지름이 바뀔 때 갈린다.
            // ★★ 순서가 «거동»이다. 유니티는 «auto-mass 를 쓰는 동적 강체에 붙은» 콜라이더에만 density 를
            //    받아 준다 — 먼저 density 를 넣으면 경고만 찍고 «조용히 버려진다».
            //    그러면 밀도가 기본값 1 로 남는데, 공은 마침 1 이라 «티가 안 나고» 블롭(100)만 100배 가벼워진다.
            body.useAutoMass = true;

            var circle = root.AddComponent<CircleCollider2D>();
            circle.radius = BlumgiUnits.ToUnits(_physics.BallRadius);
            circle.density = (float)_physics.BallDensity;
            circle.sharedMaterial = EnsureMaterial("BlumgiBallPhysics",
                                                   _physics.BallRestitution, _physics.BallFriction);

            var ball = root.AddComponent<BlumgiBallBody>();
            ball.EditorSetReferences(body, circle);

            SavePrefab(root, WorldPrefabRoot + "/BlumgiBall.prefab");
        }

        /// <summary>
        /// <b>머리 위 공</b> (<c>spr_BallVisu</c>) — ★★ <b>물리가 하나도 없는 «그림만»</b>이다.
        ///
        /// <para>
        /// 원본은 미발사 물리 공을 <c>(0, 10000)</c> 에서 낙하시키고 [6회차 실측], 블롭 머리 위에 보이는
        /// 공은 <b>별개 오브젝트</b>다. 패스 ①-d 가 물리 공을 원본대로 내리자 머리 위가 비었고
        /// 그 자리를 이 프리팹이 채운다 (패스 ②-c).
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>강체·콜라이더를 붙이지 마라.</b> 붙이면 「발사하지 않은 공이 굴러다니다 골이 된다」는
        /// 패스 ①-c 위양성이 되살아난다. <c>BlumgiBall</c> 을 복사해 쓰지 않는 이유가 이것이다.
        /// </para>
        ///
        /// <para>
        /// 스프라이트는 물리 공과 <b>같은 <c>ball</c></b> (소스 94×93 → 4의 배수 96×96 · 여백은 투명 패딩)
        /// 이라 배율 1 이 곧 표시 94×93 이다 [UIUX 2-g]. 정렬 순서도 공과 같은 띠(<c>Collisions</c> 레이어)다.
        /// </para>
        /// </summary>
        private static void BuildRestBallPrefab()
        {
            var root = new GameObject(BlumgiRestBallView.PrefabName);

            SpriteRenderer sprite = NewSprite("Sprite", root.transform,
                                              BlumgiArtAddress.BallAddress, OrderBall);

            var view = root.AddComponent<BlumgiRestBallView>();
            SetRef(view, "_renderer", sprite);

            SavePrefab(root, $"{WorldPrefabRoot}/{BlumgiRestBallView.PrefabName}.prefab");
        }

        /// <summary>
        /// 트레일 — <b>고스트 31장</b> [런타임 실측 <c>spr_BallVisu2</c> 인스턴스 수].
        /// <c>TrailRenderer</c> 를 쓰지 않는 이유가 이 숫자다.
        /// </summary>
        private static void BuildBallTrailPrefab()
        {
            var root = new GameObject("BlumgiBallTrail");

            var ghosts = new SpriteRenderer[BlumgiBallTrailView.OriginGhostCount];

            for (int i = 0; i < ghosts.Length; i++)
            {
                ghosts[i] = NewSprite($"Ghost{i:00}", root.transform,
                                      BlumgiArtAddress.BallGhostAddress, OrderTrail - i);
                ghosts[i].gameObject.SetActive(false);
            }

            var view = root.AddComponent<BlumgiBallTrailView>();
            SetRefArray(view, "_ghosts", ghosts);

            SavePrefab(root, WorldPrefabRoot + "/BlumgiBallTrail.prefab");
        }

        /// <summary>
        /// 블롭.
        ///
        /// <para>
        /// ★★ <b>루트 = Box2D body 원점 = 인스턴스 bbox 의 «아래변 중앙»</b> [16회차 실측 · 4레벨 전수].
        /// W1L3 의 bbox 는 <c>[956.432, 294.759, 1043.568, 344.759]</c> 인데 b2Body 위치는
        /// <b><c>(1000, 344.759)</c></b> 다 — <b>원점이 bbox 중심이 아니다</b>(C3 핫스팟이 바닥 중앙).
        /// 그래서 <b>콜라이더를 «높이/2» 만큼 위로 올려</b> 상자가 원점 «위»에 서게 한다.
        /// 파생값(중심 좌표)을 데이터에 적는 대신 <b>기전을 옮긴 것</b>이다 (재발방지 #53).
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>배치 원점은 <c>spr_SlimePhysics</c>(물리 바디)</b> 이고 몸통 그림은 그보다
        /// <b>y 로 35 위 · x 로 1 오른쪽</b>이다 (= bbox 중심보다 10 위 · <c>04_UIUX 2-d-3</c> · 전 레벨 동일).
        /// 그림 좌표를 원점으로 쓰면 <b>전 레벨이 어긋난다</b> — 그래서 그 오프셋을 프리팹이 든다.
        /// </para>
        /// </summary>
        private static void BuildBlobPrefab()
        {
            var root = new GameObject("BlumgiBlob");

            // ★ 동적 상자 87.14 × 50 · 밀도 100 · 회전 잠금 [4회차 실측].
            //   밀도가 공(1)의 100배라 공에 밀리지 않지만 «정적»은 아니다 — 정적으로 만들면 원본과 갈린다.
            var blobBody = root.AddComponent<Rigidbody2D>();
            blobBody.bodyType = RigidbodyType2D.Dynamic;
            blobBody.gravityScale = 1f;
            blobBody.freezeRotation = true;

            // ★ 공과 «같은» 이유로 Continuous 다 — 원본 월드 CCD 는 바디를 가리지 않고
            //   모든 비-총알 동적 바디에 걸린다 [9회차 §1-d]. 블롭은 사실상 안 움직여서
            //   거동 차이가 날 자리가 없지만, 「원본 월드 설정」을 바디 둘 중 하나에만 넣으면
            //   다음 사람이 «둘 중 어느 쪽이 의도인지» 못 읽는다.
            blobBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            blobBody.interpolation = RigidbodyInterpolation2D.None;
            blobBody.angularDamping = (float)_physics.BallAngularDamping;

            // ★★ density 보다 «먼저» 켠다 — 순서가 뒤집히면 유니티가 density 를 조용히 버린다 (공 쪽 주석 참조).
            //    블롭은 밀도 100 이 «공에 안 밀리는 이유» 그 자체라, 버려지면 원본과 정면으로 갈린다.
            blobBody.useAutoMass = true;

            var blobBox = root.AddComponent<BoxCollider2D>();
            blobBox.size = new Vector2(BlumgiUnits.ToUnits(_physics.BlobCollisionWidth),
                                       BlumgiUnits.ToUnits(_physics.BlobCollisionHeight));

            // ★★ 상자는 body 원점 «위»에 선다 — 원점이 bbox 아래변이기 때문이다 [16회차 실측].
            //    상수로 박지 않고 «높이의 절반»으로 쓴다: 높이가 바뀌면 여기가 같이 따라간다.
            Vector3 boxOffset = BlumgiUnits.ToPosition(0.0, -_physics.BlobCollisionHeight * 0.5);
            blobBox.offset = new Vector2(boxOffset.x, boxOffset.y);

            blobBox.density = (float)_physics.BlobDensity;
            blobBox.sharedMaterial = EnsureMaterial("BlumgiBlobPhysics",
                                                    _physics.BlobRestitution, _physics.BlobFriction);

            // ★ 몸통 그림은 body 원점보다 y 로 35 위다 (= bbox 중심보다 10 위 · 04_UIUX 2-d-3).
            SpriteRenderer body = NewSprite("Body", root.transform,
                                            BlumgiArtAddress.BlobBodyAddress, OrderBlob);
            body.transform.localPosition = BlumgiUnits.ToPosition(1.0, -35.0);

            SpriteRenderer eyes = NewSprite("Eyes", body.transform,
                                            BlumgiArtAddress.BlobEyesIdleAddress, OrderBlobEyes);

            // 눈 중심은 몸통 중심보다 y 로 7 위다 [실측 — 몸통 (151,562) · 눈 (152,555)].
            eyes.transform.localPosition = BlumgiUnits.ToPosition(1.0, -7.0);

            var view = root.AddComponent<BlumgiBlobView>();
            SetRef(view, "_body", body);
            SetRef(view, "_eyes", eyes);
            SetRef(view, "_eyesIdle", LoadSprite(BlumgiArtAddress.BlobEyesIdleAddress));
            SetRef(view, "_eyesAngry", LoadSprite(BlumgiArtAddress.BlobEyesAngryAddress));

            // ★ 프레임 2 «발사 표정» — 17회차가 소스 텍스처로 닫아서 이제 «비워 두지 않는다».
            SetRef(view, "_eyesShoot", LoadSprite(BlumgiArtAddress.BlobEyesShoutAddress));

            SavePrefab(root, WorldPrefabRoot + "/BlumgiBlob.prefab");
        }

        /// <summary>
        /// 골대 — <b>림 중심 하나 + 고정 오프셋</b> [UIUX 2-d-3 · 5레벨 전부 같은 식으로 검산됨].
        /// 그리는 것은 림 · 그물 · 화살표 셋뿐이다 (충돌 기둥 · 감지기는 안 그린다).
        /// </summary>
        private static void BuildHoopPrefab()
        {
            var root = new GameObject("BlumgiHoop");

            // ★ 골대 판정체 — 정적 «원» 2개 (r = 20.11 px) [4회차 Box2D fixture 실측].
            //   그림(림·그물·화살표)과 별개다. 반발 0.7 이라 공이 림에 맞으면 튄다.
            PhysicsMaterial2D hoopMaterial = EnsureMaterial("BlumgiHoopPhysics",
                                                            _physics.HoopRestitution, _physics.HoopFriction);

            for (int side = -1; side <= 1; side += 2)
            {
                var post = root.AddComponent<CircleCollider2D>();
                post.radius = BlumgiUnits.ToUnits(_physics.HoopPostRadius);
                Vector3 offset = BlumgiUnits.ToPosition(side * _physics.GoalPostOffsetX, _physics.GoalPostOffsetY);
                post.offset = new Vector2(offset.x, offset.y);
                post.sharedMaterial = hoopMaterial;
            }

            SpriteRenderer rim = NewSprite("Rim", root.transform,
                                           BlumgiArtAddress.HoopRimAddress, OrderHoopRim);

            SpriteRenderer net = NewSprite("Net", root.transform,
                                           BlumgiArtAddress.HoopNetAddress, OrderHoopNet);
            // 그물 오프셋 — 골대 중심 기준 (0, +88) [7회차 실측 5/5]. ★ 데이터에서 읽는다:
            //   그림 자리를 코드 상수로 박으면 「골대는 중심 하나 + 상수 오프셋」이 두 곳에 적힌다.
            net.transform.localPosition = BlumgiUnits.ToPosition(_physics.GoalNetOffsetX, _physics.GoalNetOffsetY);

            // ── 화살표 2장 겹침. 간격 «25 는 확정», 기준 y·진폭·주기는 미측정이라 FloatMotion 이 든다.
            var arrowRoot = new GameObject("ArrowRoot");
            arrowRoot.transform.SetParent(root.transform, false);

            BlumgiFloatMotion floatMotion = arrowRoot.AddComponent<BlumgiFloatMotion>();

            // 표시 86×50 인데 소스가 100×172 다 — «세로로 크게 눌러 쓴다» [UIUX 2-g].
            Vector3 arrowScale = BlumgiUnits.DisplayScale(100f, 172f, 86f, 50f);

            for (int i = 0; i < 2; i++)
            {
                SpriteRenderer arrow = NewSprite($"Arrow{i}", arrowRoot.transform,
                                                 BlumgiArtAddress.GoalArrowAddress, OrderArrow - i);
                arrow.transform.localPosition = BlumgiUnits.ToPosition(0.0, i * 25.0);
                arrow.transform.localScale = arrowScale;

                // ★★ [정정 · 17회차] 두 장은 «같은 그림 두 번»이 아니라 «흰 화살표 + 검정 그림자»다
                //    [실측 GetUnpremultipliedColor — 위 (255,255,255) · 25 px 아래 (0,0,0)].
                //    텍스처는 순백 마스크라(불투명 색 1종 = 흰색) 색은 여기서 틴트로 넣는다.
                arrow.color = i == 0 ? Color.white : Color.black;
            }

            // ── ★ 골인 전용 애니메이션 (Animation 3) [17회차 실측].
            //    텍스처 4장을 새로 굽지 않고 «비균일 배율 4단»으로 옮긴다 — 원본 프레임이
            //    같은 그림을 눌러 그린 것이라는 게 소스 픽셀 수로 확인됐다.
            BlumgiHoopGoalAnimation rimGoal = root.AddComponent<BlumgiHoopGoalAnimation>();
            rimGoal.Configure(rim.transform, 212f, 55f,
                              new[]
                              {
                                  new Vector2(202f, 65f),
                                  new Vector2(192f, 75f),
                                  new Vector2(222f, 45f),
                                  new Vector2(212f, 55f),
                              },
                              1f / 30f, 0.5f);

            BlumgiHoopGoalAnimation netGoal = root.AddComponent<BlumgiHoopGoalAnimation>();
            netGoal.Configure(net.transform, 164f, 140f,
                              new[]
                              {
                                  new Vector2(144f, 164f),
                                  new Vector2(174f, 130f),
                                  new Vector2(179f, 125f),
                                  new Vector2(164f, 140f),
                              },
                              1f / 25f, 0.95f);

            // ── ★★★ 반짝임 이미터 (`FXChling` · `eFXs/10/1/1`) [실측 25회차 · 시트 파라미터 4,000회 재평가]
            //
            //   생성 좌표식이 W1L1 문맥에서 `x = RND[719.0018 … 930.9352]` · `y = RND[949.5386 … 1196.9727]` 로 풀렸고,
            //   그 사각이 `spr_BasketTop` bbox `[719, 949.5, 931, 1004.5]` 에 «정확히» 걸린다:
            //     x = 골대 «좌우폭 그대로» · y = 골대 «윗변»에서 아래로 247.43 world.
            //   ⇒ 그래서 골대 프리팹의 자식으로 둔다 — 레벨마다 골대가 옮겨 다녀도 같이 따라간다.
            //
            //   ⚠ 인게임 이미터는 «둘»인데(동시 존재 1~4) 두 번째의 «걸린 오브젝트»가 미측정이다 —
            //     사각만 알고 무엇에 붙었는지 모르면 L2~L5 에서 어디에 설지 알 수 없다.
            //     관측 못 한 것은 굽지 않는다 (`04 §6` 재측정 대기에 등재).
            BuildWorldSparkleField(root.transform, RimHalfWidthWorld, RimHalfHeightWorld,
                                   HoopSparkleDepthWorld, 4);

            var view = root.AddComponent<BlumgiHoopView>();
            SetRef(view, "_rim", rim);
            SetRef(view, "_net", net);
            SetRef(view, "_rimGoalAnimation", rimGoal);
            SetRef(view, "_netGoalAnimation", netGoal);
            SetRef(view, "_arrowRoot", arrowRoot.transform);
            SetRef(view, "_arrowFloat", floatMotion);

            SavePrefab(root, WorldPrefabRoot + "/BlumgiHoop.prefab");
        }

        /// <summary>
        /// ★ 월드(<c>SpriteRenderer</c>) 쪽 반짝임 이미터를 굽는다.
        ///
        /// <para>
        /// 사각은 «골대 기준»이다 — <c>x = ± 좌우폭/2</c> · <c>y = 윗변에서 아래로 <paramref name="depthWorld"/></c>.
        /// 원본 y 는 아래가 + 라 <see cref="BlumgiUnits.ToPosition"/> 로 뒤집는다.
        /// </para>
        ///
        /// <para>
        /// 크기는 <b>소스 44 px 스프라이트의 배율</b>로 준다 — 표시 <c>S</c> world 는
        /// <c>localScale = S / 44</c> 다 (PPU 50 이라 자연 크기가 44 world 다).
        /// </para>
        /// </summary>
        private static BlumgiSparkleField BuildWorldSparkleField(Transform parent,
                                                                 float halfWidthWorld,
                                                                 float halfHeightWorld,
                                                                 float depthWorld,
                                                                 int poolSize)
        {
            var root = new GameObject("SparkleField");
            root.transform.SetParent(parent, false);

            var items = new Transform[poolSize];

            for (int i = 0; i < poolSize; i++)
            {
                SpriteRenderer sparkle = NewSprite($"Sparkle{i}", root.transform,
                                                   BlumgiArtAddress.SparkleAddress, OrderSparkle);
                sparkle.gameObject.SetActive(false);
                items[i] = sparkle.transform;
            }

            var field = root.AddComponent<BlumgiSparkleField>();

            // 윗변 = −halfHeight (원본 y-down) · 아랫경계 = 윗변 + depth.
            float topUnits = BlumgiUnits.ToPosition(0.0, -halfHeightWorld).y;
            float bottomUnits = BlumgiUnits.ToPosition(0.0, -halfHeightWorld + depthWorld).y;

            SetRefArray(field, "_items", items);
            SetVector2(field, "_areaMin",
                       new Vector2(-BlumgiUnits.ToUnits(halfWidthWorld), bottomUnits));
            SetVector2(field, "_areaMax",
                       new Vector2(BlumgiUnits.ToUnits(halfWidthWorld), topUnits));

            SetFloat(field, "_startScale",
                     BlumgiSparkleField.OriginStartSizeWorld / SparkleSourcePixels);
            SetFloat(field, "_peakScale",
                     BlumgiSparkleField.OriginPeakSizeWorld / SparkleSourcePixels);

            return field;
        }

        /// <summary>
        /// ★ UI(<c>Image</c>) 쪽 반짝임 이미터를 굽는다 — 웰컴 전용.
        ///
        /// <para>
        /// 사각은 <b><c>Main</c> 뷰포트 중심 ± <c>random(−640, +640)</c> world</b> 다 [실측 · 시트 직독].
        /// 그 중심이 곧 화면 중심이라 <b>로컬 ± 640 × 1.6875 = ± 1080 px</b> 로 «떨어진다» —
        /// 표본에서 유도한 경계가 아니라 <b>저작 식과 뷰포트에서 나온 값</b>이다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>화면 밖에도 생긴다.</b> <c>Main</c> 의 가로 뷰포트가 world 1137.8 뿐이라
        /// 사각 1280 의 양끝은 화면 밖이다 — 원본도 그래서 «가끔 한쪽에 안 뜬다».
        /// </para>
        /// </summary>
        private static BlumgiSparkleField BuildUiSparkleField(Transform parent,
                                                              float halfExtentWorld,
                                                              int poolSize,
                                                              List<BlumgiUiArtBinder.Binding> bindings)
        {
            GameObject root = NewUI("SparkleField", parent, Vector2.zero);
            Stretch(root);

            var items = new Transform[poolSize];

            for (int i = 0; i < poolSize; i++)
            {
                Image sparkle = NewImage($"Sparkle{i}", root.transform,
                                         new Vector2(SparkleSourcePixels, SparkleSourcePixels));
                AnchorCenterOffset(sparkle.gameObject, 0f, 0f,
                                   new Vector2(SparkleSourcePixels, SparkleSourcePixels));
                bindings.Add(Bind(sparkle, BlumgiArtAddress.SparkleAddress));
                sparkle.gameObject.SetActive(false);
                items[i] = sparkle.transform;
            }

            var field = root.AddComponent<BlumgiSparkleField>();

            float halfPixels = halfExtentWorld * MainScale;

            SetRefArray(field, "_items", items);
            SetVector2(field, "_areaMin", new Vector2(-halfPixels, -halfPixels));
            SetVector2(field, "_areaMax", new Vector2(halfPixels, halfPixels));

            SetFloat(field, "_startScale",
                     BlumgiSparkleField.OriginStartSizeWorld * MainScale / SparkleSourcePixels);
            SetFloat(field, "_peakScale",
                     BlumgiSparkleField.OriginPeakSizeWorld * MainScale / SparkleSourcePixels);

            // ★ 원본은 웰컴 반짝임에만 `ZOrderMoveToBottom()` 을 건다 — `Main` 레이어의 «맨 뒤»다.
            root.transform.SetAsFirstSibling();

            return field;
        }

        /// <summary>
        /// 야자수 — <b>잎과 줄기가 별개 오브젝트</b>다 [런타임 실측]. 줄기는 <b>타일드 배경</b>이라
        /// 화면 아래로 길게 늘어난다 (표시 폭 75 · 높이 2849~3940).
        /// </summary>
        private static void BuildPalmPrefab()
        {
            var root = new GameObject("BlumgiPalm");

            SpriteRenderer leaf = NewSprite("Leaf", root.transform,
                                            BlumgiArtAddress.PalmLeafAddress, OrderPalmLeaf);

            // 소스 180×96 → 표시 434×233 (≈2.43배) [UIUX 2-g]. 소스만 보고 쓰면 원본보다 작다.
            leaf.transform.localScale = BlumgiUnits.DisplayScale(180f, 96f, 434f, 233f);

            SpriteRenderer trunk = NewSprite("Trunk", root.transform,
                                             BlumgiArtAddress.PalmTrunkAddress, OrderPalmTrunk);
            trunk.drawMode = SpriteDrawMode.Tiled;
            trunk.tileMode = SpriteTileMode.Continuous;
            trunk.size = new Vector2(BlumgiUnits.ToUnits(75.0), BlumgiUnits.ToUnits(3940.0));

            // 줄기는 잎 아래로 뻗는다 — 위 끝을 잎 중심 근처에 둔다.
            trunk.transform.localPosition = BlumgiUnits.ToPosition(0.0, 3940.0 * 0.5);

            var view = root.AddComponent<BlumgiPalmView>();
            SetRef(view, "_leaf", leaf);
            SetRef(view, "_trunk", trunk);

            SavePrefab(root, WorldPrefabRoot + "/BlumgiPalm.prefab");
        }

        /// <summary>
        /// 배경 — 단색 판 + 격자 + 야자수 5.
        ///
        /// <para>
        /// ⚠ <b>야자수의 인게임 world 좌표가 나에게 없다</b> — 「5레벨 전부 동일」이라는 사실만 있고
        /// 좌표표는 웰컴 화면 것뿐이다 (04 §6 「<c>BG</c> 레이어 world→화면 환산이 캡처와 어긋난다」가 미측정).
        /// ⇒ <b>5그루를 만들되 위치는 원점에 둔다.</b> 패스 ③ 이 실측 좌표를 넣는다.
        /// </para>
        ///
        /// <para>배경 격자는 <b>스크롤·드리프트가 없다</b> [실측] — 애니메이션을 붙이지 않는다.</para>
        /// </summary>
        private static void BuildBackgroundPrefab()
        {
            var root = new GameObject("BlumgiLevelBackground");

            // 안전영역 밖까지 덮어야 한다 — W1L5 는 x −25 · y −175 에도 블록이 있고,
            // 16:9 에서 보이는 가로가 −497.8 ~ 1777.8 world 다 [UIUX 1-b].
            const double CoverWidth = 3200.0;
            const double CoverHeight = 2400.0;

            SpriteRenderer solid = NewSprite("Solid", root.transform,
                                             BlumgiArtAddress.SolidAddress, OrderBackground);
            solid.drawMode = SpriteDrawMode.Sliced;
            solid.size = new Vector2(BlumgiUnits.ToUnits(CoverWidth), BlumgiUnits.ToUnits(CoverHeight));

            SpriteRenderer grid = NewSprite("Grid", root.transform,
                                            BlumgiArtAddress.GridTileAddress, OrderGrid);
            grid.drawMode = SpriteDrawMode.Tiled;
            grid.tileMode = SpriteTileMode.Continuous;
            grid.size = new Vector2(BlumgiUnits.ToUnits(CoverWidth), BlumgiUnits.ToUnits(CoverHeight));

            var palmRoot = new GameObject("Palms");
            palmRoot.transform.SetParent(root.transform, false);

            var palmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldPrefabRoot + "/BlumgiPalm.prefab");
            var palms = new BlumgiPalmView[5];

            for (int i = 0; i < palms.Length; i++)
            {
                GameObject instance = palmPrefab == null
                    ? new GameObject($"Palm{i}")
                    : (GameObject)PrefabUtility.InstantiatePrefab(palmPrefab);

                instance.name = $"Palm{i}";
                instance.transform.SetParent(palmRoot.transform, false);
                palms[i] = instance.GetComponent<BlumgiPalmView>();

                // ★ 그루마다의 z 는 «한 프리팹을 5그루가 공유»하므로 인스턴스에만 얹을 수 있다 —
                //   런타임이 아니라 «여기»가 그 자리다 (확정표 10-b). 잎 z1·줄기 z2 … 순서를 그대로 옮긴다.
                SetPalmOrder(instance.transform, "Leaf", OrderPalmLeaf + i * 2);
                SetPalmOrder(instance.transform, "Trunk", OrderPalmTrunk + i * 2);
            }

            var view = root.AddComponent<BlumgiLevelBackground>();
            SetRef(view, "_solid", solid);
            SetRef(view, "_grid", grid);
            SetRefArray(view, "_palms", palms);

            SavePrefab(root, WorldPrefabRoot + "/BlumgiLevelBackground.prefab");
        }

        /// <summary>야자수 인스턴스 한 그루의 자식 렌더 순서를 굽는다.</summary>
        private static void SetPalmOrder(Transform palm, string childName, int order)
        {
            Transform child = palm.Find(childName);

            if (child == null)
            {
                Log.Error($"야자수 프리팹에 {childName} 이 없다 — BuildPalmPrefab 이 먼저 돌아야 한다");
                return;
            }

            var renderer = child.GetComponent<SpriteRenderer>();

            if (renderer == null)
            {
                Log.Error($"야자수 {childName} 에 SpriteRenderer 가 없다");
                return;
            }

            renderer.sortingOrder = order;
        }

        /// <summary>
        /// 컨페티. 색 4종 [실측] 은 <b>tint</b> 다.
        /// ★ <b>[해소 · 8회차] 조각 개수 100 · 이미터는 <c>FXconfettis</c> 하나</b>
        /// (<c>~Special</c>·<c>~360</c> 는 인스턴스 0) [실측 — 클리어 직후 런타임 인스턴스 계수].
        /// </summary>
        private static void BuildConfettiPrefab()
        {
            var root = new GameObject("BlumgiConfetti");

            const int PieceCount = BlumgiConfettiBurst.OriginPieceCount;   // 100 [8회차 실측]
            var pieces = new SpriteRenderer[PieceCount];

            for (int i = 0; i < PieceCount; i++)
            {
                pieces[i] = NewSprite($"Piece{i:00}", root.transform,
                                      BlumgiArtAddress.ConfettiRectAddress, OrderConfetti);
                pieces[i].gameObject.SetActive(false);
            }

            var view = root.AddComponent<BlumgiConfettiBurst>();
            SetRefArray(view, "_pieces", pieces);
            SetColorArray(view, "_colors", new[]
            {
                Hex("FF709B"), Hex("FCFF47"), Hex("7DFF69"), Hex("7D64FF"),
            });

            SavePrefab(root, WorldPrefabRoot + "/BlumgiConfetti.prefab");
        }

        /// <summary>
        /// ★★ 골인 <c>FXwinLight</c> ×2 · <c>FXteleport</c> ×1 — <b>패스 ②-h 에서 «새로» 만든 것</b>이다.
        ///
        /// <para>
        /// 23회차가 골 순간 FX 를 훅으로 잡아 전부 실측했는데 <b>우리 이관본에 두 오브젝트가 없었다</b>
        /// (있던 것은 플래시·컨페티·<c>YES!</c> 셋뿐) — 「미측정」이 아니라 <b>«안 만든 것»</b> 이었다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>빛줄기는 그림을 굽지 않는다</b> — 원본 소스가 <b>1×1 순백 불투명 1픽셀</b>이라
        /// <c>solid</c> 를 <c>Sliced</c> 로 늘리는 것이 <b>원본과 같은 것</b>이다 [23회차 소스 직독].
        /// 알파 <b>0.300</b> 은 «고정»이고, 사라지는 것은 <b>크기가 닫히기 때문</b>이다.
        /// </para>
        /// </summary>
        private static void BuildGoalBurstPrefab()
        {
            var root = new GameObject("BlumgiGoalBurst");

            // ── 텔레포트가 «먼저» 생성된다 = 빛줄기보다 아래 [실측 uid 1001076 < 1001077].
            SpriteRenderer teleport = NewSprite("Teleport", root.transform,
                                                BlumgiArtAddress.TeleportAddresses[0], OrderTeleport);
            teleport.gameObject.SetActive(false);

            var frames = new Sprite[BlumgiGoalBurst.TeleportSpriteCount];

            for (int i = 0; i < frames.Length; i++)
                frames[i] = LoadSprite(BlumgiArtAddress.TeleportAddresses[i]);

            var winLights = new SpriteRenderer[BlumgiGoalBurst.WinLightCount];

            for (int i = 0; i < winLights.Length; i++)
            {
                winLights[i] = NewSprite($"WinLight{i}", root.transform,
                                         BlumgiArtAddress.SolidAddress, OrderWinLight);

                // 1픽셀을 «늘려» 쓰는 자리라 Sliced 다 (배경 판과 같은 기전).
                winLights[i].drawMode = SpriteDrawMode.Sliced;
                winLights[i].color = new Color(1f, 1f, 1f, 0.3f);
                winLights[i].gameObject.SetActive(false);
            }

            var view = root.AddComponent<BlumgiGoalBurst>();
            SetRef(view, "_teleport", teleport);
            SetRefArray(view, "_teleportFrames", frames);
            SetRefArray(view, "_winLights", winLights);

            SavePrefab(root, WorldPrefabRoot + "/BlumgiGoalBurst.prefab");
        }

        // ══════════════════════════════════════════════ UI 창

        /// <summary>
        /// 인게임 HUD.
        /// ★ 버튼 4개는 <b>오른쪽 끝 앵커</b>, <c>WORLD n</c>·도트는 <b>중앙 앵커</b>다 [UIUX 1-e] —
        /// 고정 x 로 구우면 1920 이 아닌 화면에서 전부 어긋난다.
        /// </summary>
        private static void BuildGameWindow()
        {
            GameObject root = NewUI("BlumgiGameWindow", null, Vector2.zero);
            Stretch(root);

            // ⚠ 배경 이미지를 깔지 않는다 — 배경은 «게임 카메라»가 그린다. 깔면 판이 통째로 가려진다.
            GameObject safe = NewSafeArea(root.transform);

            var bindings = new List<BlumgiUiArtBinder.Binding>();

            // ── WORLD n (중앙 · world bbox 240,38,1040,113 → 1920 중심 (960,64) · 675×63)
            TextMeshProUGUI worldText = NewText("WorldText", safe.transform, "WORLD 1", 52);
            AnchorFromTop(worldText.gameObject, 0f, 64f, new Vector2(675f, 63f));

            // ── 레벨 도트 (중앙 · 1920 중심 (964,115) · 245×41 캔버스 px = 290×48 world)
            //
            // ★★ [정정 2026-08-30] 이 바만 «world px 로 재고 뿌리에서 한 번 스케일»한다.
            //
            //   빈칸/채움은 Image.Type.Tiled 다. 타일 폭은 «스프라이트 px ÷ PPU × 캔버스 refPPU» 로
            //   정해지는데 둘 다 100 이라 굽는 크기(58 world)가 그대로 «58 캔버스 px» 가 된다.
            //   그런데 바 폭은 캔버스 px(245)로 줬으니 245 ÷ 58 = 4.22 칸 — <b>도트가 4개 + 잘린 조각</b>이 됐다.
            //   (세로도 타일 48 > 칸 41 이라 위가 잘렸다.)
            //   ⇒ 바 안쪽을 전부 world px(290 · 58 · 48)로 두고 뿌리에 UiScale 을 걸면
            //     «타일의 자»와 «칸의 자»가 같아져 정확히 5칸이 된다.
            //
            //   ⚠ 다른 UI 는 Simple(늘어남)이라 이 문제가 없다 — 타일링하는 것만 이 규칙을 탄다.
            const float DotCell = BlumgiLevelDotBar.CellWorldWidth;   // 58 world
            const float DotHeight = 48f;                              // world [런타임 실측]
            const int DotCount = 5;                                   // 월드당 5칸 [실측 · 진행률 20 %]

            GameObject dotRoot = NewUI("LevelDots", safe.transform, new Vector2(DotCell * DotCount, DotHeight));
            AnchorFromTop(dotRoot, 964f - RefWidth * 0.5f, 115f, new Vector2(DotCell * DotCount, DotHeight));
            dotRoot.transform.localScale = new Vector3(UiScale, UiScale, 1f);

            Image dotEmpty = NewImage("Empty", dotRoot.transform, new Vector2(DotCell * DotCount, DotHeight));
            bindings.Add(Bind(dotEmpty, BlumgiArtAddress.DotEmptyAddress, Image.Type.Tiled));

            GameObject fillRoot = NewUI("Fill", dotRoot.transform, new Vector2(DotCell, DotHeight));
            var fillRect = fillRoot.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(0f, 0.5f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;

            Image dotFill = fillRoot.AddComponent<Image>();
            dotFill.raycastTarget = false;
            bindings.Add(Bind(dotFill, BlumgiArtAddress.DotFullAddress, Image.Type.Tiled));

            var dotBar = dotRoot.AddComponent<BlumgiLevelDotBar>();
            SetRef(dotBar, "_empty", dotEmpty);
            SetRef(dotBar, "_fill", dotFill);
            SetRef(dotBar, "_fillRect", fillRect);

            // ── 우상단 버튼 4 (오른쪽 끝 −60/−160/−260/−360 world · 각 100×100 world) [UIUX 1-e]
            Button home = NewIconButton("HomeButton", safe.transform, 60f, bindings,
                                        BlumgiArtAddress.ButtonHomeAddress, out Image _);
            Button map = NewIconButton("MapButton", safe.transform, 160f, bindings,
                                       BlumgiArtAddress.ButtonMapAddress, out Image _);

            // ⠿ 월드 선택 — <b>의도된 차이 #6</b> (PD 판정 2026-08-30). 이관 범위가 월드1 이라 «갈 곳이 없다».
            // 눌러도 아무 일이 없는 버튼은 사람에게 «고장»으로 보인다 — 2P 카드(#1)와 같은 처리로 자리만 두고 끈다.
            map.gameObject.SetActive(false);
            Button sound = NewIconButton("SoundButton", safe.transform, 260f, bindings,
                                         BlumgiArtAddress.ButtonSoundOnAddress, out Image soundIcon);
            Button retry = NewIconButton("RetryButton", safe.transform, 360f, bindings,
                                         BlumgiArtAddress.ButtonRetryAddress, out Image _);

            // ── 튜토리얼 패널 (원본 레이어 FXBottom — 레이아웃 중앙 기준이라 캔버스 중앙 앵커와 같다)
            BlumgiTutorialPanel tutorial = BuildTutorialPanel(safe.transform, bindings);

            var window = root.AddComponent<BlumgiGameWindow>();
            SetEnum(window, "_windowType", EWindowType.Normal);
            SetRef(window, "_worldText", worldText);
            SetRef(window, "_dotBar", dotBar);
            SetRef(window, "_retryButton", retry);
            SetRef(window, "_soundButton", sound);
            SetRef(window, "_mapButton", map);
            SetRef(window, "_homeButton", home);
            SetRef(window, "_soundIcon", soundIcon);
            SetRef(window, "_tutorial", tutorial);

            AttachBinder(root, bindings);
            SetLayerRecursively(root, UiLayer);
            SavePrefab(root, UiPrefabRoot + "/BlumgiGameWindow.prefab");
        }

        /// <summary>
        /// 튜토리얼 패널 — ★ <b>판 2 · 문구 2 · 마우스 3 · 미니 그림 2</b> 가 원본 구조다 [UIUX 2-b · §5-b-3].
        ///
        /// <para>
        /// ★★ <b>토글 하나가 «네 가지»를 함께 바꾼다</b> [실측 26·25회차] —
        /// 문구(<c>HOLD</c>↔<c>SHOOT!</c>) · 마우스 흰 벌(자리 20 world 차) · <b>미니 그림(프레임·크기·자리)</b>.
        /// 검정 그림자(판 · 마우스)는 <b>두 컷 모두</b> 보인다.
        /// </para>
        ///
        /// <para>
        /// ⚠ 마우스가 3개인 이유는 <b>닫혔다</b> — 검정 그림자 1(고정) + 흰 컷 2(교대) [실측 26회차].
        /// </para>
        /// </summary>
        private static BlumgiTutorialPanel BuildTutorialPanel(Transform parent,
                                                              List<BlumgiUiArtBinder.Binding> bindings)
        {
            // world bbox 19,11,444,361 → 1920 중심 (615,157) · 358×295 [UIUX 2-b]
            GameObject panel = NewUI("TutorialPanel", parent, new Vector2(358f, 295f));
            AnchorFromTop(panel, 615f - RefWidth * 0.5f, 157f, new Vector2(358f, 295f));

            // 판 2장 — 뒤가 «오프셋 단색 그림자»다 (블러 없음) [06_리소스 공통 스타일]
            Image shadow = NewImage("PanelShadow", panel.transform, new Vector2(358f, 295f));
            shadow.color = new Color(0f, 0f, 0f, 1f);
            shadow.rectTransform.anchoredPosition = new Vector2(-11f, -11f);
            bindings.Add(Bind(shadow, BlumgiArtAddress.Panel9PAddress, Image.Type.Sliced));

            Image board = NewImage("Panel", panel.transform, new Vector2(358f, 295f));
            bindings.Add(Bind(board, BlumgiArtAddress.Panel9PAddress, Image.Type.Sliced));

            // 안쪽 하늘색 화면 — world 38,36,438,286 → 1920 중심 (621,136) · 337×211
            Image screen = NewImage("Screen", panel.transform, new Vector2(337f, 211f));
            screen.color = Hex("85E0FF");
            screen.rectTransform.anchoredPosition = new Vector2(621f - 615f, -(136f - 157f));
            bindings.Add(Bind(screen, BlumgiArtAddress.TutorialScreenAddress));

            // ★★★ 미니 그림 — <b>컷마다 «그림도 크기도 자리도» 바뀐다</b> [실측 25·26회차].
            //   원본 `Sprite2` 는 3프레임짜리 한 오브젝트인데 시트가 컷마다 «다른 프레임»을 세운다:
            //     컷 A = f1 · 152 × 154 world · 중심 (195, 199)
            //     컷 B = f2 · 230 × 258 world · 중심 (255, 147)
            //   ⇒ 한 장을 돌려 쓰면 컷 B 에서 «안 커지고 자리도 안 옮겨간다» — 그래서 두 벌이다.
            //   1920 환산 — x = (worldX + 497.778) × 0.84375 · y = worldY × 0.84375 (기준 레이어).
            Image artHold = NewImage("ArtHold", panel.transform, new Vector2(128.25f, 129.94f));
            artHold.rectTransform.anchoredPosition = new Vector2(584.53f - 615f, -(167.91f - 157f));
            bindings.Add(Bind(artHold, BlumgiArtAddress.TutorialArtAddress));

            Image artShoot = NewImage("ArtShoot", panel.transform, new Vector2(194.06f, 217.69f));
            artShoot.rectTransform.anchoredPosition = new Vector2(635.15f - 615f, -(124.03f - 157f));
            bindings.Add(Bind(artShoot, BlumgiArtAddress.TutorialArtShootAddress));

            // 문구 2개 — «한 텍스트의 문자열을 갈아 끼우는 것이 아니다» [실측]
            GameObject holdCut = NewUI("HoldCut", panel.transform, new Vector2(255f, 83f));
            AnchorCenterOffset(holdCut, 539f - 615f, -(273f - 157f), new Vector2(255f, 83f));
            TextMeshProUGUI hold = NewText("HOLD", holdCut.transform, "HOLD", 44);
            hold.color = Color.black;   // [실측 캡처] 튜토 문구는 «검정»이다 (흰 채움 + 외곽선이 아니다)
            Stretch(hold.gameObject);

            GameObject shootCut = NewUI("ShootCut", panel.transform, new Vector2(247f, 78f));
            AnchorCenterOffset(shootCut, 570f - 615f, -(270f - 157f), new Vector2(247f, 78f));
            TextMeshProUGUI shoot = NewText("SHOOT", shootCut.transform, "SHOOT!", 44);
            shoot.color = Color.black;
            Stretch(shoot.gameObject);

            // ★★★ 마우스 — <b>세 벌</b>이다 [실측 26회차 런타임 덤프 · 패스 ②-k 정정].
            //   패스 ②-j 까지는 <b>한 벌</b>만 있었고 그것도 «그림자 자리»에 흰 그림을 얹고 있었다.
            //
            //   | uid | bbox (world) | 중심 | tint | 프레임 | vis |
            //   |---|---|---|---|---|---|
            //   | 600173 | 326.6,319.1,409.4,418.9 | (368, 369) | **(0,0,0)** | f0 | **항상** |
            //   | 670095 | 328.6,314.1,411.4,413.9 | (370, 364) | (255,255,255) | f0 | `HOLD` 컷과 같이 |
            //   | 258047 | 328.6,294.2,411.4,393.8 | (370, 344) | (255,255,255) | f1 | `SHOOT!` 컷과 같이 |
            //
            //   ⇒ <b>검정 그림자 1(고정) + 흰 컷 2(교대)</b>. 흰 두 벌은 «프레임»만이 아니라
            //     <b>자리도 20 world 다르다</b> — 한 이미지의 스프라이트만 갈면 그 20 이 사라진다.
            //   ⚠ 골 화살표(흰 + 검정 25 아래)와 <b>같은 수법</b>이다 — 낱개로 옮기면 그림자가 빠진다
            //     (UIUX 「CSS 클래스 하나가 만드는 요소를 낱개로 옮기면 반드시 하나가 빠진다」).
            //
            //   1920 환산 — 중심 x = (worldX + 497.778) × 0.84375 · y = worldY × 0.84375, 크기 70×84.
            const float MouseW = 70f;
            const float MouseH = 84f;

            Image mouseShadow = NewImage("MouseShadow", panel.transform, new Vector2(MouseW, MouseH));
            mouseShadow.rectTransform.anchoredPosition = new Vector2(731f - 615f, -(311f - 157f));
            mouseShadow.color = Color.black;
            bindings.Add(Bind(mouseShadow, BlumgiArtAddress.MouseDownAddress));

            // `HOLD` 컷과 짝 — 원본에서 `HOLD` 가 보이는 프레임에 «같이» 보인 흰 벌이다.
            Image mouseHold = NewImage("MouseHold", panel.transform, new Vector2(MouseW, MouseH));
            mouseHold.rectTransform.anchoredPosition = new Vector2(732f - 615f, -(307f - 157f));
            bindings.Add(Bind(mouseHold, BlumgiArtAddress.MouseDownAddress));

            // `SHOOT!` 컷과 짝 — 20 world 위(1920 에서 17 px)로 올라가고 프레임이 f1 이다.
            Image mouseShoot = NewImage("MouseShoot", panel.transform, new Vector2(MouseW, MouseH));
            mouseShoot.rectTransform.anchoredPosition = new Vector2(732f - 615f, -(290f - 157f));
            bindings.Add(Bind(mouseShoot, BlumgiArtAddress.MouseUpAddress));

            var view = panel.AddComponent<BlumgiTutorialPanel>();
            SetRef(view, "_holdCut", holdCut);
            SetRef(view, "_shootCut", shootCut);
            SetRef(view, "_mouseHold", mouseHold);
            SetRef(view, "_mouseShoot", mouseShoot);
            SetRef(view, "_mouseShadow", mouseShadow);
            SetRef(view, "_artHold", artHold.gameObject);
            SetRef(view, "_artShoot", artShoot.gameObject);

            return view;
        }

        /// <summary>
        /// WELCOME 화면.
        /// ★★ 좌표는 <b>UIUX 2-a 의 정규화 값을 그대로</b> 쓴다 — <c>Main</c> 레이어만 2배인
        /// «두 배율» 함정을 통과하지 않으려면 <b>world 를 환산하지 않는 것</b>이 유일하게 안전한 길이다.
        /// </summary>
        private static void BuildWelcomeWindow()
        {
            GameObject root = NewUI("BlumgiWelcomeWindow", null, Vector2.zero);
            Stretch(root);

            var bindings = new List<BlumgiUiArtBinder.Binding>();

            // 배경 판 — 웰컴은 게임 카메라가 아니라 창이 배경을 든다.
            Image background = NewImage("BG", root.transform, Vector2.zero);
            Stretch(background.gameObject);
            background.color = Hex("47C8FF");   // 웰컴 배경 [실측 05_연출 §1 W1L2 계열과 같은 하늘색]
            bindings.Add(Bind(background, BlumgiArtAddress.SolidAddress));

            // ★★★ BG 장식 5개 — 단색 판 «바로 위» · SafeArea(내용물) «아래».
            //    ⚠ SafeArea 안에 넣지 않는다 — 원본 BG 는 화면 전체를 덮는 레이어라 노치 여백을 안 탄다.
            BlumgiWelcomeBackground welcomeBg = BuildWelcomeBackground(root.transform, bindings);

            GameObject safe = NewSafeArea(root.transform);

            // ── ★★★ 반짝임 이미터 (`FXChling` · `eStart/5/3/1`) [실측 25회차 · 시트 파라미터 4,000회 재평가]
            //   `Main` 뷰포트 중심 ± `random(−640, +640)` world 정사각. 마지막 줄의 `SetAsFirstSibling()` 이
            //   원본 `ZOrderMoveToBottom()` 이다 — 웰컴 반짝임«만» 레이어 맨 뒤로 간다.
            //   ⚠ 동시 존재 실측 1~2 라 풀은 4로 넉넉히 잡는다 (「2개」는 그 캡처 순간의 수였다).
            BuildUiSparkleField(safe.transform, WelcomeSparkleHalfExtentWorld, 4, bindings);

            // 하단 그라디언트 — 중심 (960,945) · 1919×1080
            Image gradient = NewImage("Gradient", safe.transform, new Vector2(1919f, 1080f));
            AnchorFromTop(gradient.gameObject, 0f, 945f, new Vector2(1919f, 1080f));
            gradient.raycastTarget = false;
            bindings.Add(Bind(gradient, BlumgiArtAddress.GradientAddress));

            // 1 PLAYER 카드 — 중심 (589,413) · 674×675
            GameObject oneCard = NewUI("OnePlayerCard", safe.transform, new Vector2(674f, 675f));
            AnchorFromTop(oneCard, 589f - RefWidth * 0.5f, 413f, new Vector2(674f, 675f));

            // ★★★ [패스 ②-k] 카드 «검정 그림자 사본» — 이관 공백이었다.
            //   원본은 같은 `PLAYERButton` 을 «두 벌» 둔다 — 뒤에 <b>tint (0,0,0)</b> 사본을
            //   world +10,+10 만큼 어긋나게 깔고 그 위에 흰 카드를 얹는다
            //   [실측 24·26회차 런타임 덤프 — uid 593536 bbox(230,375,630,775) tint(0,0,0) zi 2 ·
            //    흰 카드 uid 970275 bbox(220,365,620,765) tint(255,255,255) zi 4].
            //   ⚠ 캡처로도 닫혔다 — 카드 «오른쪽» 검정 띠가 11 px 인데 «왼쪽»은 4 px 다
            //     (836×470 캡처 · 차 7 px = 10 world × 0.734). 그림자가 실제로 «보인다».
            //   ⚠ <b>raycastTarget 을 켜지 않는다</b> — 켜면 카드 밖 7 px 이 클릭 영역이 된다.
            Image oneShadow = NewImage("CardShadow", oneCard.transform, new Vector2(675f, 675f));
            oneShadow.rectTransform.anchoredPosition = new Vector2(606f - 589f, -(430f - 413f));
            oneShadow.color = Color.black;
            bindings.Add(Bind(oneShadow, BlumgiArtAddress.PlayerCardAddress));

            Image oneCardImage = NewImage("Card", oneCard.transform, new Vector2(674f, 675f));
            bindings.Add(Bind(oneCardImage, BlumgiArtAddress.PlayerCardAddress));

            // ★★ [패스 ③ 실측] 카드가 «눌리지 않았다». NewImage 는 기본이 raycastTarget = false 인데
            //    Button 은 자기 GameObject 에 Graphic 이 없어 «맞을 것»이 통째로 없었다 —
            //    EventSystem.RaycastAll 이 이 카드를 한 번도 안 돌려준다. 아이콘 버튼은 명시적으로 켜 둬서
            //    티가 안 났고, 이 카드 하나만 조용히 죽어 있었다 (재생 검사가 잡았다).
            oneCardImage.raycastTarget = true;

            Button oneButton = oneCard.AddComponent<Button>();
            oneButton.targetGraphic = oneCardImage;

            // ⚠ 카드 헤더 문자는 원본이 «스프라이트에 구워»져 있다 [04 §4-c].
            //   우리는 서체 자체가 대체(의도된 차이 #5)라 TMP 로 얹는다 — PD 등재 요청 항목.
            TextMeshProUGUI oneLabel = NewText("Label", oneCard.transform, "1 PLAYER", 62);
            AnchorFromTop(oneLabel.gameObject, 0f, 68f, new Vector2(600f, 90f));

            // 2 PLAYERS 카드 — 중심 (1331,413). 확정표 C 로 «꺼 둔다».
            GameObject twoCard = NewUI("TwoPlayersCard", safe.transform, new Vector2(674f, 675f));
            AnchorFromTop(twoCard, 1331f - RefWidth * 0.5f, 413f, new Vector2(674f, 675f));

            // 그림자 사본 — 1P 과 같은 구조다 (중심 1348,430 · 674×675) [04 §2-a].
            Image twoShadow = NewImage("CardShadow", twoCard.transform, new Vector2(674f, 675f));
            twoShadow.rectTransform.anchoredPosition = new Vector2(1348f - 1331f, -(430f - 413f));
            twoShadow.color = Color.black;
            bindings.Add(Bind(twoShadow, BlumgiArtAddress.PlayerCardAddress));

            Image twoCardImage = NewImage("Card", twoCard.transform, new Vector2(674f, 675f));
            bindings.Add(Bind(twoCardImage, BlumgiArtAddress.PlayerCardAddress));
            twoCard.SetActive(false);

            // 손가락 커서 — 중심 (614,751) · 169×169
            GameObject hand = NewUI("HandCursor", safe.transform, new Vector2(169f, 169f));
            AnchorFromTop(hand, 614f - RefWidth * 0.5f, 751f, new Vector2(169f, 169f));
            Image handImage = NewImage("Icon", hand.transform, new Vector2(169f, 169f));
            bindings.Add(Bind(handImage, BlumgiArtAddress.HandCursorAddress));

            // WELCOME — 중심 (960,1021). ★ 글자별 파동 + 외곽선 8 world [실측]
            TextMeshProUGUI welcome = NewText("WelcomeText", safe.transform, "WELCOME", 120);
            AnchorFromTop(welcome.gameObject, 0f, 1021f, new Vector2(1349f, 337f));
            welcome.gameObject.AddComponent<BlumgiWaveText>();
            // ★ 외곽선 8 world × `Main` 배율 1.6875 = 13.5 px @1920 [실측 25회차].
            ApplyOutline(welcome, 8f, MainScale);

            // 크레딧 2줄 — 중심 (359,1021). ⚠ 두 줄의 «크기가 다르다» (35 / 30 world)
            // ★ [확인 · 25회차] 크레딧은 `TextUI` #904361 = **`FG` 레이어**라 배율이 `UiScale`(0.84375) 이 맞다 —
            //   같은 화면의 `WELCOME`(`Main` · 1.6875)과 «배율이 다르다». 셋을 한 배율로 묶으면 여기가 2배가 된다.
            //   ⇒ 35 → 29.53 px · 30 → 25.31 px. 정수 반올림을 걷어내고 실측값 그대로 넣는다.
            TextMeshProUGUI credit1 = NewText("Credit1", safe.transform,
                                              "BLUMGI X VENTUROUS", Mathf.RoundToInt(35f * UiScale));
            credit1.fontSize = 35f * UiScale;   // 29.53125 px
            AnchorFromTop(credit1.gameObject, 359f - RefWidth * 0.5f, 1000f, new Vector2(675f, 42f));
            credit1.color = Hex("2F9FD6");

            TextMeshProUGUI credit2 = NewText("Credit2", safe.transform,
                                              "MUSIC BY MUSMUS", Mathf.RoundToInt(30f * UiScale));
            credit2.fontSize = 30f * UiScale;   // 25.3125 px
            AnchorFromTop(credit2.gameObject, 359f - RefWidth * 0.5f, 1040f, new Vector2(675f, 38f));
            credit2.color = Hex("2F9FD6");

            // 🔊 — 중심 (1869,51) · 84×84
            Button soundButton = NewIconButton("SoundButton", safe.transform, 60f, bindings,
                                               BlumgiArtAddress.ButtonSoundOnAddress, out Image soundIcon);

            var window = root.AddComponent<BlumgiWelcomeWindow>();
            SetEnum(window, "_windowType", EWindowType.Normal);
            SetRef(window, "_onePlayerButton", oneButton);
            SetRef(window, "_twoPlayersCard", twoCard);
            SetRef(window, "_welcomeText", welcome);
            SetRef(window, "_creditLine1", credit1);
            SetRef(window, "_creditLine2", credit2);
            SetRef(window, "_handCursor", hand);
            SetRef(window, "_soundButton", soundButton);
            SetRef(window, "_soundIcon", soundIcon);
            SetRef(window, "_background", welcomeBg);

            AttachBinder(root, bindings);
            SetLayerRecursively(root, UiLayer);
            SavePrefab(root, UiPrefabRoot + "/BlumgiWelcomeWindow.prefab");
        }

        /// <summary>
        /// ★★★ WELCOME 의 <c>BG</c> 장식 <b>5개</b>를 굽는다 —
        /// 격자(op 0.5) · 야자수 잎 2 · 기둥 2 · 틴트 <c>(58,177,228)</c>.
        ///
        /// <para>
        /// ⚠⚠ <b>인게임 배경(<see cref="BlumgiBackgroundLayout"/>)을 갖다 쓰지 않는다</b> —
        /// 개수(11 ↔ 5) · 좌표 · 틴트 · 격자 피치(232.5 ↔ 150) · 격자 불투명도(1 ↔ 0.5)가 <b>전부 다르다</b>.
        /// 표는 <see cref="BlumgiWelcomeBackgroundLayout"/> 에 «따로» 있다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>환산은 <see cref="BlumgiWelcomeBackgroundLayout"/> 한 곳만 지난다.</b>
        /// 웰컴 <c>BG</c> 는 <c>zElevation = 0</c> 이라 <b>인게임의 ×1/3 이 «없다»</b> —
        /// 여기서 배율을 다시 곱하면 그 규칙이 두 곳으로 갈린다.
        /// </para>
        ///
        /// <para>
        /// ★ 그리는 순서는 <b>형제 순서</b>가 든다 (원본 <c>zIndex</c> 0·1·2·3·4 오름차순 그대로).
        /// </para>
        /// </summary>
        private static BlumgiWelcomeBackground BuildWelcomeBackground(
            Transform parent, List<BlumgiUiArtBinder.Binding> bindings)
        {
            GameObject decorRoot = NewUI("BgDecor", parent, Vector2.zero);
            Stretch(decorRoot);

            var palms = new List<Image>(4);
            Image grid = null;

            BlumgiWelcomeBackgroundLayout.Decor[] decors = BlumgiWelcomeBackgroundLayout.Decors;

            for (int i = 0; i < decors.Length; i++)
            {
                BlumgiWelcomeBackgroundLayout.Decor decor = decors[i];

                double bakedHeight = BlumgiWelcomeBackgroundLayout.BakedWorldHeight(decor);
                double centerY = BlumgiWelcomeBackgroundLayout.BakedWorldCenterY(decor);

                // 앵커는 «캔버스 가로 중심 · 캔버스 위»다 — 16:9 가 아니어도 그대로 맞는다.
                float offsetX = BlumgiWelcomeBackgroundLayout.ToCanvasOffsetX(decor.WorldCenterX, RefHeight);
                float topDistance = BlumgiWelcomeBackgroundLayout.ToCanvasTopDistance(centerY, RefHeight);

                Vector2 size;
                float localScale = 1f;

                if (decor.Tiled)
                {
                    // ★ 타일드는 «배율 안의 로컬 px» 로 재고 트랜스폼이 imageScale 을 든다 —
                    //   레벨 도트 바에서 실측으로 확인한 그 규칙이다 (타일의 자 = 칸의 자).
                    size = new Vector2(
                        BlumgiWelcomeBackgroundLayout.TileLocalLength(decor.WorldWidth, decor.ImageScale),
                        BlumgiWelcomeBackgroundLayout.TileLocalLength(bakedHeight, decor.ImageScale));
                    localScale = BlumgiWelcomeBackgroundLayout.TileLocalScale(decor.ImageScale, RefHeight);
                }
                else
                {
                    size = new Vector2(
                        BlumgiWelcomeBackgroundLayout.ToCanvasLength(decor.WorldWidth, RefHeight),
                        BlumgiWelcomeBackgroundLayout.ToCanvasLength(bakedHeight, RefHeight));
                }

                Image image = NewImage(decor.Name, decorRoot.transform, size);
                AnchorFromTop(image.gameObject, offsetX, topDistance, size);
                image.transform.localScale = new Vector3(localScale, localScale, 1f);

                // ⚠ 순백 마스크에 «런타임 틴트» — 결과 색을 텍스처에 굽지 않는다 (재발방지 #94).
                image.color = decor.Tint;

                bindings.Add(Bind(image, decor.Address,
                                  decor.Tiled ? Image.Type.Tiled : Image.Type.Simple));

                if (decor.Address == BlumgiArtAddress.GridTileAddress)
                    grid = image;
                else
                    palms.Add(image);
            }

            var view = decorRoot.AddComponent<BlumgiWelcomeBackground>();
            SetRef(view, "_grid", grid);
            SetRefArray(view, "_palms", palms.ToArray());

            return view;
        }

        /// <summary>클리어 연출 — 흰 플래시 · <c>YES!</c> · 흰색 페이드 전환. 컨페티는 «월드»라 배선이 넣는다.</summary>
        private static void BuildClearOverlay()
        {
            GameObject root = NewUI("BlumgiClearOverlay", null, Vector2.zero);
            Stretch(root);

            var bindings = new List<BlumgiUiArtBinder.Binding>();

            Image flash = NewImage("Flash", root.transform, Vector2.zero);
            Stretch(flash.gameObject);
            flash.color = new Color(1f, 1f, 1f, 0f);
            bindings.Add(Bind(flash, BlumgiArtAddress.SolidAddress));

            // ── YES! — 화면 중앙~좌중앙 · 폭 ≈ 화면의 1/3 [실측]
            GameObject yes = NewUI("Yes", root.transform, new Vector2(RefWidth / 3f, 260f));
            AnchorFromTop(yes, -140f, RefHeight * 0.45f, new Vector2(RefWidth / 3f, 260f));

            var yesGroup = yes.AddComponent<CanvasGroup>();
            TextMeshProUGUI yesText = NewText("Text", yes.transform, "YES!", 170);
            Stretch(yesText.gameObject);

            // ★★ [정정 · 17회차] `YES!` 는 «검은 외곽선»이 아니다 — ApplyOutline 을 걸지 않는다.
            //    소스 텍스처 픽셀 실측이 «흰 채우기 + 핑크(255,102,154) 외곽선»이었고,
            //    색상 회전이 AdjustHSL(hue,1,1) 이라 «흰색은 안 변하고 외곽선만 돈다».
            //    ⇒ 채움·외곽선 둘 다 BlumgiRainbowText 가 «런타임»에 넣는다
            //      (프리팹에 구우면 fontMaterial 인스턴스가 저장 때 날아간다 — ApplyOutline 진단).
            yesText.color = Color.white;
            yesText.gameObject.AddComponent<BlumgiRainbowText>();

            yes.SetActive(false);

            // ── 흰색 페이드 전환 — «컷이 아니다» [실측]
            Image fade = NewImage("Fade", root.transform, Vector2.zero);
            Stretch(fade.gameObject);
            fade.color = new Color(1f, 1f, 1f, 0f);
            bindings.Add(Bind(fade, BlumgiArtAddress.SolidAddress));

            var window = root.AddComponent<BlumgiClearOverlay>();
            SetEnum(window, "_windowType", EWindowType.Popup);
            SetRef(window, "_flash", flash);
            SetRef(window, "_fade", fade);
            SetRef(window, "_yesText", yesText);
            SetRef(window, "_yesRect", yes.GetComponent<RectTransform>());
            SetRef(window, "_yesGroup", yesGroup);

            AttachBinder(root, bindings);
            SetLayerRecursively(root, UiLayer);
            SavePrefab(root, UiPrefabRoot + "/BlumgiClearOverlay.prefab");
        }

        // ══════════════════════════════════════════════ 물리 — 실측값은 «데이터»에서 온다

        /// <summary>
        /// 설정 표를 <b>실제 데이터 경로</b>로 읽는다 — 굽는 도구가 값을 따로 들고 있으면
        /// 다음 실측이 왔을 때 <b>한쪽만 고쳐진다</b>.
        /// </summary>
        private static BlumgiConfigData LoadPhysicsConfig()
        {
            if (Directory.Exists(DataFolder) == false)
                return null;

            var map = new Dictionary<string, string>();

            foreach (string path in Directory.GetFiles(DataFolder, "*.json"))
                map[Path.GetFileNameWithoutExtension(path)] = File.ReadAllText(path);

            BlumgiContainerRegister.RegisterAll();
            DataManager.Instance.InitializeFromJson(map);

            return GameRoot.Instance.BlumgiConfigDataContainer?.Config;
        }

        /// <summary>
        /// 물리 머티리얼을 굽는다.
        ///
        /// <para>
        /// ★★ <b>반발 «혼합»은 유니티 2D 가 Box2D 그대로 <c>max(e₁, e₂)</c> 로 한다</b> —
        /// 그래서 <b>블록 0 · 골대 0.7 · 블롭 0.2 를 «실측대로» 넣어도 공(0.7)과의 실효 반발은 전부 0.7</b> 이 된다
        /// [4회차 실측: 공 0.7 × 블록 0 인데 관측 반발 0.700]. 평균이나 곱이면 절대 그 값이 안 나온다.
        /// ⚠ <b>실효값을 맞추려고 블록 반발을 0.7 로 «올리지» 않는다</b> — 그건 없는 실측을 지어내는 것이고,
        /// 혼합 규칙이 바뀌는 날 조용히 틀린다. 혼합이 정말 <c>max</c> 인지는 <b>채점기가 잰다</b>.
        /// </para>
        /// </summary>
        private static PhysicsMaterial2D EnsureMaterial(string assetName, double bounciness, double friction)
        {
            string path = $"{PhysicsMaterialRoot}/{assetName}.physicsMaterial2D";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);

            if (material == null)
            {
                material = new PhysicsMaterial2D(assetName);
                AssetDatabase.CreateAsset(material, path);
            }

            material.bounciness = (float)bounciness;
            material.friction = (float)friction;

            // ★★ 혼합 규칙을 «명시»한다 [19회차 반영]. 지금 유니티 기본값과 같지만 «기본값이라 안 적는다»가
            //   위험한 자리다 — 엔진 판이 바뀌거나 누가 인스펙터에서 바꾸면 실효 반발이 0.7 → 0.35 로
            //   조용히 갈리고, 그건 «값»이 아니라 «규칙»이 바뀐 것이라 데이터 대조로 안 잡힌다.
            //   ⚠ Mean 은 이름과 달리 «기하» 평균이라 Box2D 의 √(f₁·f₂) 와 같은 식이다.
            material.bounceCombine = BlumgiPhysicsSetup.Box2DBounceCombine;
            material.frictionCombine = BlumgiPhysicsSetup.Box2DFrictionCombine;

            EditorUtility.SetDirty(material);
            return material;
        }

        // ══════════════════════════════════════════════ 유틸

        /// <summary>
        /// 어드레서블 아트를 «어느 <c>Image</c> 에 · 어떻게 그려» 물릴지 한 줄로 적는다.
        ///
        /// <para>
        /// ★ <b>여기서 <c>Image.type</c> 까지 «같이» 세운다.</b> 프리팹의 <c>type</c> 과 바인딩의 <c>Draw</c> 가
        /// 서로 다른 자리에 있으면 반드시 어긋난다 — 실제로 어긋나서 <b>런타임이 프리팹의 <c>Tiled</c> 를
        /// <c>Simple</c> 로 덮어썼고</b>, 도트 바가 5칸이 아니라 «늘어난 2칸»으로 떴다 [2026-08-30 캡처].
        /// </para>
        /// </summary>
        private static BlumgiUiArtBinder.Binding Bind(Image target, string address,
                                                      Image.Type draw = Image.Type.Simple)
        {
            target.type = draw;
            return new BlumgiUiArtBinder.Binding { Target = target, Address = address, Draw = draw };
        }

        private static void AttachBinder(GameObject root, List<BlumgiUiArtBinder.Binding> bindings)
        {
            var binder = root.AddComponent<BlumgiUiArtBinder>();
            binder.EditorSetBindings(bindings.ToArray());
        }

        /// <summary>
        /// 우상단 아이콘 버튼. 오른쪽 끝에서 <paramref name="rightWorldOffset"/> world 만큼 안쪽,
        /// 크기 100×100 world [UIUX 1-e].
        /// </summary>
        private static Button NewIconButton(string name, Transform parent, float rightWorldOffset,
                                            List<BlumgiUiArtBinder.Binding> bindings,
                                            string address, out Image icon)
        {
            float size = 100f * UiScale;         // 84.375 px
            float right = rightWorldOffset * UiScale;
            float top = 60f * UiScale;           // world y 중심 60 (bbox 10~110)

            GameObject go = NewUI(name, parent, new Vector2(size, size));
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-right, -top);

            icon = NewImage("Icon", go.transform, new Vector2(size, size));
            icon.raycastTarget = true;
            bindings.Add(Bind(icon, address));

            Button button = go.AddComponent<Button>();
            button.targetGraphic = icon;
            return button;
        }

        private static SpriteRenderer NewSprite(string name, Transform parent, string spriteName, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadSprite(spriteName);
            renderer.sortingOrder = order;
            return renderer;
        }

        /// <summary>⚠ 「없으면 조용히 null」이 가장 나쁜 실패다 — 프리팹은 구워지고 화면만 빈다.</summary>
        private static Sprite LoadSprite(string spriteName)
        {
            string path = $"{GameArt}/{spriteName}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite == null)
                Log.Error($"스프라이트를 못 읽었다: {path}"
                          + " (BlumgiSpriteBuilder.BuildAll → BlumgiArtImportSetup.Setup 순서로 돌린다)");

            return sprite;
        }

        private static GameObject NewUI(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));

            if (parent != null)
                go.transform.SetParent(parent, false);

            go.GetComponent<RectTransform>().sizeDelta = size;
            return go;
        }

        private static Image NewImage(string name, Transform parent, Vector2 size)
        {
            GameObject go = NewUI(name, parent, size);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static GameObject NewSafeArea(Transform parent)
        {
            GameObject go = NewUI("SafeArea", parent, Vector2.zero);
            Stretch(go);
            go.AddComponent<SafeArea>();
            return go;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string text, int size)
        {
            GameObject go = NewUI(name, parent, new Vector2(0f, size + 8f));
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = _font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            return tmp;
        }

        /// <summary>
        /// 검은 외곽선. [실측] <c>[lineThickness=8][outlineback=rgb(0,0,0)]</c> — <b>두께 8 world</b>
        /// 이고 <b>글자마다 개별</b>로 걸려 있다.
        ///
        /// <para>
        /// ★ <b>[정정 · 25회차] px 값이 «6.75» 가 아니라 «13.5» 다.</b> 이 외곽선이 걸린 오브젝트
        /// (<c>TextUI</c> #558341 = <c>WELCOME</c>)는 <b><c>Main</c> 레이어</b>라 배율이
        /// 기준 레이어의 «정확히 2배»인 <b>1.6875</b> 다 [실측 뷰포트]. 8 × 1.6875 = <b>13.5 px @1920</b>.
        /// ⚠ 같은 화면의 <b>크레딧은 <c>FG</c></b> 라 0.84375 를 탄다 — 셋을 한 배율로 묶지 않는다.
        /// </para>
        /// ⚠ TMP 의 <c>_OutlineWidth</c> 는 0~1 정규화라 world 두께를 직접 못 넣는다 —
        /// 폰트 <c>padding</c> 대비 비율로 환산한다. <b>정확한 대응은 렌더 판정에서 잰다.</b>
        ///
        /// <para>
        /// ★★★ <b>[진단 2026-08-30] 이 외곽선은 «화면에 안 나온다». 원인을 규명했고, 고치지는 «않았다».</b>
        ///
        /// <list type="number">
        /// <item><b>기전 결함(확증).</b> <c>text.fontMaterial</c> 은 «그 자리에서 새 <c>Material</c> 인스턴스를
        /// 만드는» 프로퍼티인데 그 인스턴스는 <b>에셋이 아니다</b>. 프리팹을 저장하면 참조가
        /// <c>{fileID: 0}</c> 으로 굳고(구운 프리팹 YAML 에 <b>머티리얼 오브젝트가 0개</b>다),
        /// 런타임은 폰트 에셋의 기본 머티리얼(<c>_OutlineWidth: 0</c>)로 되돌아간다.
        /// ⇒ 고치려면 <b>진짜 <c>.mat</c> 에셋을 구워 <c>fontSharedMaterial</c> 로 물린다.</b></item>
        ///
        /// <item>⚠ <b>임시 대체 서체 탓이 «아니다».</b> 위대로 고쳐 실제로 돌려 보니
        /// <b>임시 서체로도 외곽선이 즉시 나왔다</b> — 자산 대기와 묶이지 않는다.</item>
        ///
        /// <item>⚠ <b>그런데 기전만 고치면 «더 나빠진다».</b> 아래 <c>0~1</c> 환산은 <b>미측정 추정</b>이라
        /// 실제로 걸어 보니 <c>0.3375</c> 가 <b>글자를 통째로 먹어</b> 획이 검게 뭉쳤다
        /// (TMP 외곽선은 «안쪽»으로 자란다).</item>
        ///
        /// <item>⚠ <b>그리고 <c>YES!</c> 는 애초에 «검은» 외곽선이 아니다</b> —
        /// 원본 실측 <c>Fx/G02_seq_14.png</c> 에서 <b>채움이 «흰색»이고 «외곽선»이 색상환을 돈다</b>.
        /// 지금 우리는 <b>정확히 반대</b>(채움이 돌고 외곽선이 검정)다. <c>WORLD 1</c>·<c>WELCOME</c> 쪽이
        /// 검은 외곽선이다.</item>
        /// </list>
        ///
        /// ⇒ <b>두께 환산과 «어느 쪽이 도는가»를 먼저 재고</b>, 그 다음에 기전을 고친다.
        /// 재지 않은 값으로 기전만 고치면 화면이 원본에서 «더» 멀어진다 —
        /// 그래서 이 회차는 <b>진단만 남기고 되돌렸다</b>.
        /// </para>
        /// </summary>
        private static void ApplyOutline(TextMeshProUGUI text, float worldThickness, float pixelScale)
        {
            if (text.font == null)
            {
                Log.Warning($"폰트가 없어 외곽선을 못 걸었다: {text.name}");
                return;
            }

            text.fontMaterial.EnableKeyword("OUTLINE_ON");
            text.outlineColor = Color.black;

            // ⚠ 배율(`pixelScale`)은 25회차에 닫혔지만 «px → TMP 0~1» 환산(아래 20f)은 여전히 «미측정 추정»이다.
            //   위 진단대로 기전(폰트 머티리얼 에셋)까지 고치려면 이 나눗셈을 «먼저» 재야 한다.
            text.outlineWidth = Mathf.Clamp01(worldThickness * pixelScale / 20f);
        }

        private static void Stretch(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>화면 «위»에서 잰 좌표로 앉힌다 — 원본 y 가 위에서 재는 값이라 표를 그대로 옮길 수 있다.</summary>
        private static void AnchorFromTop(GameObject go, float centerOffsetX, float topDistance, Vector2 size)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(centerOffsetX, -topDistance);
        }

        private static void AnchorCenterOffset(GameObject go, float x, float y, Vector2 size)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(x, y);
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;

            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int index = path.LastIndexOf('/');
            string parent = index < 0 ? string.Empty : path.Substring(0, index);
            string leaf = path.Substring(parent.Length + 1);

            if (AssetDatabase.IsValidFolder(parent) == false)
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void SavePrefab(GameObject root, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);

            if (success == false)
                Log.Error($"프리팹 저장 실패: {path}");

            Object.DestroyImmediate(root);
        }

        // ── 직렬화 필드 주입. ⚠ 「썼다」가 아니라 «됐다»로 확인한다.

        private static void SetRef(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
            {
                Log.Error($"{target.GetType().Name} 에 {field} 이 없다");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (value == null)
                return;

            var verify = new SerializedObject(target);

            if (verify.FindProperty(field).objectReferenceValue != value)
                Log.Error($"{target.GetType().Name}.{field} 이 안 들어갔다");
        }

        private static void SetRefArray<T>(Object target, string field, T[] values) where T : Object
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
            {
                Log.Error($"{target.GetType().Name} 에 {field} 이 없다");
                return;
            }

            property.arraySize = values.Length;

            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetColorArray(Object target, string field, Color[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
            {
                Log.Error($"{target.GetType().Name} 에 {field} 이 없다");
                return;
            }

            property.arraySize = values.Length;

            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).colorValue = values[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
            {
                Log.Error($"{target.GetType().Name} 에 {field} 이 없다");
                return;
            }

            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector2(Object target, string field, Vector2 value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
            {
                Log.Error($"{target.GetType().Name} 에 {field} 이 없다");
                return;
            }

            property.vector2Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(Object target, string field, System.Enum value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
            {
                Log.Error($"{target.GetType().Name} 에 {field} 이 없다");
                return;
            }

            property.enumValueIndex = System.Convert.ToInt32(value);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }
    }
}
