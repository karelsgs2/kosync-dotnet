
using Microsoft.AspNetCore.Mvc;
using Kosync.Database.Entities;

namespace Kosync.Controllers;

[ApiController]
public class ManagementController : ControllerBase
{
    private readonly ILogger<ManagementController> _logger;
    private readonly ProxyService _proxyService;
    private readonly IPService _ipService;
    private readonly KosyncDb _db;
    private readonly UserService _userService;

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
    public ObjectResult UpdateSettings([FromBody] Dictionary<string, string> payload)
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

        LogInfo($"Settings updated by [{_userService.Username}]");
        return StatusCode(200, new { message = "Settings updated" });
    }

    [HttpGet("/manage/users")]
    public ObjectResult GetUsers()
    {
        if (!_userService.IsAdmin) return StatusCode(401, new { message = "Unauthorized" });

        var userCollection = _db.Context.GetCollection<DbUser>("users");
        var users = userCollection.FindAll().Select(i => new
        {
            id = i.Id,
            username = i.Username,
            isAdministrator = i.IsAdministrator,
            isActive = i.IsActive,
            documentCount = i.Documents?.Count ?? 0
        });

        return StatusCode(200, users);
    }

    [HttpPost("/manage/users")]
    public ObjectResult CreateUser([FromBody] UserCreateRequest payload)
    {
        if (!_userService.IsAdmin) return StatusCode(401, new { message = "Unauthorized" });

        var userCollection = _db.Context.GetCollection<DbUser>("users");
        if (userCollection.Exists(i => i.Username == payload.username)) return StatusCode(400, new { message = "User already exists" });

        var user = new DbUser()
        {
            Username = payload.username,
            PasswordHash = Utility.HashPassword(payload.password)
        };

        userCollection.Insert(user);
        userCollection.EnsureIndex(u => u.Username);

        LogInfo($"User [{payload.username}] created by [{_userService.Username}]");
        return StatusCode(200, new { message = "User created successfully" });
    }

    [HttpDelete("/manage/users")]
    public ObjectResult DeleteUser([FromQuery] string username)
    {
        if (!_userService.IsAdmin && username != _userService.Username) return StatusCode(401, new { message = "Unauthorized" });

        var userCollection = _db.Context.GetCollection<DbUser>("users");
        var user = userCollection.FindOne(u => u.Username == username);
        if (user is null) return StatusCode(404, new { message = "User not found" });

        userCollection.Delete(user.Id);
        LogInfo($"User [{username}] deleted by [{_userService.Username}]");
        return StatusCode(200, new { message = "Success" });
    }

    private void LogInfo(string text) => Log(LogLevel.Information, text);
    private void Log(LogLevel level, string text)
    {
        string clientIp = _ipService?.ClientIP ?? "unknown";
        string logMsg = $"[{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")}] [{clientIp}] {text}";
        _logger?.Log(level, logMsg);
    }
}
