/*--------------------------------------------------------------
 * File: ShellUtil.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/01/09 18:50:11 
 *--------------------------------------------------------------
 */

using System;
using System.Diagnostics;
using UnityEngine;

public static class ShellUtil 
{
    public static string Run(string scriptPath)
    {
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = "/bin/sh"; // Linux/macOS使用sh，Windows可用powershell
        psi.Arguments = scriptPath;
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        
        Process process = Process.Start(psi);
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        
        process.WaitForExit();

        string ret = "";
        if (!string.IsNullOrEmpty(output))
        {
            Log.Info("Shell Output: " + output);
            ret = output;
        }

        if (!string.IsNullOrEmpty(error))
        {
            Log.Error("Shell Error: " + error);
            ret = error;
        }
        
        return ret;
    }
    
    public static void RunWithArgs(string scriptPath, string[] args)
    {
        string arguments = scriptPath;
        foreach (string arg in args)
        {
            arguments += " " + arg;
        }
    
        ProcessStartInfo psi = new ProcessStartInfo("/bin/sh", arguments);
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
    
        Process process = Process.Start(psi);
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    
        UnityEngine.Debug.Log(output);
    }
}