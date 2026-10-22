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
/// 动画走双缓冲对账（逻辑帧定目标位，渲染帧趋近），表现层读逻辑数据只允许 Fix.AsFloat。
/// partial 拆分：Sync = 帧对账；Anim = 视觉圈与插值；Build = 灰盒图元。
/// </summary>
public partial class SixLineView : BattleView
{
    // ---- 灰盒布局（横屏 1920×1080）----
    private const float CellSize = 1.4f; // 格尺寸 = 圆直径
    private const float BoardWidth = 10f;
    private static readonly Vector2 P0Origin = new(-15f, -8.4f); // 棋盘左下角
    private static readonly Vector2 P1Origin = new(1f, -8.4f);

    private const float TriFollowSpeed = 30f; // 下落组跟随速度，掩盖 20Hz 步进
    private const float SettleSpeed = 16f;
    private const float RotAnimSec = 0.1f;    // 旋转过渡时长

    private SixLineLogic _logic;
    private SlLocalDriver _driver;
    private SlKeyboardInput _keyboardInput;
    private long _seed;
    private int _curFrame; // 视觉圈认领去重戳

    private Transform _root;
    private BoardView _p0;
    private BoardView _p1;

    public override void Init()
    {
        // 阶段 1 本地生成种子；阶段 3 改读 Room.Seed
        _seed = GenerateSeed();
        _logic = new SixLineLogic(_seed);
        _driver = new SlLocalDriver(_logic);
        _keyboardInput = new SlKeyboardInput(0); // TODO(1-7) P1 键盘源 / 触屏按键

        _root = new GameObject("SixLineRoot").transform;
        _p0 = BuildBoard("P0Board", P0Origin, CellSize);
        _p1 = BuildBoard("P1Board", P1Origin, CellSize);

        _driver.RegisterInput(_keyboardInput);
        _driver.OnFrame += OnFrame;
        _driver.OnRenderFrame += OnRenderFrame;

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
        _keyboardInput = null;

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
        _curFrame = frame;
        SyncPlayer(_logic.Players[0], _p0);
        SyncPlayer(_logic.Players[1], _p1);

        if (frame % 50 == 0)
            Log.Info("[六连珠][校验] 帧:", frame, "哈希:", _logic.StateHash());
    }

    private void OnRenderFrame(float alpha, float dt)
    {
        UpdateAnims(_p0, dt);
        UpdateAnims(_p1, dt);
    }

    private static long GenerateSeed()
    {
        return DateTime.UtcNow.Ticks;
    }
}
