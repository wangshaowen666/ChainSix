/*--------------------------------------------------------------
 * File: ProcedureLaunch.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/11/12 16:25:31 
 *--------------------------------------------------------------
 */

using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class ProcedureLaunch : ProcedureBase
{
    private readonly int _layer = LayerMask.NameToLayer("UI");
    private Transform _uiRoot;
    
    public override void OnEnter()
    {
        base.OnEnter();
        GameCamera.CreateUICamera();
        CreateUICanvas();
        
        var panel = LoadLoginPanel();
        _fsm.SetData(LaunchConfig.LoginPanel, panel);
        panel.SetTip("游戏启动中...", 0.1f);

        ChangeTo<ProcedureVersionCheck>();
    }
    
    private LoginPanel LoadLoginPanel()
    {
        GameObject loginPrefab = Resources.Load<GameObject>(LaunchConfig.LoginPath);
        if (loginPrefab == null)
        {
            Log.Error("登陆界面路径错误：", LaunchConfig.LoginPath);
            return null;
        }

        GameObject loginInstance = Object.Instantiate(loginPrefab, _uiRoot);
        return loginInstance.GetComponent<LoginPanel>();
    }
    
    private void CreateUICanvas()
    {
        var obj = new GameObject()
        {
            name = "UICanvas",
            layer = _layer
        };
        
        var canvas = obj.AddComponent<Canvas>();
        obj.AddComponent<GraphicRaycaster>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = GameCamera.UICamera;
        
        var uiRoot = new GameObject { name = LaunchConfig.UIRoot, layer = _layer };
        uiRoot.transform.SetParent(obj.transform, false);
        var rt = uiRoot.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        _uiRoot = uiRoot.transform;
        
        InitCanvasScaler(obj);
        InitEventSystem(obj.transform);
        
        Object.DontDestroyOnLoad(obj);
    }
    
    private void InitCanvasScaler(GameObject obj)
    {
        var model = LaunchConfig.ScreenSize;
        var scaler = obj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = model;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = Screen.width * 1.0f / Screen.height > model.x * 1.0f / model.y ? 1 : 0;
    }

    private void InitEventSystem(Transform parent)
    {
        var obj = new GameObject("EventSystem");
        obj.AddComponent<InputSystemUIInputModule>();
        obj.transform.SetParent(parent, false);
    }
}