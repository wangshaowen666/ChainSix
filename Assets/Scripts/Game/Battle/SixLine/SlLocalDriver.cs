/*--------------------------------------------------------------
 * File: SlLocalDriver.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/09/22 10:00:00
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 六连珠单机驱动器（本地固定步长，对齐 VampireSurvivor 的 LocalDriver 模式）：
/// - 注册 CoreMgr.Update（IUpdateable），墙钟时间累积，每满 SixLineLogic.LogicFrameMs(50ms) 推进一逻辑帧，帧号从 1 递增
/// - 渲染帧先轮询输入源（边缘/持续操作累积进 pending），逻辑帧边界把 pending 整体喂给 SixLineLogic.Tick
/// - 暂停：停步进不累积墙钟，恢复不追帧（丢弃暂停期间流逝的墙钟）
/// - 阶段 3 好友对战换成 FrameSyncMgr 驱动，SixLineLogic/表现层零改动
/// </summary>
public class SlLocalDriver : IUpdateable
{
    private readonly SixLineLogic _logic;
    private readonly List<ISlInputSource> _sources = new List<ISlInputSource>();
    private readonly List<SlInput> _pending = new List<SlInput>(); // 渲染帧轮询累积（点列等边缘事件不因逻辑帧边界丢失）
    private double _lastRealTime = -1; // 上次采样的墙钟时间（-1 = 未初始化）
    private float _frameTimer;         // 墙钟累积的毫秒预算
    private int _nextFrameId = 1;

    /// <summary>暂停状态（true = 逻辑步进停止，TimeScale 不受影响）</summary>
    public bool Paused { get; private set; }

    /// <summary>当前已推进的逻辑帧号（未开始为 0）</summary>
    public int CurFrameId { get; private set; }

    /// <summary>每推一逻辑帧触发一次（Tick 后携带帧号），表现层在此做视图对账</summary>
    public event Action<int> OnFrame;

    /// <summary>每渲染帧触发一次（alpha = 帧内推进进度 0~1），表现层据此做帧间插值（1-6 接入）</summary>
    public event Action<float, float> OnRenderFrame;

    public SlLocalDriver(SixLineLogic logic)
    {
        _logic = logic;
        CoreMgr.Update.RegisterUpdate(this);
    }

    /// <summary>注册输入源（渲染帧轮询 Collect，可多个）</summary>
    public void RegisterInput(ISlInputSource source)
    {
        _sources.Add(source);
    }

    public void MyUpdate(float deltaTime, float realDeltaTime)
    {
        if (Paused)
        {
            // 暂停：停步进（不消费帧/不累积预算），alpha 冻结，插值输出恒定
            OnRenderFrame?.Invoke(Mathf.Clamp01(_frameTimer / SixLineLogic.LogicFrameMs), 0f);
            return;
        }

        // 渲染帧轮询输入源：点列等边缘操作先累积，等下一个逻辑帧边界统一消费
        for (int i = 0; i < _sources.Count; i++)
            _sources[i].Collect(_pending);

        // 墙钟累积（编辑器失焦/长卡顿仍按真实流逝时间步进）
        var now = Time.realtimeSinceStartup;
        if (_lastRealTime < 0) _lastRealTime = now;
        _frameTimer += (float)((now - _lastRealTime) * 1000.0);
        var deltaSeconds = (float)(now - _lastRealTime);
        _lastRealTime = now;

        while (_frameTimer >= SixLineLogic.LogicFrameMs)
        {
            _frameTimer -= SixLineLogic.LogicFrameMs;
            CurFrameId = _nextFrameId++;
            _logic.Tick(CurFrameId, _pending);
            _pending.Clear(); // 帧内输入已消费（追帧时后续帧为空帧）
            OnFrame?.Invoke(CurFrameId);
        }

        OnRenderFrame?.Invoke(Mathf.Clamp01(_frameTimer / SixLineLogic.LogicFrameMs), deltaSeconds);
    }

    /// <summary>暂停逻辑步进（直接停步进，不动 TimeScale）</summary>
    public void Pause()
    {
        Paused = true;
    }

    /// <summary>恢复逻辑步进，不追帧</summary>
    public void Resume()
    {
        Paused = false;
        _lastRealTime = -1;
    }

    /// <summary>注销 Update 订阅（战斗退出时由 SixLineView.Dispose 调用）</summary>
    public void Dispose()
    {
        CoreMgr.Update.UnRegisterUpdate(this);
    }
}

/// <summary>
/// 六连珠输入源：渲染帧被驱动器轮询，把操作追加进 pending。
/// 逻辑层禁止读 Input.*，输入是系统边界不参与确定性演算；
/// 阶段 3 联机侧不走此接口（FrameData.Inputs 直接转换 SlInput）
/// </summary>
public interface ISlInputSource
{
    void Collect(List<SlInput> pending);
}
