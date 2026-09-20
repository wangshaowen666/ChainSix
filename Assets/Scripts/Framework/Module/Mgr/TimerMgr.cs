/*--------------------------------------------------------------
 * File: TimerMgr.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/05/22 16:09:58
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

public class TimerMgr : ManagerBase
{
    private readonly HashSet<CancellationTokenSource> _activeCts = new();
    private readonly object _ctsLock = new();

    #region 延迟执行

    /// <summary>
    /// 延迟指定毫秒后执行
    /// </summary>
    /// <param name="delayMs">延迟毫秒</param>
    /// <param name="action">回调</param>
    /// <param name="linkedToken">绑定外部取消源（如UI销毁自动取消，传入this.GetCancellationTokenOnDestroy()）</param>
    /// <param name="ignoreTimeScale">是否忽略时间缩放</param>
    /// <param name="inMainThread">是否在主线程执行回调，false则在子线程执行（适用于纯计算/数据解析）</param>
    public CancellationTokenSource StartDelay(int delayMs, Action action,
        CancellationToken linkedToken = default, bool ignoreTimeScale = false, bool inMainThread = true)
    {
        var cts = CreateCts(linkedToken);
        lock (_ctsLock) _activeCts.Add(cts);
        DelayRun(delayMs, action, cts.Token, ignoreTimeScale, inMainThread, cts).Forget();
        return cts;
    }

    /// <summary>
    /// 延迟指定秒后执行，推荐使用StartDelay毫秒方法，少一点运算量
    /// </summary>
    public CancellationTokenSource StartSecondDelay(float delaySd, Action action,
        CancellationToken linkedToken = default, bool ignoreTimeScale = false, bool inMainThread = true)
    {
        return StartDelay((int)(delaySd * 1000f), action, linkedToken, ignoreTimeScale, inMainThread);
    }

    /// <summary>
    /// 延迟指定帧后执行
    /// </summary>
    public CancellationTokenSource StartFrameDelay(int delayFrame, Action action,
        CancellationToken linkedToken = default, bool inMainThread = true)
    {
        var cts = CreateCts(linkedToken);
        lock (_ctsLock) _activeCts.Add(cts);
        FrameDelayRun(delayFrame, action, cts.Token, inMainThread, cts).Forget();
        return cts;
    }

    #endregion

    #region 重复执行

    /// <summary>
    /// 间隔指定毫秒重复执行
    /// </summary>
    /// <param name="intervalMs">间隔毫秒</param>
    /// <param name="action">回调</param>
    /// <param name="repeatCount">重复次数，-1为无限</param>
    /// <param name="isImmediately">是否立即执行一次</param>
    /// <param name="linkedToken">绑定外部取消源</param>
    /// <param name="ignoreTimeScale">是否忽略时间缩放</param>
    /// <param name="inMainThread">是否在主线程执行回调，false则在子线程执行</param>
    public CancellationTokenSource StartRepeat(int intervalMs, Action action, int repeatCount = -1,
        bool isImmediately = false, CancellationToken linkedToken = default,
        bool ignoreTimeScale = false, bool inMainThread = true)
    {
        var cts = CreateCts(linkedToken);
        lock (_ctsLock) _activeCts.Add(cts);
        RepeatRun(intervalMs, action, repeatCount, isImmediately, cts.Token, ignoreTimeScale, inMainThread, cts).Forget();
        return cts;
    }

    /// <summary>
    /// 间隔指定帧重复执行
    /// </summary>
    public CancellationTokenSource StartFrameRepeat(int intervalFrame, Action action, int repeatCount = -1,
        bool isImmediately = false, CancellationToken linkedToken = default, bool inMainThread = true)
    {
        var cts = CreateCts(linkedToken);
        lock (_ctsLock) _activeCts.Add(cts);
        FrameRepeatRun(intervalFrame, action, repeatCount, isImmediately, cts.Token, inMainThread, cts).Forget();
        return cts;
    }

    #endregion

    #region 取消

    /// <summary>
    /// 取消指定计时
    /// </summary>
    public void Stop(CancellationTokenSource cts)
    {
        if (cts == null) return;
        cts.Cancel();
        cts.Dispose();
        lock (_ctsLock) _activeCts.Remove(cts);
    }

    /// <summary>
    /// 取消所有计时
    /// </summary>
    public void StopAll()
    {
        List<CancellationTokenSource> snapshot;
        lock (_ctsLock)
        {
            snapshot = new List<CancellationTokenSource>(_activeCts);
            _activeCts.Clear();
        }
        foreach (var cts in snapshot)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    #endregion

    private CancellationTokenSource CreateCts(CancellationToken linkedToken)
    {
        return linkedToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(linkedToken)
            : new CancellationTokenSource();
    }

    private void RemoveCts(CancellationTokenSource cts)
    {
        lock (_ctsLock) _activeCts.Remove(cts);
    }

    #region 内部实现

    private async UniTaskVoid DelayRun(int delayMs, Action action, CancellationToken token,
        bool ignoreTimeScale, bool inMainThread, CancellationTokenSource cts)
    {
        try
        {
            await UniTask.Delay(delayMs, ignoreTimeScale, cancellationToken: token);

            if (!inMainThread)
                await UniTask.SwitchToThreadPool();

            action.Invoke();
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Log.Error("定时器执行异常", e);
        }
        finally
        {
            if (!inMainThread)
            {
                try { await UniTask.SwitchToMainThread(); } catch { }
            }
            RemoveCts(cts);
        }
    }

    private async UniTaskVoid FrameDelayRun(int delayFrame, Action action, CancellationToken token,
        bool inMainThread, CancellationTokenSource cts)
    {
        try
        {
            await UniTask.DelayFrame(delayFrame, cancellationToken: token);

            if (!inMainThread)
                await UniTask.SwitchToThreadPool();

            action.Invoke();
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Log.Error("定时器执行异常", e);
        }
        finally
        {
            if (!inMainThread)
            {
                try { await UniTask.SwitchToMainThread(); } catch { }
            }
            RemoveCts(cts);
        }
    }

    private async UniTaskVoid RepeatRun(int intervalMs, Action action, int repeatCount,
        bool isImmediately, CancellationToken token, bool ignoreTimeScale, bool inMainThread,
        CancellationTokenSource cts)
    {
        try
        {
            int count = 0;
            if (isImmediately)
            {
                if (!inMainThread)
                    await UniTask.SwitchToThreadPool();
                count++;
                action.Invoke();
            }

            while (!token.IsCancellationRequested && (repeatCount == -1 || count < repeatCount))
            {
                await UniTask.Delay(intervalMs, ignoreTimeScale, cancellationToken: token);
                if (token.IsCancellationRequested) break;

                if (!inMainThread)
                    await UniTask.SwitchToThreadPool();

                count++;
                action.Invoke();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Log.Error("定时器执行异常", e);
        }
        finally
        {
            if (!inMainThread)
            {
                try { await UniTask.SwitchToMainThread(); } catch { }
            }
            RemoveCts(cts);
        }
    }

    private async UniTaskVoid FrameRepeatRun(int intervalFrame, Action action, int repeatCount,
        bool isImmediately, CancellationToken token, bool inMainThread, CancellationTokenSource cts)
    {
        try
        {
            int count = 0;
            if (isImmediately)
            {
                if (!inMainThread)
                    await UniTask.SwitchToThreadPool();
                count++;
                action.Invoke();
            }

            while (!token.IsCancellationRequested && (repeatCount == -1 || count < repeatCount))
            {
                await UniTask.DelayFrame(intervalFrame, cancellationToken: token);
                if (token.IsCancellationRequested) break;

                if (!inMainThread)
                    await UniTask.SwitchToThreadPool();

                count++;
                action.Invoke();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Log.Error("定时器执行异常", e);
        }
        finally
        {
            if (!inMainThread)
            {
                try { await UniTask.SwitchToMainThread(); } catch { }
            }
            RemoveCts(cts);
        }
    }

    #endregion
}
