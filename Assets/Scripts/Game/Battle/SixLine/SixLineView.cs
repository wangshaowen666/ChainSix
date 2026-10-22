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
/// 六连珠（表现层）：
/// - 组装 SixLineLogic + SlLocalDriver（本地固定步长驱动）+ SlKeyboardInput（P0 移动/旋转/加速，触屏按键 1-6 接入）
/// - 蜂窝错行棋盘（偶行 10 格/奇行 9 格）+ 圆形棋子池 + 三角下落组（按旋转状态渲染 ▲/▽，
///   旋转时三球保色走位到新姿态 = 旋转过程），横屏双棋盘左右并排
/// - 动画层（VS 双缓冲范式）：逻辑帧对账做来源匹配——新占用格从正上方/斜上方刚空出的格认领视觉圈，
///   落定组三颗从下落组手中移交；渲染帧所有视觉圈向目标位匀速趋近，下落/滚落/沉降全程平滑无瞬移
/// - 表现层读逻辑层数据只允许 Fix.AsFloat
///
/// 文件拆分（partial，按职责）：
/// - SixLineView.cs        本文件：布局常量、组装（Init/Dispose）、帧回调（OnFrame/OnRenderFrame）
/// - SixLineView.Sync.cs   逻辑帧对账（下落组移交 + 棋盘三趟认领 + 球理想位）
/// - SixLineView.Anim.cs   视觉圈/棋盘表现数据结构 + 渲染帧插值
/// - SixLineView.Build.cs  灰盒图元构建（底板/危险区/圆盘网格/调色板）
/// </summary>
public partial class SixLineView : BattleView
{
    // ---- 灰盒布局（横屏 1920×1080：正交 size 10 => 视野约 35.6×20 格；双方等大棋盘左右并排）----
    private const float CellSize = 1.4f; // 格尺寸（= 圆直径）：12 行棋盘高 16.8 格，上下留 HUD 空间
    private const float BoardWidth = 10f;
    // 棋盘左下角（世界坐标）：P0 左、P1 右，中间留 2 格间隔，两侧对称余量
    private static readonly Vector2 P0Origin = new(-15f, -8.4f);
    private static readonly Vector2 P1Origin = new(1f, -8.4f);

    // 动画速度（世界单位/秒）
    private const float TriFollowSpeed = 30f; // 下落组跟随逻辑位（掩盖 20Hz 步进）
    private const float SettleSpeed = 16f;    // 沉降/滚落移动速度
    private const float RotAnimSec = 0.1f;    // 旋转过渡时长（秒）：三球按统一时长转位，保留颜色身份

    private SixLineLogic _logic;
    private SlLocalDriver _driver;
    private SlKeyboardInput _keyboardInput;
    private long _seed;
    private int _curFrame; // 当前逻辑帧号（视觉圈认领去重戳）

    private Transform _root;
    private BoardView _p0;
    private BoardView _p1;

    public override void Init()
    {
        // 阶段 1 本地对局：本地生成种子（同种子可重放验证 StateHash）；阶段 3 改读 Room.Seed（StartGamePush 下发）
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

    /// <summary>逻辑帧推进：棋盘/三角组对账（确定各视觉圈目标位），周期哈希日志供同种子重放对比</summary>
    private void OnFrame(int frame)
    {
        _curFrame = frame;
        SyncPlayer(_logic.Players[0], _p0);
        SyncPlayer(_logic.Players[1], _p1);

        if (frame % 50 == 0)
            Log.Info("[六连珠][校验] 帧:", frame, "哈希:", _logic.StateHash());
    }

    /// <summary>渲染帧推进：所有视觉圈向目标位匀速趋近（下落/滚落/沉降平滑呈现）</summary>
    private void OnRenderFrame(float alpha, float dt)
    {
        UpdateAnims(_p0, dt);
        UpdateAnims(_p1, dt);
    }

    /// <summary>本地对局种子（联机阶段改为服务器下发，本方法删除）</summary>
    private static long GenerateSeed()
    {
        return DateTime.UtcNow.Ticks;
    }
}
