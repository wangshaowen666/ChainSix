/*--------------------------------------------------------------
 * File: AutoID.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/11/27 17:27:47 
 *--------------------------------------------------------------
 */

using System;
using UnityEngine;

public static class AutoID
{
    private static uint _id = 0;

    public static uint GetID()
    {
        if (_id == uint.MaxValue)
            _id = 0;
        
        return ++_id;
    }
}