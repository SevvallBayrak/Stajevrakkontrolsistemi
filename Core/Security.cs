using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace StajEvrak.Core;

// ---------------------------------------------------------------------------
// Şifre özetleme
// ---------------------------------------------------------------------------
public abstract class PasswordHasherBase
{
    public abstract string Hash(string password);
    public abstract bool Verify(string storedHash, string password);
}

/// <summary>PBKDF2-SHA256 + rastgele tuz. Şifreler veritabanında açık tutulmaz (Bölüm 4.1).</summary>
public sealed class Pbkdf2PasswordHasher : PasswordHasherBase
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public override string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"pbkdf2_sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public override bool Verify(string storedHash, string password)
    {
        try
        {
            var parts = (storedHash ?? "").Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2_sha256") return false;
            var iterations = int.Parse(parts[1]);
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password ?? "", salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed class LoginResult
{
    public bool Success { get; init; }
    public string Message { get; init; }
    public User User { get; init; }
    public bool Locked { get; init; }
    public string Token { get; init; }
    public DateTime ExpiresAt { get; init; }
}

public sealed class AuthService
{
    private readonly UserRepository _users;
    private readonly SessionRepository _sessions;
    private readonly LoginAttemptRepository _attempts;
    private readonly PasswordHasherBase _hasher;
    private readonly int _maxFailed;
    private readonly int _lockMinutes;
    private readonly int _sessionHours;
    private readonly string _dummyHash;

    public AuthService(UserRepository users, SessionRepository sessions, LoginAttemptRepository attempts,
                       PasswordHasherBase hasher, int maxFailed, int lockMinutes, int sessionHours)
    {
        _users = users;
        _sessions = sessions;
        _attempts = attempts;
        _hasher = hasher;
        _maxFailed = maxFailed;
        _lockMinutes = lockMinutes;
        _sessionHours = sessionHours;
        _dummyHash = hasher.Hash("dummy-password");
    }

    public PasswordHasherBase Hasher => _hasher;

    // --- Giriş
    public LoginResult Login(string identifier, string password, string ip)
    {
        identifier = (identifier ?? "").Trim();
        var user = identifier.Length > 0 ? _users.FindByLogin(identifier) : null;

        if (user == null)
        {
            // Var olmayan kullanıcı için de aynı süre: zamanlama ile kullanıcı tespiti engellenir
            _hasher.Verify(_dummyHash, password ?? "");
            _attempts.Log(identifier.Length > 0 ? identifier : "(boş)", null, ip, false, "Kullanıcı bulunamadı");
            return new LoginResult { Message = "Kullanıcı adı / e-posta veya şifre hatalı." };
        }

        if (user.IsLocked())
        {
            _attempts.Log(identifier, user.Id, ip, false, "Kilitli hesaba giriş denemesi");
            var kalan = Math.Max(1, (int)(user.LockedUntil.Value - DateTime.Now).TotalMinutes + 1);
            return new LoginResult
            {
                Locked = true,
                Message = $"Hesabınız {_maxFailed} hatalı deneme nedeniyle kilitlendi. Yaklaşık {kalan} dakika sonra tekrar deneyin veya yöneticiye başvurun."
            };
        }

        if (!_hasher.Verify(user.PasswordHash, password ?? ""))
        {
            var lockedNow = user.RegisterFailedAttempt(_maxFailed, DateTime.Now.AddMinutes(_lockMinutes));
            _users.SaveSecurityState(user);
            if (lockedNow)
            {
                _attempts.Log(identifier, user.Id, ip, false, $"Hatalı şifre — hesap kilitlendi ({_maxFailed}/{_maxFailed})");
                return new LoginResult
                {
                    Locked = true,
                    Message = $"{_maxFailed} kez hatalı giriş yapıldı. Hesabınız {_lockMinutes} dakika süreyle kilitlendi."
                };
            }
            _attempts.Log(identifier, user.Id, ip, false, $"Hatalı şifre ({user.FailedAttempts}/{_maxFailed})");
            return new LoginResult
            {
                Message = $"Kullanıcı adı / e-posta veya şifre hatalı. Kalan deneme hakkı: {_maxFailed - user.FailedAttempts}"
            };
        }

        user.ResetFailedAttempts();
        _users.SaveSecurityState(user);
        _attempts.Log(identifier, user.Id, ip, true, "Başarılı giriş");

        // Rol bilgisi sunucu tarafı oturum kaydında taşınır
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var expires = DateTime.Now.AddHours(_sessionHours);
        _sessions.Create(token, user.Id, user.Role, expires);
        return new LoginResult { Success = true, User = user, Token = token, ExpiresAt = expires, Message = $"Hoş geldiniz, {user.FullName}!" };
    }

    /// <summary>Çıkış: token sunucu tarafında geçersiz kılınır.</summary>
    public void Logout(string token)
    {
        if (!string.IsNullOrEmpty(token)) _sessions.Revoke(token);
    }

    /// <summary>Her istekte token ve rol doğrulanır.</summary>
    public User ResolveUser(string token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        var session = _sessions.GetValid(token);
        if (session == null) return null;
        var user = _users.Get(session.UserId);
        if (user == null || user.Role.Code() != session.Role) return null;
        return user;
    }

    // --- Kayıt (öğrenciler hesaplarını kendileri oluşturur)
    public List<string> RegisterStudent(IFormCollection form)
    {
        var errors = RegistrationValidator.Validate(form);
        if (errors.Count > 0) return errors;

        var username = form["username"].ToString().Trim();
        var email = form["email"].ToString().Trim().ToLowerInvariant();
        var studentNo = form["student_no"].ToString().Trim();
        var taken = _users.Exists(username, email);
        if (taken.Username) errors.Add("Bu kullanıcı adı zaten kullanılıyor.");
        if (taken.Email) errors.Add("Bu e-posta adresi ile kayıtlı bir hesap var.");
        if (_users.StudentNoTaken(studentNo)) errors.Add("Bu öğrenci numarası ile kayıtlı bir hesap var.");
        if (errors.Count > 0) return errors;

        _users.Create(username, email, _hasher.Hash(form["password"].ToString()), Role.Student,
            TextUtils.CollapseSpaces(form["full_name"].ToString()), studentNo, form["tc_kimlik_no"].ToString().Trim());
        return errors;
    }

    public List<string> ChangePassword(User user, string current, string newPassword, string confirm)
    {
        if (!_hasher.Verify(user.PasswordHash, current ?? "")) return new List<string> { "Mevcut şifre hatalı." };
        var errors = RegistrationValidator.PasswordErrors(newPassword ?? "", confirm ?? "");
        if (errors.Count > 0) return errors;
        user.ChangePasswordHash(_hasher.Hash(newPassword));
        _users.SaveSecurityState(user);
        return errors;
    }

    public void Unlock(User user)
    {
        user.ResetFailedAttempts();
        _users.SaveSecurityState(user);
    }
}

public static class RegistrationValidator
{
    private static readonly Regex UsernameRe = new(@"^[a-zA-Z0-9._]{3,30}$");
    public static readonly Regex EmailRe = new(@"^[^@\s]+@[^@\s]+\.[A-Za-z]{2,}$");

    public static List<string> Validate(IFormCollection form)
    {
        var errors = new List<string>();
        var fullName = form["full_name"].ToString().Trim();
        if (fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 2)
            errors.Add("Ad ve soyad giriniz.");
        if (!UsernameRe.IsMatch(form["username"].ToString()))
            errors.Add("Kullanıcı adı 3-30 karakter olmalı; harf, rakam, nokta ve alt çizgi içerebilir.");
        if (!EmailRe.IsMatch(form["email"].ToString().Trim()))
            errors.Add("Geçerli bir e-posta adresi giriniz.");
        var sn = form["student_no"].ToString().Trim();
        if (sn.Length < 5 || sn.Length > 12 || !sn.All(char.IsAsciiDigit))
            errors.Add("Öğrenci numarası yalnızca rakamlardan oluşmalı (5-12 hane).");
        var tc = form["tc_kimlik_no"].ToString().Trim();
        if (!(tc.Length == 11 && tc.All(char.IsAsciiDigit) && tc[0] != '0'))
            errors.Add("T.C. / Yabancı kimlik numarası 11 haneli olmalı ve 0 ile başlamamalıdır.");
        errors.AddRange(PasswordErrors(form["password"].ToString(), form["password_confirm"].ToString()));
        return errors;
    }

    public static List<string> PasswordErrors(string password, string confirm)
    {
        var errors = new List<string>();
        if (password.Length < 8 || !Regex.IsMatch(password, @"[A-Za-zÇĞİÖŞÜçğıöşü]") || !Regex.IsMatch(password, @"\d"))
            errors.Add("Şifre en az 8 karakter olmalı, harf ve rakam içermelidir.");
        if (password != confirm)
            errors.Add("Şifreler eşleşmiyor.");
        return errors;
    }
}
