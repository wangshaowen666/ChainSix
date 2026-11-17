using System;
using System.Collections.Generic;

// 仅为独立测试提供可控墙钟与键盘，不进入 Unity 的 Assets 编译。
namespace UnityEngine
{
    public static class Application { public static bool isFocused = true; }
    public static class Time { public static double realtimeSinceStartupAsDouble; }
    public static class Mathf { public static float Clamp01(float value) => Math.Clamp(value, 0, 1); }
    public enum KeyCode { A, D, E, Q, O, U, LeftArrow, RightArrow, DownArrow, Space }
    public static class Input
    {
        public static readonly HashSet<KeyCode> Held = new HashSet<KeyCode>();
        public static readonly HashSet<KeyCode> Down = new HashSet<KeyCode>();
        public static bool GetKey(KeyCode key) => Held.Contains(key);
        public static bool GetKeyDown(KeyCode key) => Down.Contains(key);
    }
}
public interface IUpdateable { void MyUpdate(float deltaTime, float realDeltaTime); }
public static class CoreMgr { public static readonly TestUpdateMgr Update = new TestUpdateMgr(); }
public sealed class TestUpdateMgr
{
    public readonly HashSet<IUpdateable> Registered = new HashSet<IUpdateable>();
    public void RegisterUpdate(IUpdateable value) => Registered.Add(value);
    public void UnRegisterUpdate(IUpdateable value) => Registered.Remove(value);
}
