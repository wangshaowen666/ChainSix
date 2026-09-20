/*--------------------------------------------------------------
 * File: EncodeUtil.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/11/26 15:35:19 
 *--------------------------------------------------------------
 */

using System;
using System.Security.Cryptography;
using MiniJSON;

/*
 * MD5算法目前被认为是不安全的，尤其是在需要防范碰撞攻击（即两个不同的输入产生相同的哈希值）的密码学场景中
 *  
 */

public static class ParseUtil 
{
    public static string MD5Str(string source)
    {
        MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
        byte[] dataRaw = System.Text.Encoding.UTF8.GetBytes(source);
        byte[] data = md5.ComputeHash(dataRaw, 0, dataRaw.Length);
        md5.Clear();

        string destString = "";
        for (int i = 0; i < data.Length; i++)
        {
            destString += System.Convert.ToString(data[i], 16).PadLeft(2, '0');
        }

        destString = destString.PadLeft(32, '0');
        return destString;
    }

    /// <summary>
    /// MemoryPack转换成二进制，测试比自带的BinaryFormatter性能提升15倍
    /// </summary>
    /// <param name="t"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    // public static byte[] ToBinary<T>(T t)
    // {
    //     try
    //     {
    //         byte[] bytes = MemoryPackSerializer.Serialize(t);
    //         return bytes;
    //     }
    //     catch (Exception e)
    //     {
    //         Log.Error("Binary序列化出错：", e);
    //         return null;
    //     }
    // }
    //
    // public static T DeBinary<T>(byte[] bytes)
    // {
    //     try
    //     {
    //         T ret = MemoryPackSerializer.Deserialize<T>(bytes);
    //         return ret;
    //     }
    //     catch (Exception e)
    //     {
    //         Log.Error("Binary反序列化出错：", e);
    //         return default;
    //     }
    // }
    
    public static string ToJson<T>(T t)
    {
        try
        {
            return Json.Serialize(t);
        }
        catch (Exception e)
        {
            Log.Error("Json序列化出错：", e);
            return null;
        }
    }

    /// <summary>
    /// MiniJson轻量级转换成obj，复杂结构只支持Dictionary(string,object)和List(object),需自行转化
    /// </summary>
    /// <param name="json"></param>
    /// <returns></returns>
    public static object DeJson(string json)
    {
        try
        {
            return Json.Deserialize(json);
        }
        catch (Exception e)
        {
            Log.Error("Json反序列化出错：", json, e.Message);
            return null;
        }
    }
}