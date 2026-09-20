/*--------------------------------------------------------------
 * File: FrameAnimView.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/08/25 16:40:00
 *--------------------------------------------------------------
 */

using System;
using UnityEngine;

/// <summary>
/// 序列帧动画视图（prefab 工作流适配器）：
/// 挂在带 SpriteRenderer 的节点上，OnEnable 创建播放器 / OnDisable 归还，
/// 天然适配对象池复用（重新激活自动重播默认动画）
/// 代码驱动路径（如战斗视图对账）建议直接用 GameMgr.FrameAnim.CreatePlayer，不必挂本组件
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class FrameAnimView : MonoBehaviour
{
    [SerializeField]
    [Tooltip("动画配置")]
    private FrameAnimAsset _asset;

    [SerializeField]
    [Tooltip("激活时默认播放的动画名（空则不自动播放）")]
    private string _defaultClip;

    [SerializeField]
    [Tooltip("OnEnable 时自动播放默认动画")]
    private bool _playOnEnable = true;

    private SpriteRenderer _spriteRenderer;
    private FrameAnimPlayer _player;

    /// <summary>是否正在播放</summary>
    public bool IsPlaying => _player != null && _player.IsPlaying;

    /// <summary>当前动画名（未播放为 null）</summary>
    public string CurrentClipName => _player != null ? _player.CurrentClipName : null;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (_asset == null)
        {
            return;
        }

        _player = GameMgr.FrameAnim.CreatePlayer(_spriteRenderer, _asset);
        if (_playOnEnable && !string.IsNullOrEmpty(_defaultClip))
        {
            _player.Play(_defaultClip);
        }
    }

    private void OnDisable()
    {
        ReleasePlayer();
    }

    private void OnDestroy()
    {
        ReleasePlayer();
    }

    /// <summary>
    /// 播放动画（从头播）
    /// </summary>
    /// <param name="clipName">动画名</param>
    /// <param name="onFinish">非循环动画自然播完回调</param>
    public bool Play(string clipName, Action onFinish = null)
    {
        if (!EnsurePlayer())
        {
            return false;
        }

        return _player.Play(clipName, onFinish);
    }

    /// <summary>停止播放（保持当前帧）</summary>
    public void Stop()
    {
        _player?.Stop();
    }

    /// <summary>设置播放速度（1 为原速）</summary>
    public void SetSpeed(float speed)
    {
        _player?.SetSpeed(speed);
    }

    /// <summary>暂停（冻结在当前帧）</summary>
    public void Pause()
    {
        _player?.Pause();
    }

    /// <summary>恢复播放</summary>
    public void Resume()
    {
        _player?.Resume();
    }

    private bool EnsurePlayer()
    {
        if (_player != null)
        {
            return true;
        }

        if (_asset == null || _spriteRenderer == null)
        {
            Log.Warning("FrameAnimView 缺少动画配置或 SpriteRenderer:", gameObject.name);
            return false;
        }

        _player = GameMgr.FrameAnim.CreatePlayer(_spriteRenderer, _asset);
        return true;
    }

    private void ReleasePlayer()
    {
        if (_player == null)
        {
            return;
        }

        GameMgr.FrameAnim.ReleasePlayer(_player);
        _player = null;
    }
}
