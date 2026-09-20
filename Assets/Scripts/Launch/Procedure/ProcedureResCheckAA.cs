/*--------------------------------------------------------------
 * File: ProcedureResCheckAA.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/12/11 14:09:47 
 *--------------------------------------------------------------
 */

using System;
using Cysharp.Threading.Tasks;
public class ProcedureResCheckAA : ProcedureBase
{
    private const int Mb = 1024 * 1024;
    
    private AddressableHelper _helper;
    private LoginPanel _loginPanel;

    public override void OnInit(Fsm fsm)
    {
        base.OnInit(fsm);
        _helper = new AddressableHelper();
        _helper.OnDownloadProgress += OnHelperDownloadProgress;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        
        _loginPanel = _fsm.GetData<LoginPanel>(LaunchConfig.LoginPanel);
        AsyncRun().Forget();
    }

    private async UniTaskVoid AsyncRun()
    {
        try
        {
            _loginPanel.SetTip("更新资源列表...", 0.3f);

            // WX 桥接层请求超时后，C# 侧的 await 永远不返回——没有异常、没有失败状态
            await _helper.InitAsync().Timeout(TimeSpan.FromSeconds(10));
            await _helper.UpdateCatalog().Timeout(TimeSpan.FromSeconds(10));
            await _helper.CheckRes().Timeout(TimeSpan.FromSeconds(10));
            await _helper.Download(); 

            ChangeTo<ProcedureLoadDll>();
        }
        catch (ResourceUpdateException e)
        {
            // 目前服务器为内网，我希望在外网能够跳过热更直接进游戏玩单机模式
            ChangeTo<ProcedureLoadDll>();
            Log.Error("热更失败:", e.ErrorCode, e.Message, e.InnerException, e.StackTrace);
        }
        catch (TimeoutException e)
        {
            ChangeTo<ProcedureLoadDll>();
            Log.Error("热更超时:", e.Message, e.InnerException, e.StackTrace);
        }
        
        catch (Exception e)
        {
            Log.Error("更新执行出错:", e.Message, e.InnerException, e.StackTrace);
        }
    }
    
    private void OnHelperDownloadProgress(float arg1, long arg2, long arg3)
    {
        _loginPanel.SetTip($"资源下载中...({(arg2 / Mb):F2}/{(arg3 / Mb):F2})", 0.3f + 0.4f * arg1);
    }
}