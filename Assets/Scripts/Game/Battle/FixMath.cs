/*--------------------------------------------------------------
 * File: FixMath.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/08/18 16:00:00
 *--------------------------------------------------------------
 */

using System;

/// <summary>
/// Q16.16 定点数（帧同步确定性数学）：
/// - 64 位整数存储：高 48 位整数部分 + 低 16 位小数部分
/// - 精度 1/65536 ≈ 0.000015，范围 ±1.4e14（140万亿）（业务常量直接写值：Fix.FromDouble(0.07)）
/// - 纯整数运算，跨平台（x86/ARM、Mono/IL2CPP）结果完全一致；
///   float 在不同平台/编译器下可能因舍入差异导致双端画面不一致，帧同步逻辑层禁用
/// - 乘法安全范围：两操作数绝对值之和不超过约 2^31（塔防数值远小于此）
/// </summary>
public readonly struct Fix : IEquatable<Fix>
{
    public const int FractionBits = 16;
    public const long OneRaw = 1L << FractionBits;

    public readonly long Raw;

    private Fix(long raw)
    {
        Raw = raw;
    }

    public static readonly Fix Zero = new Fix(0);
    public static readonly Fix Half = new Fix(OneRaw / 2);
    public static readonly Fix One = new Fix(OneRaw);

    /// <summary>从整数构造</summary>
    public static Fix FromInt(int v) => new Fix((long)v << FractionBits);

    /// <summary>
    /// 从十进制字面量构造（业务常量用，如 Fix.FromDouble(0.07)）。
    /// 仅限初始化常量场景：double 转换在 .NET 各平台语义一致（IEEE754），
    /// 结果确定；禁止在逐帧逻辑中用浮点动态构造。
    /// 内部四舍五入到最近定点值（误差 < 1/65536）
    /// </summary>
    public static Fix FromDouble(double v)
        => new Fix((long)(v * OneRaw + (v >= 0 ? 0.5 : -0.5)));

    /// <summary>从原始定点值构造（仅序列化/调试用）</summary>
    public static Fix FromRaw(long raw) => new Fix(raw);

    /// <summary>截断取整数部分</summary>
    public int Int => (int)(Raw >> FractionBits);

    /// <summary>转 float（仅表现层使用，逻辑层禁用）</summary>
    public float AsFloat => Raw / (float)OneRaw;

    public static Fix operator +(Fix a, Fix b) => new Fix(a.Raw + b.Raw);
    public static Fix operator -(Fix a, Fix b) => new Fix(a.Raw - b.Raw);
    public static Fix operator -(Fix a) => new Fix(-a.Raw);

    public static Fix operator *(Fix a, Fix b) => new Fix((a.Raw * b.Raw) >> FractionBits);

    public static Fix operator /(Fix a, Fix b) => new Fix((a.Raw << FractionBits) / b.Raw);

    public static bool operator ==(Fix a, Fix b) => a.Raw == b.Raw;
    public static bool operator !=(Fix a, Fix b) => a.Raw != b.Raw;
    public static bool operator <(Fix a, Fix b) => a.Raw < b.Raw;
    public static bool operator >(Fix a, Fix b) => a.Raw > b.Raw;
    public static bool operator <=(Fix a, Fix b) => a.Raw <= b.Raw;
    public static bool operator >=(Fix a, Fix b) => a.Raw >= b.Raw;

    public bool Equals(Fix other) => Raw == other.Raw;
    public override bool Equals(object obj) => obj is Fix f && Raw == f.Raw;
    public override int GetHashCode() => Raw.GetHashCode();
    public override string ToString() => (Raw / (double)OneRaw).ToString("F4");

    /// <summary>
    /// 平方根（整数位扫描法，纯整数运算确定性）。
    /// 输入范围：a >= 0 且 a.Raw ≤ 2.8e14（内部左移 16 位不溢出 ulong；
    /// 本工程距离平方最大约 2e12，余量 100 倍以上）
    /// </summary>
    public static Fix Sqrt(Fix a)
    {
        if (a.Raw <= 0) return Zero;

        // sqrt(a) 的定点原始值 = isqrt(a.Raw << 16)
        var num = (ulong)a.Raw << FractionBits;
        ulong res = 0;
        var bit = 1UL << 62; // 从最高可能的平方位开始
        while (bit > num) bit >>= 2;
        while (bit != 0)
        {
            if (num >= res + bit)
            {
                num -= res + bit;
                res = (res >> 1) + bit;
            }
            else
            {
                res >>= 1;
            }
            bit >>= 2;
        }
        return new Fix((long)res);
    }
}

/// <summary>
/// 确定性随机数（xorshift64*）：
/// 纯 64 位整数运算，同种子在任意平台产生完全一致的序列。
/// 种子由战斗开始消息统一下发（阶段 5 暂用常量种子），双端各自演算结果相同。
/// 禁止在战斗逻辑中使用 UnityEngine.Random / System.Random
/// </summary>
public class XRng
{
    private ulong _state;

    public XRng(ulong seed)
    {
        _state = seed;
        if (_state == 0) _state = 0x9E3779B97F4A7C15UL; // 全零状态会导致序列退化
    }

    /// <summary>[min, max) 均匀随机整数</summary>
    public int NextInt(int min, int max) => min + (int)(NextULong() % (ulong)(max - min));

    /// <summary>[0, max) 均匀随机整数（常用简写，max=100 即百分数域）</summary>
    public int NextInt(int max) => (int)(NextULong() % (ulong)max);

    /// <summary>[min, max) 均匀随机 Fix（业务直接写小数，如 rng.NextFix(0.1, 0.3)）。
    /// 内部：随机整数 raw 域映射到区间，纯整数运算，双端一致</summary>
    public Fix NextFix(double min, double max)
    {
        // 区间 raw 宽度（min/max 是编译期字面量常量，转换确定，见 Fix.FromDouble 说明）
        var minFix = Fix.FromDouble(min);
        var maxFix = Fix.FromDouble(max);
        var widthRaw = maxFix.Raw - minFix.Raw;
        // [0, width) 整数随机（width 是 long，取 abs 防负区间写反）
        var width = widthRaw < 0 ? -widthRaw : widthRaw;
        var offset = (long)(NextULong() % (ulong)width);
        return Fix.FromRaw((widthRaw < 0 ? maxFix.Raw : minFix.Raw) + offset);
    }

    /// <summary>概率判定：以 pct/100 的概率为 true（如 pct=5 表示 5%）。
    /// 内部在 [0,10000) 取值与 pct*100 比较（百分数扩大为万分比，整数比较无歧义）</summary>
    public bool Chance(int pct)
    {
        var v = NextULong() % 10000UL;
        return v < (ulong)(pct * 100);
    }

    /// <summary>概率判定：以万分比概率为 true（basisPoints=500 表示 5.00%）</summary>
    public bool ChanceBp(int basisPoints)
    {
        var v = NextULong() % 10000UL;
        return v < (ulong)basisPoints;
    }
    
    /// <summary>下一个 64 位随机数</summary>
    private ulong NextULong()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        return _state * 2685821657736338717UL;
    }
}
