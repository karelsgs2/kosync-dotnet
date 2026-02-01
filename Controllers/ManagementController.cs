
using Microsoft.AspNetCore.Mvc;

namespace Kosync.Controllers;

[ApiController]
public class ManagementController : ControllerBase
{
    private ILogger<ManagementController> _logger;

    private ProxyService _proxyService;
    private IPService _ipService;
    private KosyncDb _db;
    private UserService _userService;


    public ManagementController(ILogger<ManagementController> logger, ProxyService proxyService, IPService ipService, KosyncDb db, UserService userService)
    {
        _logger = logger;
        _proxyService = proxyService;
        _ipService = ipService;
        _db = db;
        _userService = userService;
    }

    [HttpGet("/manage/settings")]
    public ObjectResult GetSettings()
    {
        if (!_userService.IsAdmin) return StatusCode(401, new { message = "Unauthorized" });

        var settingsCollection = _db.Context.GetCollection<SystemSetting>("system_settings");
        var settings = settingsCollection.FindAll().ToDictionary(s => s.Key, s => s.Value);
        
        return StatusCode(200, settings);
    }

    [HttpPut("/manage/settings")]
    public ObjectResult UpdateSettings(Dictionary<string, string> payload)
    {
        if (!_userService.IsAdmin) return StatusCode(401, new { message = "Unauthorized" });

        var settingsCollection = _db.Context.GetCollection<SystemSetting>("system_settings");
        foreach (var entry in payload)
        {
            var s = settingsCollection.FindOne(i => i.Key == entry.Key);
            if (s != null)
            {
                s.Value = entry.Value;
                settingsCollection.Update(s);
            }
            else
            {
                settingsCollection.Insert(new SystemSetting { Key = entry.Key, Value = entry.Value });
            }
        }

        LogInfo($"User [{_userService.Username}] updated system settings.");
        return StatusCode(200, new { message = "Settings updated" });
    }

    [HttpGet("/manage/users")]
    public ObjectResult GetUsers()
    {
        if (!_userService.IsAuthenticated || !_userService.IsAdmin || !_userService.IsActive)
        {
            return StatusCode(401, new { message = "Unauthorized" });
        }

        var userCollection = _db.Context.GetCollection<User>("users");
        var users = userCollection.FindAll().Select(i => new
        {
            id = i.Id,
            username = i.Username,
            isAdministrator = i.IsAdministrator,
            isActive = i.IsActive,
            documentCount = i.Documents.Count()
        });

        LogInfo($"User [{_userService.Username}] requested /manage/users");
        return StatusCode(200, users);
    }

    [HttpPost("/manage/users")]
    public ObjectResult CreateUser(UserCreateRequest payload)
    {
        if (!_userService.IsAuthenticated || !_userService.IsAdmin || !_userService.IsActive)
        {
            return StatusCode(401, new { message = "Unauthorized" });
        }

        var userCollection = _db.Context.GetCollection<User>("users");
        var existingUser = userCollection.FindOne(i => i.Username == payload.username);
        if (existingUser is not null) return StatusCode(400, new { message = "User already exists" });

        var user = new User()
        {
            Username = payload.username,
            PasswordHash = Utility.HashPassword(payload.password),
            IsAdministrator = false
        };

        userCollection.Insert(user);
        userCollection.EnsureIndex(u => u.Username);

        LogInfo($"User [{payload.username}] created by user [{_userService.Username}]");
        return StatusCode(200, new { message = "User created successfully" });
    }

    [HttpDelete("/manage/users")]
    public ObjectResult DeleteUser(string username)
    {
        if (!_userService.IsAuthenticated || (!_userService.IsAdmin && !username.Equals(_userService.Username, StringComparison.OrdinalIgnoreCase)) || !_userService.IsActive)
        {
            return StatusCode(401, new { message = "Unauthorized" });
        }

        var userCollection = _db.Context.GetCollection<User>("users");
        var user = userCollection.FindOne(u => u.Username == username);
        if (user is null) return StatusCode(404, new { message = "User does not exist" });

        userCollection.Delete(user.Id);
        LogInfo($"User [{username}] deleted by [{_userService.Username}]");
        return StatusCode(200, new { message = "Success" });
    }

    [HttpGet("/manage/users/documents")]
    public ObjectResult GetDocuments(string username)
    {
        if (!_userService.IsAuthenticated || (!_userService.IsAdmin && !username.Equals(_userService.Username, StringComparison.OrdinalIgnoreCase)) || !_userService.IsActive)
        {
            return StatusCode(401, new { message = "Unauthorized" });
        }

        var userCollection = _db.Context.GetCollection<User>("users");
        var user = userCollection.FindOne(i => i.Username == username);
        if (user is null) return StatusCode(400, new { message = "User does not exist" });

        return StatusCode(200, user.Documents);
    }

    [HttpPut("/manage/users/active")]
    public ObjectResult UpdateUserActive(string username)
    {
        if (!_userService.IsAuthenticated || !_userService.IsAdmin || !_userService.IsActive)
        {
            return StatusCode(401, new { message = "Unauthorized" });
        }

        if (username == "admin") return StatusCode(400, new { message = "Cannot update admin user" });

        var userCollection = _db.Context.GetCollection<User>("users");
        var user = userCollection.FindOne(i => i.Username == username);
        if (user is null) return StatusCode(400, new { message = "User does not exist" });

        user.IsActive = !user.IsActive;
        userCollection.Update(user);

        LogInfo($"User [{username}] set to {(user.IsActive ? "active" : "inactive")} by user [{_userService.Username}]");
        return StatusCode(200, new { message = user.IsActive ? "User marked as active" : "User marked as inactive" });
    }

    [HttpPut("/manage/users/password")]
    public ObjectResult UpdatePassword(string username, PasswordChangeRequest payload)
    {
        if (!_userService.IsAuthenticated || !_userService.IsAdmin || !_userService.IsActive)
        {
            return StatusCode(401, new { message = "Unauthorized" });
        }

        if (string.IsNullOrWhiteSpace(payload.password)) return StatusCode(400, new { message = "Password cannot be empty" });
        if (username == "admin") return StatusCode(400, new { message = "Cannot update admin user" });

        var userCollection = _db.Context.GetCollection<User>("users");
        var user = userCollection.FindOne(i => i.Username == username);
        if (user is null) return StatusCode(400, new { message = "User does not exist" });

        user.PasswordHash = Utility.HashPassword(payload.password);
        userCollection.Update(user);

        LogInfo($"User [{username}] password updated by [{_userService.Username}].");
        return StatusCode(200, new { message = "Password changed successfully" });
    }

    private void LogInfo(string text) => Log(LogLevel.Information, text);
    private void Log(LogLevel level, string text)
    {
        string logMsg = $"[{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")}] [{_ipService.ClientIP}]";
        if (_proxyService.TrustedProxies.Length > 0 && !_ipService.TrustedProxy) logMsg += "*";
        logMsg += $" {text}";
        _logger?.Log(level, logMsg);
    }
}
