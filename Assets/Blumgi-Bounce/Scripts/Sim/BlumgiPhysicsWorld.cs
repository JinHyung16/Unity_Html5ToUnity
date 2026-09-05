using System.Collections.Generic;
using JinHyung.Core;
using JinHyung.Data;
using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>프리팹을 이름으로 건네주는 주입점. 게임은 Addressables, 검사는 에디터 경로로 준다.</summary>
    public delegate GameObject BlumgiPrefabLoader(string prefabName);

    /// <summary>
    /// 레벨 하나의 <b>물리 실체</b>를 세운다 — 블록 · 골대 · 블롭 · 공.
    ///
    /// <para>
    /// ★★ <b>콜라이더 «크기»는 여기서 정하지 않는다.</b> 프리팹이 들고 온다 (확정표 10-b —
    /// 프리팹은 빌더가 굽고 손으로 안 고친다). 여기가 정하는 것은 <b>«어디에 놓느냐»</b>뿐이다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>배치 좌표는 데이터 그대로다.</b> 격자 피치(50)로 재구성하지 않는다 —
    /// 그러면 원본 이상 #3(W1L1 원점 +2)이 반올림돼 사라진다.
    /// 그리고 <b>콜라이더는 84 × 58.983 이라 피치보다 크다</b> — 이웃끼리 겹치는 것이 원본이고,
    /// 겹치지 않게 «정돈»하면 그 틈으로 공이 빠진다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlumgiPhysicsWorld : MonoBehaviour
    {
        public const string BlockPrefabName = "BlumgiBlock";
        public const string BallPrefabName = "BlumgiBall";
        public const string BlobPrefabName = "BlumgiBlob";
        public const string HoopPrefabName = "BlumgiHoop";

        private readonly List<GameObject> _spawned = new List<GameObject>(256);

        private Transform _blockRoot;

        /// <summary>지금 세워진 레벨. 아무것도 안 세웠으면 <c>null</c>.</summary>
        public BlumgiLevelRuntime Level { get; private set; }

        /// <summary>공의 물리 바디. 프리팹이 없으면 <c>null</c> — 대체를 만들지 않는다.</summary>
        public BlumgiBallBody Ball { get; private set; }

        /// <summary>블롭의 강체. 동적이라 공에 밀린다(밀도 100 이라 거의 안 밀리지만 0 은 아니다).</summary>
        public Rigidbody2D Blob { get; private set; }

        /// <summary>
        /// 블롭의 충돌체. <b>공과의 쌍만</b> 충돌에서 빠진다 —
        /// <b>블록·바닥과는 계속 부딪힌다</b> (<see cref="BlumgiBallBody.ExcludeFromCollision"/>).
        /// </summary>
        public Collider2D BlobCollider { get; private set; }

        /// <summary>세운 블록 수. 데이터 행 수와 «같아야» 한다 — 다르면 프리팹이 하나 안 나온 것이다.</summary>
        public int BlockCount { get; private set; }

        /// <summary>
        /// 레벨 하나를 세운다. 이미 세워져 있으면 <b>먼저 헐고</b> 다시 세운다.
        /// </summary>
        public void Build(BlumgiLevelRuntime level, BlumgiPrefabLoader loader)
        {
            Teardown();

            if (level == null || loader == null)
            {
                Log.Error("물리 월드를 세울 수 없다 — 레벨이나 프리팹 로더가 없다");
                return;
            }

            Level = level;
            BlumgiLevelData data = level.Level;

            GameObject blockPrefab = loader(BlockPrefabName);

            if (blockPrefab == null)
            {
                Log.Error($"프리팹을 못 읽었다: {BlockPrefabName}");
                return;
            }

            var blockRootGo = new GameObject("Blocks");
            blockRootGo.transform.SetParent(transform, false);
            _blockRoot = blockRootGo.transform;
            _spawned.Add(blockRootGo);

            IReadOnlyList<BlumgiVec2> centers = level.BlockCenters;
            BlockCount = 0;

            for (int i = 0; i < centers.Count; i++)
            {
                GameObject block = Instantiate(blockPrefab, _blockRoot);
                block.transform.localPosition = BlumgiUnits.ToPosition(centers[i].X, centers[i].Y);
                BlockCount++;
            }

            Spawn(loader, HoopPrefabName, data.GoalRimX, data.GoalRimY);

            // ★★★ 발사대 자리는 «레벨 데이터»가 아니라 «유도값»이다 [20회차 · 재발방지 #102] —
            //   authored 자리에서 떨어져 아래 블록 위에 정착한 위치를 BlumgiLevelRuntime 이 푼다.
            //   ⚠ 블록을 먼저 세워야 하는 것이 아니라, 유도가 블록 «데이터»를 읽는다 (순서 무관).
            BlumgiVec2 launcher = level.LauncherBodyPosition;

            GameObject blob = Spawn(loader, BlobPrefabName, launcher.X, launcher.Y);

            if (blob != null)
            {
                Blob = blob.GetComponent<Rigidbody2D>();
                BlobCollider = blob.GetComponent<Collider2D>();
            }

            // ★ 공은 «경기장 밖»에서 시작한다 — 원본은 레벨이 깔리면 물리 공을 (0, 10000) 에 두고
            //   그대로 떨어뜨린다 [6회차 실측]. 머리 위에 보이는 공은 별개 오브젝트(spr_BallVisu)다.
            GameObject ball = Spawn(loader, BallPrefabName, level.BallParkPosition.X, level.BallParkPosition.Y);

            if (ball != null)
            {
                Ball = ball.GetComponent<BlumgiBallBody>();

                if (Ball == null)
                {
                    Log.Error($"{BallPrefabName} 에 {nameof(BlumgiBallBody)} 가 없다 — 프리팹을 다시 굽는다");
                }
                else
                {
                    // ★★★ «공 ↔ 발사대» 쌍 «하나»만 충돌에서 뺀다 [16회차 실측].
                    //   원본에서 공은 발사대 콜라이더를 66~135 프레임 «완전히 관통»하는데
                    //   그 구간의 접촉 상대는 발사대 «아래» 블록 행뿐이고 블롭은 0 건이다.
                    //   ⚠ 블롭은 살아 있어야 한다 — 깔고 앉은 블록 3개와 계속 접촉 중이다.
                    //     그래서 «블롭을 끄는» 것이 아니라 «쌍을 빼는» 것이다. 순서 주의 —
                    //     블롭을 먼저 세워야 콜라이더가 있다 (위 Spawn 순서가 그렇다).
                    Ball.ExcludeFromCollision(BlobCollider);

                    Ball.Park(BlumgiUnits.ToVector(level.BallParkPosition.X, level.BallParkPosition.Y));
                }
            }
        }

        /// <summary>세운 것을 전부 헌다. 다음 레벨의 블록이 앞 레벨 위에 겹치는 것을 막는다.</summary>
        public void Teardown()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] == null)
                    continue;

                // ⚠ 채점기는 «한 프레임 안»에서 전부 돈다 — Destroy 는 프레임 끝에 미뤄져
                //    앞 레벨의 콜라이더가 살아 있는 채로 다음 레벨을 세우게 된다.
                DestroyImmediate(_spawned[i]);
            }

            _spawned.Clear();
            _blockRoot = null;
            Ball = null;
            Blob = null;
            BlobCollider = null;
            BlockCount = 0;
            Level = null;
        }

        private GameObject Spawn(BlumgiPrefabLoader loader, string prefabName, double worldX, double worldY)
        {
            GameObject prefab = loader(prefabName);

            if (prefab == null)
            {
                Log.Error($"프리팹을 못 읽었다: {prefabName}");
                return null;
            }

            GameObject instance = Instantiate(prefab, transform);
            instance.transform.localPosition = BlumgiUnits.ToPosition(worldX, worldY);
            _spawned.Add(instance);
            return instance;
        }

        private void OnDestroy()
        {
            Teardown();
        }
    }
}
