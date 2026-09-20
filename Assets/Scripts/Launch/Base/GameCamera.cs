using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class GameCamera 
{
    public const string MapCameraName = "MapCamera";
    public const string BattleCameraName = "BattleCamera";
    public const string TestCameraName = "TestCamera";
    public const string UICameraName = "UICamera";
    
    public static Camera UICamera { get; private set; }
    public static Camera MainCamera { get; private set; }

    public static void CreateUICamera()
    {
        var obj = new GameObject(LaunchConfig.UICamera);
        var camera = obj.AddComponent<Camera>();
            
        camera.orthographic = true;
        camera.cullingMask = LayerMask.GetMask("UI");;
        camera.clearFlags = CameraClearFlags.Depth;
            
        Object.DontDestroyOnLoad(obj);
        UICamera = camera;
    }
    
    public static void SetMainCamera(string cameraName)
    {
        var camera = Camera.allCameras.FirstOrDefault(cam => string.Equals(cam.name, cameraName));
        if (!camera)
        {
            Log.Error("想要设置的相机不存在：", cameraName);
            return;
        }

        var cameraData = camera.GetUniversalAdditionalCameraData();
        if (cameraData.renderType != CameraRenderType.Base)
        {
            cameraData.renderType = CameraRenderType.Base;
        }
            
        var uiCameraData = UICamera.GetUniversalAdditionalCameraData();
        if (uiCameraData.renderType != CameraRenderType.Overlay)
        {
            uiCameraData.renderType = CameraRenderType.Overlay;
        }
            
        if (!cameraData.cameraStack.Contains(UICamera))
        {
            cameraData.cameraStack.Add(UICamera);
        }
        
        MainCamera = camera;
    }
}
