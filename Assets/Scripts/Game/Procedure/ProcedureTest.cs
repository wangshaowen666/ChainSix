/*--------------------------------------------------------------
 * File: ProcedureTest.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/08/27 18:45:45 
 *--------------------------------------------------------------
 */

using System;
using UnityEngine;

public class ProcedureTest : ProcedureBase
{
    public override void OnEnter()
    {
        base.OnEnter();
        GameCamera.SetMainCamera(GameCamera.TestCameraName);
    }
}