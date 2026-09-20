/*--------------------------------------------------------------
 * File: BuildConfig.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/01/12 11:00:00 
 *--------------------------------------------------------------
 */

using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "EditorPathConfig", menuName = "Game/EditorPathConfig")]
public class EditorPathConfig : ScriptableObject
{
    [Title("服务器路径")]
    [FolderPath(AbsolutePath = true)]
    public string remotePath = "";
    
    [Title("热更dll源目录")]
    [FolderPath(AbsolutePath = true)]
    public string dllSourcePath = "";
    
    [Title("热更dll目标目录")]
    [FolderPath(AbsolutePath = true)]
    public string dllTargetPath = "";
    
    [Title("excel导表工具路径")]
    [FilePath(AbsolutePath = true)]
    public string excelShellPath = "";
    
    [Title("proto导入工具路径")]
    [FilePath(AbsolutePath = true)]
    public string protoShellPath = "";
}
