/*--------------------------------------------------------------
 * File: SixLineView.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/09/21 11:00:00
 *--------------------------------------------------------------
 */

using System;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 六连珠表现层：组装 Logic + SlLocalDriver + SlKeyboardInput，双棋盘左右并排。
/// 动画按稳定棋子 ID 与逻辑轨迹分阶段播放，表现层读逻辑数据只允许 Fix.AsFloat。
/// partial 拆分：Sync = 帧对账；Anim = 视觉圈与插值；Build = 灰盒图元。
/// </summary>
public partial class SixLineView : BattleView
{
    // ---- 灰盒布局（横屏 1920×1080）----
    private const float LogicStepSeconds = SixLineLogic.LogicFrameMs * .001f;
    private const float CellSize = 1.4f; // 格尺寸 = 圆直径
    private static float RowPitch => SixLineLogic.RowPitch.AsFloat;
    private float BoardHeightCells => (_logic.Rows - 1) * RowPitch + 1f;

    private static float GridY(float y) => (y - .5f) * RowPitch + .5f;

    private SixLineLogic _logic;
    private SlLocalDriver _driver;
    private SlTouchInput[] _touchInputs;
    private SixLineHud _hud;
    private Camera _camera;
    private Vector3 _cameraPosition;
    private float _cameraSize;
    private bool _cameraOrthographic;
    private long _seed;
    private bool _resultRevealed;

    private Transform _root;
    private BoardView _p0;
    private BoardView _p1;

    public override void Init()
    {
        // 阶段 1 本地生成种子；阶段 3 改读 Room.Seed
        _seed = GenerateSeed();
        _logic = new SixLineLogic(_seed, GameMgr.DataTable.TbSixLineRule.Get(1));
        _driver = new SlLocalDriver(_logic);
        _touchInputs = new[] { new SlTouchInput(0), new SlTouchInput(1) };

        _root = new GameObject("SixLineRoot").transform;
        var w = _logic.Columns * CellSize;
        var h = BoardHeightCells * CellSize;
        _p0 = BuildBoard("P0Board", new Vector2(-w - CellSize, -h * .5f), CellSize, 0);
        _p1 = BuildBoard("P1Board", new Vector2(CellSize, -h * .5f), CellSize, 1);
        _camera = GameCamera.MainCamera;
        if (_camera != null)
        {
            _cameraPosition = _camera.transform.position;
            _cameraSize = _camera.orthographicSize;
            _cameraOrthographic = _camera.orthographic;
            _camera.orthographic = true;
            _camera.transform.position = new Vector3(0, 0, _cameraPosition.z);
            FitCamera();
        }
        for (int i = 0; i < 2; i++)
        {
            _driver.RegisterInput(new SlKeyboardInput(i));
            _driver.RegisterInput(_touchInputs[i]);
        }
        _driver.OnFrame += OnFrame;
        _driver.OnRenderFrame += OnRenderFrame;

        SyncPlayer(_logic.Players[0], _p0);
        SyncPlayer(_logic.Players[1], _p1);
        if (GameMgr.UI.HasPanel(cfg.DPnlId.BattlePanel) is BattlePanel panel)
            AttachHud(panel);
        Log.Info("[六连珠] 进入战斗, 种子:", _seed);
    }

    public override void Dispose()
    {
        if (_driver != null)
        {
            _driver.OnFrame -= OnFrame;
            _driver.OnRenderFrame -= OnRenderFrame;
            _driver.Dispose();
            _driver = null;
        }
        _hud?.Dispose();
        _hud = null;
        _touchInputs = null;
        if (_camera != null)
        {
            _camera.transform.position = _cameraPosition;
            _camera.orthographicSize = _cameraSize;
            _camera.orthographic = _cameraOrthographic;
            _camera = null;
        }

        if (_root != null)
        {
            _root.SetParent(null);
            Object.Destroy(_root.gameObject);
            _root = null;
            _p0 = null;
            _p1 = null;
        }
        _logic = null;
    }

    private void OnFrame(int frame)
    {
        SyncPlayer(_logic.Players[0], _p0);
        SyncPlayer(_logic.Players[1], _p1);

        _hud?.Refresh(_logic, _seed, !PresentationBusy());
        if (_logic.GameOver)
            Log.Info("[六连珠] 对局结束, 获胜方:", _logic.WinnerIndex, "帧:", frame);
        if (frame % 50 == 0)
            Log.Info("[六连珠][校验] 帧:", frame, "哈希:", _logic.StateHash());
    }

    private void OnRenderFrame(float alpha, float dt)
    {
        FitCamera();
        UpdateAnims(_p0, alpha, dt);
        UpdateAnims(_p1, alpha, dt);
        _hud?.UpdateEffects(dt);
        if (_logic.GameOver && !_resultRevealed && !PresentationBusy())
        {
            _resultRevealed = true;
            _hud?.Refresh(_logic, _seed);
        }
    }

    private bool PresentationBusy() => _p0.Playing || _p1.Playing;

    public void AttachHud(BattlePanel panel)
    {
        if (_logic == null)
            return;
        _hud?.Dispose();
        _hud = panel.CreateSixLineHud(_touchInputs,
            () => GameMgr.Battle.EnterBattle(BattleMode.SixLine),
            () => GameMgr.Event.Send(GameEvent.ProcedureExitBattle));
        _hud.Refresh(_logic, _seed, !PresentationBusy());
    }

    private void FitCamera()
    {
        if (_camera == null)
            return;
        // 为顶部预览与底部双人按钮留空，棋盘尺寸来自规则表。
        var height = (BoardHeightCells + 2f * RowPitch) * CellSize / .60f;
        var width = (_logic.Columns * 2f + 3f) * CellSize / .96f;
        _camera.orthographicSize = Mathf.Max(height * .5f, width * .5f / Mathf.Max(.1f, _camera.aspect));
        var viewHeight = _camera.orthographicSize * 2f;
        var quarterWidth = _camera.orthographicSize * _camera.aspect * .5f;
        var boardWidth = _logic.Columns * CellSize;
        _p0.Root.position = new Vector3(-quarterWidth - boardWidth * .5f, -viewHeight * .32f, 0);
        _p1.Root.position = new Vector3(quarterWidth - boardWidth * .5f, -viewHeight * .32f, 0);
    }

    private static long GenerateSeed()
    {
        return DateTime.UtcNow.Ticks;
    }
}
