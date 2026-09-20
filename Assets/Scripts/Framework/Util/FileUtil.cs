/*--------------------------------------------------------------
 * File: FileUtil.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/11/11 14:21:06 
 *--------------------------------------------------------------
 */

using System;
using System.IO;

public static class FileUtil
{
    // 默认输出路径
    private const string OutPath = "/Users/wangshaowen/Desktop";
    
    /// <summary>
    /// 写文件并输出
    /// </summary>
    /// <param name="content">文本内容</param>
    /// <param name="fileName">文件名</param>
    /// <param name="outPath">输出路径</param>
    public static void WriteFile(string content, string fileName, string outPath = null)
    {
        string path = Path.Combine(string.IsNullOrEmpty(outPath) ? OutPath : outPath, fileName);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// 根据路径获得文件内容
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <returns></returns>
    public static string ReadFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                string content = File.ReadAllText(path);
                return content;
            }

            Log.Error("未找到文件：", path);
            return null;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    
    /// <summary>
    /// 文件夹拷贝
    /// </summary>
    /// <param name="sourceDir">源文件夹</param>
    /// <param name="destinationDir">目标文件集</param>
    /// <param name="overwrite">是否覆盖</param>
    public static void CopyDirectory(string sourceDir, string destinationDir, bool overwrite = true)
    {
        if (!Directory.Exists(sourceDir))
        {
            Log.Error($"源目录不存在: {sourceDir}");
            return;
        }

        if (!Directory.Exists(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        string[] files = Directory.GetFiles(sourceDir);
        foreach (string file in files)
        {
            string fileName = Path.GetFileName(file);
            string destFile = Path.Combine(destinationDir, fileName);
            File.Copy(file, destFile, overwrite); 
        }

        string[] subDirs = Directory.GetDirectories(sourceDir);
        foreach (string subDir in subDirs)
        {
            string dirName = Path.GetFileName(subDir);
            string destSubDir = Path.Combine(destinationDir, dirName);
            CopyDirectory(subDir, destSubDir); 
        }
    }

    /// <summary>
    /// 文件拷贝
    /// </summary>
    /// <param name="sourceFilePath">源文件目录</param>
    /// <param name="destFilePath">目标文件目录</param>
    /// <param name="overwrite">是否覆盖</param>
    public static void CopyFile(string sourceFilePath, string destFilePath, bool overwrite = true)
    {
        if (!File.Exists(sourceFilePath))
        {
            Log.Error("源文件不存在：", sourceFilePath);
            return;
        }

        var folderPath = Path.GetDirectoryName(destFilePath);
        if (folderPath != null && !Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
        
        File.Copy(sourceFilePath, destFilePath, overwrite);
    }

    /// <summary>
    /// 将文件字节数转化成英文形式
    /// </summary>
    /// <param name="byteLength">字节数</param>
    /// <returns></returns>
    public static string GetFileLength(long byteLength)
    {
        if (byteLength < 1024L)
        {
            return $"{byteLength} B";
        }
        
        if (byteLength < 1048576L)
        {
            return $"{byteLength / 1024f:F2} KB";
        }
        
        if (byteLength < 1073741824L) // 2 ^ 30
        {
            return $"{byteLength / 1048576f:F2} MB";
        }
        
        return $"{byteLength / 1073741824f:F2} GB";
    }
    
    
}