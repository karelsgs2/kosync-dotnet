
using Kosync.Database.Entities;

namespace Kosync.Services;

public class UserService
{
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly KosyncDb _db;

    private bool userLoadAttempted = false;

    private string? _username = "";
    public string? Username
    {
        get
        {
            LoadUser();
            return _username;
        }
    }

    private bool _isAuthenticated = false;
    public bool IsAuthenticated
    {
        get
        {
            LoadUser();
            return _isAuthenticated;
        }
    }

    private bool _isActive = false;
    public bool IsActive
    {
        get
        {
            LoadUser();
            return _isActive;
        }
    }

    private bool _isAdmin = false;
    public bool IsAdmin
    {
        get
        {
            LoadUser();
            return _isAdmin;
        }
    }

    public UserService(IHttpContextAccessor contextAccessor, KosyncDb db)
    {
        _contextAccessor = contextAccessor;
        _db = db;
    }

    private void LoadUser()
    {
        if (userLoadAttempted) return;
        userLoadAttempted = true;

        _username = _contextAccessor?.HttpContext?.Request.Headers["x-auth-user"];
        string? passwordHash = _contextAccessor?.HttpContext?.Request.Headers["x-auth-key"];

        if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(passwordHash)) return;

        var userCollection = _db.Context.GetCollection<DbUser>("users");
        var user = userCollection.FindOne(i => i.Username == _username && i.PasswordHash == passwordHash);

        if (user is null) return;

        _isAuthenticated = true;
        _isActive = user.IsActive;
        _isAdmin = user.IsAdministrator;
    }
}
