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
/// 六连珠单机驱动器：注册 CoreMgr.Update，按墙钟累积每满 LogicFrameMs 推一逻辑帧。
/// 渲染帧轮询输入源累积到 pending，逻辑帧边界整体喂给 Tick。
/// 阶段 3 好友对战换成 FrameSyncMgr，Logic 与表现层零改动。
/// </summary>
public class SlLocalDriver : IUpdateable
{
    private readonly SixLineLogic _logic;
    private readonly List<ISlInputSource> _sources = new List<ISlInputSource>();
    private readonly List<SlInput> _pending = new List<SlInput>(); // 边缘事件不因逻辑帧边界丢失
    private double _lastRealTime = -1;
    private float _frameTimer;
    private int _nextFrameId = 1;

    public bool Paused { get; private set; }
    public int CurFrameId { get; private set; }

    /// <summary>Tick 后携带帧号触发，表现层在此做视图对账</summary>
    public event Action<int> OnFrame;

    /// <summary>每渲染帧触发（alpha = 帧内进度 0~1）</summary>
    public event Action<float, float> OnRenderFrame;

    public SlLocalDriver(SixLineLogic logic)
    {
        _logic = logic;
        CoreMgr.Update.RegisterUpdate(this);
    }

    public void RegisterInput(ISlInputSource source)
    {
        _sources.Add(source);
    }

    public void MyUpdate(float deltaTime, float realDeltaTime)
    {
        if (Paused)
        {
            // 暂停：停步进不累积预算，alpha 冻结
            OnRenderFrame?.Invoke(Mathf.Clamp01(_frameTimer / SixLineLogic.LogicFrameMs), 0f);
            return;
        }

        for (int i = 0; i < _sources.Count; i++)
            _sources[i].Collect(_pending);

        // 用真实流逝时间累积（编辑器失焦/长卡顿也能步进）
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
            _pending.Clear(); // 追帧时后续帧为空帧
            OnFrame?.Invoke(CurFrameId);
        }

        OnRenderFrame?.Invoke(Mathf.Clamp01(_frameTimer / SixLineLogic.LogicFrameMs), deltaSeconds);
    }

    public void Pause()
    {
        Paused = true;
    }

    public void Resume()
    {
        Paused = false;
        _lastRealTime = -1; // 丢弃暂停期间流逝的墙钟，不追帧
    }

    public void Dispose()
    {
        CoreMgr.Update.UnRegisterUpdate(this);
    }
}

/// <summary>
/// 六连珠输入源：渲染帧被驱动器轮询。逻辑层禁止读 Input.*，
/// 阶段 3 联机侧不走此接口（FrameData.Inputs 直接转 SlInput）。
/// </summary>
public interface ISlInputSource
{
    void Collect(List<SlInput> pending);
}
