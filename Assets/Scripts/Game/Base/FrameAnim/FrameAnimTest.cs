/*--------------------------------------------------------------
 * File: FrameAnimTest.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/08/25 16:50:00
 *--------------------------------------------------------------
 */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 帧动画测试脚本（零美术资源）：
/// 程序化生成 8 帧序列帧（颜色渐变 + 方向点），运行时构造成 FrameAnimAsset，
/// 走 GameMgr.FrameAnim.CreatePlayer 真实使用路径播放。
/// IMGUI 面板控制 Play/Stop/Pause/Resume/变速/全局暂停，演示 die 播完自动回收。
/// 用法：需从游戏正常流程启动（保证 GameMgr 已初始化、FrameAnimMgr 已注册驱动），
///       新建空物体挂本脚本，Play 即可看到旋转渐变的方块动画。
/// </summary>
public class FrameAnimTest : MonoBehaviour
{
    private const int TexSize = 64;

    /// <summary>IMGUI 字体大小（高分屏 1440*3200 下需放大）</summary>
    private const int GuiFontSize = 72;

    /// <summary>IMGUI 按钮高度</summary>
    private const int GuiControlHeight = 104;

    private FrameAnimAsset _asset;
    private SpriteRenderer _renderer;
    private FrameAnimPlayer _player;
    private float _speed = 1f;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        if (_renderer == null)
        {
            _renderer = gameObject.AddComponent<SpriteRenderer>();
        }

        _asset = BuildTestAsset();
        _player = GameMgr.FrameAnim.CreatePlayer(_renderer, _asset);
        _player.Play("walk");
    }

    private void OnDestroy()
    {
        if (_player != null)
        {
            GameMgr.FrameAnim.ReleasePlayer(_player);
        }
    }

    private void OnGUI()
    {
        // 放大字体与控件尺寸，适配 1440*3200 高分屏
        var skin = GUI.skin;
        skin.box.fontSize = GuiFontSize;
        skin.button.fontSize = GuiFontSize;
        skin.label.fontSize = GuiFontSize;
        skin.button.fixedHeight = GuiControlHeight;

        var rect = new Rect(40, 40, 920, 1000);
        GUI.Box(rect, "");
        GUILayout.BeginArea(rect);

        GUILayout.Label($"全局暂停: {GameMgr.FrameAnim.IsPaused} ");
        GUILayout.Label($"Playing: {(_player != null && _player.IsPlaying)}  Clip: {(_player != null ? _player.CurrentClipName : "null")}");
        GUILayout.Space(8);

        if (GUILayout.Button("Play walk (循环)"))
        {
            PlayOrCreate("walk");
        }
        if (GUILayout.Button("Play die (非循环, 播完自动回收)"))
        {
            PlayOrCreate("die");
        }
        if (GUILayout.Button("Stop"))
        {
            _player?.Stop();
        }
        if (GUILayout.Button("Pause (实例暂停)"))
        {
            _player?.Pause();
        }
        if (GUILayout.Button("Resume"))
        {
            _player?.Resume();
        }

        GUILayout.Space(8);
        GUILayout.Label($"Speed: {_speed:F1}");
        var newSpeed = GUILayout.HorizontalSlider(_speed, 0.1f, 3f, GUILayout.Height(88));
        if (Mathf.Abs(newSpeed - _speed) > 0.001f)
        {
            _speed = newSpeed;
            _player?.SetSpeed(_speed);
        }

        if (GUILayout.Button("全局 Pause / Resume"))
        {
            if (GameMgr.FrameAnim.IsPaused)
            {
                GameMgr.FrameAnim.Resume();
            }
            else
            {
                GameMgr.FrameAnim.Pause();
            }
        }

        GUILayout.EndArea();
    }

    private void PlayOrCreate(string clipName)
    {
        // 演示播放器回收后重建：die 播完已自动回收，再播需从池重建（首次 new，之后复用池）
        if (_player == null || _player.IsReleased)
        {
            _player = GameMgr.FrameAnim.CreatePlayer(_renderer, _asset);
        }

        _player.Play(clipName, () =>
        {
            // 演示 onFinish 回调内安全回收（Mgr 延迟一帧移除）
            Log.Info("FrameAnimTest 动画播完:", clipName, " 自动回收");
            GameMgr.FrameAnim.ReleasePlayer(_player);
        });
    }

    /// <summary>
    /// 构造运行时动画资产：walk(8帧循环) + die(6帧非循环，逐渐缩小)
    /// </summary>
    private FrameAnimAsset BuildTestAsset()
    {
        var asset = ScriptableObject.CreateInstance<FrameAnimAsset>();
        asset.clips = new List<FrameAnimAsset.Clip>
        {
            new()
            {
                clipName = "walk",
                sprites = GenerateSprites(8, 1f),
                duration = 0.8f,
                loop = true,
            },
            new()
            {
                clipName = "die",
                sprites = GenerateSprites(6, 0.5f),
                duration = 0.6f,
                loop = false,
            },
        };
        return asset;
    }

    private List<Sprite> GenerateSprites(int count, float endScale)
    {
        var sprites = new List<Sprite>(count);
        for (int i = 0; i < count; i++)
        {
            var t = count <= 1 ? 0f : i / (float)(count - 1);
            sprites.Add(CreateFrameSprite(i, t, endScale));
        }

        return sprites;
    }

    /// <summary>
    /// 生成一帧：色相随帧变化 + 一个随帧旋转的方向白点 + 大小随 t 缩放
    /// </summary>
    private Sprite CreateFrameSprite(int index, float t, float endScale)
    {
        var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
        tex.hideFlags = HideFlags.HideAndDontSave;

        var pixels = new Color[TexSize * TexSize];
        var center = (TexSize - 1) * 0.5f;
        var radius = TexSize * 0.32f * Mathf.Lerp(1f, endScale, t);
        var color = Color.HSVToRGB(index * 0.125f, 0.8f, 1f);
        var angle = index * 45f * Mathf.Deg2Rad;
        var dirX = center + Mathf.Cos(angle) * TexSize * 0.18f;
        var dirY = center + Mathf.Sin(angle) * TexSize * 0.18f;

        for (int y = 0; y < TexSize; y++)
        {
            for (int x = 0; x < TexSize; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var distSqr = dx * dx + dy * dy;
                if (distSqr <= radius * radius)
                {
                    var pdx = x - dirX;
                    var pdy = y - dirY;
                    pixels[y * TexSize + x] = pdx * pdx + pdy * pdy <= 16f ? Color.white : color;
                }
                else
                {
                    pixels[y * TexSize + x] = new Color(0f, 0f, 0f, 0f);
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize), new Vector2(0.5f, 0.5f), 100f);
    }
}
