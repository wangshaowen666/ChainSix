/*--------------------------------------------------------------
 * File: Empty4Raycast.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/05/22 17:53:55 
 *--------------------------------------------------------------
 */

using UnityEngine;
using UnityEngine.UI;

public class Empty4Raycast : MaskableGraphic, ICanvasRaycastFilter
{
    protected Empty4Raycast()
    {
        useLegacyMeshGeneration = false;
    }
    protected override void OnPopulateMesh(VertexHelper toFill)
    {
        toFill.Clear();
    }

    public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
    {
        return true;
    }
}