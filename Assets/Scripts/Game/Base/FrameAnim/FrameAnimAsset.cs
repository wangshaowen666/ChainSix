/*--------------------------------------------------------------
 * File: FrameAnimAsset.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/08/25 16:40:00
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 序列帧动画配置（ScriptableObject）：
/// 一个资源包含多条动画（如 walk / dead），每条为 Sprite 列表 + 总时长
/// 使用方式：Assets/Create/Game/FrameAnimAsset 创建；同资源内 Sprite 建议打进同一图集，
/// 换帧不破坏 SpriteRenderer 合批
/// </summary>
[CreateAssetMenu(fileName = "FrameAnimAsset", menuName = "Game/FrameAnimAsset")]
public class FrameAnimAsset : ScriptableObject
{
    /// <summary>
    /// 单条动画数据
    /// </summary>
    [Serializable]
    public class Clip
    {
        [Tooltip("动画名（Play 时按此查找）")]
        public string clipName;

        [Tooltip("帧序列（建议同一图集）")]
        public List<Sprite> sprites;

        [Tooltip("动画总时长（秒），帧间隔 = 总时长 / 帧数")]
        public float duration;

        [Tooltip("是否循环")]
        public bool loop;
    }

    [Tooltip("动画列表")]
    public List<Clip> clips = new();

    /// <summary>
    /// 按动画名查找（线性查找，仅在 Play 切换动画时调用，不在逐帧路径上）
    /// </summary>
    public Clip GetClip(string clipName)
    {
        if (string.IsNullOrEmpty(clipName) || clips == null)
        {
            return null;
        }

        for (int i = 0; i < clips.Count; i++)
        {
            if (clips[i].clipName == clipName)
            {
                return clips[i];
            }
        }

        return null;
    }
}
