/*--------------------------------------------------------------
 * File: IUpdateable.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/11/11 18:52:59 
 *--------------------------------------------------------------
 */

public interface IUpdateable 
{
    /// <summary>
    /// 优先级，值越大越先轮询
    /// </summary>
    int Priority
    {
        get
        {
            return (int)UpdatePriority.Default;
        }
    }

    /// <summary>
    /// 游戏框架轮询
    /// </summary>
    /// <param name="deltaTime">帧间隔，单位为秒，受timeScale影响</param>
    /// <param name="realDeltaTime">帧间隔，单位为秒，不受timeScale影响</param>
    void MyUpdate(float deltaTime, float realDeltaTime);
}

/// <summary>
/// 模块优先级，高的优先轮询
/// 框架层只提供default，具体优先级类型由业务提供
/// </summary>
public enum UpdatePriority
{
    Default = 0,
}
