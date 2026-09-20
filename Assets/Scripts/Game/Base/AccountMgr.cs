/*--------------------------------------------------------------
 * File: AccountMgr.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/08/13 14:00:00 
 *--------------------------------------------------------------
 */

using System;

/// <summary>
/// 账号服务：注册、登录、token 校验。登录态存 PlayerPrefs 持久化。
/// 对外全部为回调 API（内部由 HttpMsgHandler 用 UniTask 异步执行）
/// </summary>
public class AccountMgr : ManagerBase
{
    private const string TokenKey = "login_token";
    private const string PlayerIdKey = "login_playerId";
    private const string UsernameKey = "login_username";

    public string Token => PlayerPrefsUtil.GetString(TokenKey);
    public int PlayerId => PlayerPrefsUtil.GetInt(PlayerIdKey);
    public string Username => PlayerPrefsUtil.GetString(UsernameKey);
    public bool IsLoggedIn => !string.IsNullOrEmpty(Token);

    /// <summary>登录态变化（登录成功/登出）时触发，用于让依赖 token 的缓存失效重建</summary>
    public event Action OnLoginStateChanged;

    /// <summary>
    /// 注册，完成后回调（看 resp.ErrorCode 判断是否成功）
    /// </summary>
    public void Register(string username, string password, Action<NetMsg.RegisterResp> onDone)
    {
        var req = new NetMsg.RegisterReq { Username = username, Password = password };
        GameMgr.HttpMsg.Post(req, onDone);
    }

    /// <summary>
    /// 登录，成功后自动保存 token/playerId/username 到本地，完成后回调
    /// </summary>
    public void Login(string username, string password, Action<NetMsg.LoginResp> onDone)
    {
        var req = new NetMsg.LoginReq { Username = username, Password = password };
        GameMgr.HttpMsg.Post<NetMsg.LoginResp>(req, resp =>
            {
                if (resp.ErrorCode == NetMsg.ErrorCode.ErrorNone && !string.IsNullOrEmpty(resp.Token))
                {
                    PlayerPrefsUtil.SetString(TokenKey, resp.Token);
                    PlayerPrefsUtil.SetInt(PlayerIdKey, resp.PlayerId);
                    PlayerPrefsUtil.SetString(UsernameKey, resp.Username);
                    Log.Info("登录成功:", resp.Username);
                    OnLoginStateChanged?.Invoke();
                }
                else
                {
                    Log.Error("登录失败, 错误码:", resp.ErrorCode);
                }
                onDone?.Invoke(resp);
            });
    }

    /// <summary>
    /// 用本地保存的 token 请求 /me，验证 token 是否仍有效，完成后回调
    /// </summary>
    public void GetMe(Action<NetMsg.GetMeResp> onDone)
    {
        GameMgr.HttpMsg.Get(onDone);
    }

    /// <summary>
    /// 退出登录，清除本地登录态
    /// </summary>
    public void Logout()
    {
        PlayerPrefsUtil.DeleteKey(TokenKey);
        PlayerPrefsUtil.DeleteKey(PlayerIdKey);
        PlayerPrefsUtil.DeleteKey(UsernameKey);
        OnLoginStateChanged?.Invoke();
    }
}
