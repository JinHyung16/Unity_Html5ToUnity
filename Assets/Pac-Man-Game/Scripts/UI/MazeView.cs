using System.Collections.Generic;
using JinHyung.Data;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace JinHyung.PacMan
{
    /// <summary>
    /// 미로를 <b>Tilemap</b> 으로, 팩맨·유령을 스프라이트로 그린다 (확정 C).
    ///
    /// <para>
    /// ⚠ <b>여기는 표기만 한다.</b> 상태는 <see cref="MazeManager"/> 가 든다 —
    /// 뷰가 상태를 들기 시작하면 원본 배열과 그림이 갈린다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>펠릿을 개체로 만들지 않는다.</b> 364개가 상시로 돌 이유가 없다 —
    /// 벽과 같은 Tilemap 에 타일로 얹고, 먹으면 그 칸만 지운다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>격자 y 는 아래로 커지고 유니티 y 는 위로 커진다.</b> 한 곳에서만 뒤집는다
    /// (<see cref="ToWorld"/>) — 두 곳에서 뒤집으면 반드시 한쪽이 틀린다.
    /// </para>
    /// </summary>
    public class MazeView : MonoBehaviour
    {
        [SerializeField] private Tilemap _tilemap;
        [SerializeField] private Transform _entityRoot;

        [SerializeField] private Sprite _wallSprite;
        [SerializeField] private Sprite _pelletSprite;
        [SerializeField] private Sprite _powerSprite;

        [SerializeField] private SpriteRenderer _pacman;
        [SerializeField] private SpriteRenderer _ghostPrefab;
        [SerializeField] private Sprite[] _pacFrames;

        private MazeManager _maze;
        private PacmanManager _pac;
        private GhostManager _ghosts;
        private PacConfigData _config;

        private readonly List<SpriteRenderer> _ghostViews = new List<SpriteRenderer>(2);

        // ⚠ 타일을 «에셋으로» 만들지 않는다 — 스프라이트 한 장을 감싸는 껍데기일 뿐인데
        //   에셋으로 두면 굽는 단계가 하나 늘고, 그 단계가 조용히 실패할 수 있다 (실측: 0/3).
        //   런타임에 만들어 쓴다.
        private TileBase _wallTile;
        private TileBase _pelletTile;
        private TileBase _powerTile;

        public void Bind(MazeManager maze, PacmanManager pac, GhostManager ghosts)
        {
            _maze = maze;
            _pac = pac;
            _ghosts = ghosts;
            _config = GameRoot.Instance.PacConfigDataContainer.Data;

            _maze.OnTileChanged += HandleTileChanged;
            _pac.OnMoved += HandlePacMoved;
            _ghosts.OnMoved += HandleGhostsMoved;

            _wallTile = CreateTile(_wallSprite);
            _pelletTile = CreateTile(_pelletSprite);
            _powerTile = CreateTile(_powerSprite);

            BuildGhostViews();
            RedrawAll();
        }

        private void OnDestroy()
        {
            // ⚠ 구독을 끊지 않으면 다음 사용처에서 죽은 뷰가 두 번 불린다.
            if (_maze != null)
                _maze.OnTileChanged -= HandleTileChanged;

            if (_pac != null)
                _pac.OnMoved -= HandlePacMoved;

            if (_ghosts != null)
                _ghosts.OnMoved -= HandleGhostsMoved;
        }

        private static TileBase CreateTile(Sprite sprite)
        {
            if (sprite == null)
                return null;

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            return tile;
        }

        /// <summary>격자 좌표 → 월드. <b>세로를 뒤집는 곳은 여기 하나뿐</b>이다.</summary>
        private Vector3 ToWorld(float col, float row)
        {
            return new Vector3(col, -row, 0f);
        }

        // ────────────────────────────── 미로

        public void RedrawAll()
        {
            _tilemap.ClearAllTiles();

            for (int r = 0; r < _maze.Rows; r++)
            {
                for (int c = 0; c < _maze.Cols; c++)
                    SetTile(c, r);
            }
        }

        private void HandleTileChanged(int col, int row)
        {
            SetTile(col, row);
        }

        private void SetTile(int col, int row)
        {
            var position = new Vector3Int(col, -row, 0);
            int value = _maze.GetTile(col, row);

            TileBase tile = null;

            if (value == MazeManager.Wall)
                tile = _wallTile;
            else if (value == MazeManager.Pellet)
                tile = _pelletTile;
            else if (value == MazeManager.PowerPellet)
                tile = _powerTile;

            _tilemap.SetTile(position, tile);
        }

        // ────────────────────────────── 팩맨

        private void HandlePacMoved()
        {
            if (_pacman == null)
                return;

            _pacman.transform.localPosition = ToWorld(_pac.X, _pac.Y);

            // 원본 입 애니메이션 — `0.08 + 0.28 * (0.5 + 0.5 * sin(gameTime * chompSpeed))`.
            // ★ 프레임 인덱스를 «중앙에서» 굴린다 — 개체마다 재생 컴포넌트를 붙이지 않는다.
            if (_pacFrames != null && _pacFrames.Length > 0)
            {
                float phase = _pac.GameTime * _config.ChompSpeed;
                int frame = Mathf.Abs(Mathf.FloorToInt(phase / (Mathf.PI * 2f) * _pacFrames.Length))
                            % _pacFrames.Length;

                _pacman.sprite = _pacFrames[frame];
            }

            _pacman.transform.localRotation = Quaternion.Euler(0f, 0f, FacingAngle());
        }

        /// <summary>
        /// 원본 <c>angleOffset</c> (<c>script.js:393~400</c>).
        /// ⚠ <b>멈춰 있으면 오른쪽을 본다</b> — 원본이 그렇다.
        /// ⚠ 격자 y 가 뒤집혀 있으므로 위/아래 각도도 뒤집힌다.
        /// </summary>
        private float FacingAngle()
        {
            if (_pac.DirX == 1)
                return 0f;

            if (_pac.DirX == -1)
                return 180f;

            if (_pac.DirY == -1)
                return 90f;

            if (_pac.DirY == 1)
                return -90f;

            return 0f;
        }

        // ────────────────────────────── 유령

        private void BuildGhostViews()
        {
            for (int i = 0; i < _ghostViews.Count; i++)
            {
                if (_ghostViews[i] != null)
                    Destroy(_ghostViews[i].gameObject);
            }

            _ghostViews.Clear();

            for (int i = 0; i < _ghosts.Ghosts.Count; i++)
            {
                SpriteRenderer view = Instantiate(_ghostPrefab, _entityRoot);
                view.gameObject.name = $"Ghost_{i}";

                // 템플릿은 꺼져 있다 — 복제본만 켠다.
                view.gameObject.SetActive(true);

                // ⚠ 색은 tint 다 — 같은 모양을 색마다 굽지 않는다.
                view.color = _ghosts.Ghosts[i].Color;
                _ghostViews.Add(view);
            }
        }

        private void HandleGhostsMoved()
        {
            for (int i = 0; i < _ghostViews.Count && i < _ghosts.Ghosts.Count; i++)
            {
                GhostManager.Ghost ghost = _ghosts.Ghosts[i];

                // ★ 원본 이상 A5 — 유령은 «반올림 좌표»로 그려진다.
                //   이동은 연속인데 그림은 칸 단위로 튄다. 팩맨은 연속이다.
                _ghostViews[i].transform.localPosition =
                    ToWorld(Mathf.Round(ghost.X), Mathf.Round(ghost.Y));
            }
        }
    }
}
