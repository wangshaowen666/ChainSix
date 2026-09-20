/*--------------------------------------------------------------
 * File: FrameAnimPlayer.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/08/25 16:40:00
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 序列帧播放器（纯 C#，非 MonoBehaviour，走项目统一对象池 ClassPool）：
/// - 核心原理：动画 = 按时间查表换 Sprite 引用，每帧成本仅一次加法与比较，
///   只有跨帧边界那一拍才触碰 SpriteRenderer
/// - 由 FrameAnimMgr 统一驱动与池化管理，禁止直接 new，走 GameMgr.FrameAnim.CreatePlayer
/// - 表现层组件：允许 float / UnityEngine API，不受逻辑层确定性纪律约束
/// </summary>
public class FrameAnimPlayer : IResetable
{
    private SpriteRenderer _target;
    private FrameAnimAsset _asset;
    private FrameAnimAsset.Clip _clip;
    private List<Sprite> _sprites;
    private Action _onFinish; // 非循环动画自然播完回调（Stop / 重新 Play 不触发）
    private float _frameInterval; // 帧间隔（秒），<= 0 表示静态帧不推进
    private float _timeAcc; // 时间累加器
    private float _speed = 1f;
    private int _frameIdx;
    private bool _isPlaying;
    private bool _isPaused;
    private bool _isReleased; // 已回收标记，延迟到下一帧从驱动列表移除

    /// <summary>是否正在播放（非循环动画播完为 false）</summary>
    public bool IsPlaying => _isPlaying;

    /// <summary>是否暂停（实例级，配合 FrameAnimMgr 全局暂停使用）</summary>
    public bool IsPaused => _isPaused;

    /// <summary>当前动画名（未播放为 null）</summary>
    public string CurrentClipName => _clip != null ? _clip.clipName : null;

    /// <summary>当前播放速度（1 为原速）</summary>
    public float Speed => _speed;

    internal bool IsReleased => _isReleased;

    /// <summary>
    /// 播放动画（从头播，重置进度），找不到动画返回 false
    /// </summary>
    /// <param name="clipName">动画名</param>
    /// <param name="onFinish">非循环动画自然播完回调（可在回调内安全调 ReleasePlayer）</param>
    public bool Play(string clipName, Action onFinish = null)
    {
        if (_isReleased || _asset == null)
        {
            return false;
        }

        var clip = _asset.GetClip(clipName);
        if (clip == null || clip.sprites == null || clip.sprites.Count == 0)
        {
            Log.Warning("FrameAnimPlayer 播放失败, 找不到动画:", clipName, " 资源:", _asset.name);
            return false;
        }

        _clip = clip;
        _sprites = clip.sprites;
        _onFinish = onFinish;
        _frameIdx = 0;
        _timeAcc = 0f;
        _isPlaying = true;
        CalcInterval();
        ApplyCurrentFrame();
        return true;
    }

    /// <summary>
    /// 停止播放（保持当前帧，不触发 onFinish）
    /// </summary>
    public void Stop()
    {
        _isPlaying = false;
        _onFinish = null;
    }

    /// <summary>
    /// 设置播放速度（保持当前进度）
    /// </summary>
    public void SetSpeed(float speed)
    {
        if (speed <= 0f)
        {
            return;
        }

        _speed = speed;
        if (_clip != null)
        {
            CalcInterval();
        }
    }

    /// <summary>暂停（动画冻结在当前帧）</summary>
    public void Pause()
    {
        _isPaused = true;
    }

    /// <summary>恢复播放</summary>
    public void Resume()
    {
        _isPaused = false;
    }

    /// <summary>
    /// 推进动画（仅 FrameAnimMgr 调用）：
    /// 时间尖峰时连续跨帧直达正确帧（不慢放追赶）
    /// </summary>
    internal void Advance(float delta)
    {
        if (_isReleased || !_isPlaying || _isPaused || _frameInterval <= 0f)
        {
            return;
        }

        // 视图已销毁但未归还播放器：停止播放防止空引用
        if (_target == null)
        {
            _isPlaying = false;
            _onFinish = null;
            return;
        }

        _timeAcc += delta;
        while (_timeAcc >= _frameInterval && _isPlaying)
        {
            _timeAcc -= _frameInterval;
            _frameIdx++;

            if (_frameIdx >= _sprites.Count)
            {
                if (_clip.loop)
                {
                    _frameIdx = 0;
                }
                else
                {
                    // 非循环播完：停在最后一帧，由持有者决定回收或切换
                    _frameIdx = _sprites.Count - 1;
                    _isPlaying = false;
                    ApplyCurrentFrame();
                    var callback = _onFinish;
                    _onFinish = null;
                    callback?.Invoke();
                    return;
                }
            }

            ApplyCurrentFrame();
        }
    }

    /// <summary>
    /// 池化初始化（仅 FrameAnimMgr 调用）
    /// </summary>
    internal void Init(SpriteRenderer target, FrameAnimAsset asset)
    {
        _target = target;
        _asset = asset;
        _isReleased = false;
    }

    /// <summary>
    /// 回收标记（仅 FrameAnimMgr 调用）：
    /// 只做逻辑释放，延迟到下一帧由 Mgr flush 统一归还对象池，届时触发 Reset 完整清理
    /// </summary>
    internal void Release()
    {
        _isReleased = true;
        _isPlaying = false;
        _onFinish = null;
    }

    /// <summary>
    /// 完整清理（IResetable，对象池回收时调用）
    /// </summary>
    public void Reset()
    {
        _isReleased = true;
        _isPlaying = false;
        _isPaused = false;
        _onFinish = null;
        _clip = null;
        _sprites = null;
        _target = null;
        _asset = null;
        _speed = 1f;
        _frameInterval = -1f;
        _timeAcc = 0f;
        _frameIdx = 0;
    }

    /// <summary>
    /// 帧间隔 = 总时长 / 帧数 / 速度；单帧或无效配置视为静态帧
    /// </summary>
    private void CalcInterval()
    {
        if (_sprites.Count <= 1 || _clip.duration <= 0f || _speed <= 0f)
        {
            _frameInterval = -1f;
        }
        else
        {
            _frameInterval = _clip.duration / _sprites.Count / _speed;
        }
    }

    private void ApplyCurrentFrame()
    {
        _target.sprite = _sprites[_frameIdx];
    }
}
