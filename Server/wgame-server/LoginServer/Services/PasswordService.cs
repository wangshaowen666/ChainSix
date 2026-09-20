namespace LoginServer.Services;

/// <summary>密码哈希服务（BCrypt）</summary>
public class PasswordService
{
    /// <summary>哈希密码</summary>
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    /// <summary>校验密码</summary>
    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
