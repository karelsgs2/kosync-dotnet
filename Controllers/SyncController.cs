
using Microsoft.AspNetCore.Mvc;

namespace Kosync.Controllers;

using DbUser = Kosync.Database.Entities.User;

[ApiController]
public class SyncController : ControllerBase
{
    private ILogger<SyncController> _logger;
    private ProxyService _proxyService;
    private IPService _ipService;
    private KosyncDb _db;
    private UserService _userService;

    public SyncController(ILogger<SyncController> logger, ProxyService proxyService, IPService ipService, KosyncDb db, UserService userService)
    {
        _logger = logger;
        _proxyService = proxyService;
        _ipService = ipService;
        _db = db;
        _userService = userService;
    }

    [HttpGet("/")]
    public IActionResult Index() => Ok("kosync-dotnet server is running.");

    [HttpGet("/healthcheck")]
    public ObjectResult HealthCheck() => StatusCode(200, new { state = "OK" });

    [HttpGet("/users/profile")]
    public ObjectResult GetUserProfile()
    {
        if (!_userService.IsAuthenticated) return StatusCode(401, new { message = "Unauthorized" });

        var userCollection = _db.Context.GetCollection<DbUser>("users");
        var user = userCollection.FindOne(i => i.Username == _userService.Username);
        
        return StatusCode(200, new {
            username = user.Username,
            preferences = user.PreferencesJson,
            metadata = user.MetadataJson
        });
    }

    [HttpPut("/users/profile")]
    public ObjectResult UpdateUserProfile([FromBody] UserProfileUpdateRequest payload)
    {
        if (!_userService.IsAuthenticated) return StatusCode(401, new { message = "Unauthorized" });

        var userCollection = _db.Context.GetCollection<DbUser>("users");
        var user = userCollection.FindOne(i => i.Username == _userService.Username);

        if (payload.preferences != null) user.PreferencesJson = payload.preferences;
        if (payload.metadata != null) user.MetadataJson = payload.metadata;

        userCollection.Update(user);
        return StatusCode(200, new { message = "Profile updated" });
    }

    [HttpPut("/users/password")]
    [HttpPut("/users/password/")]
    public ObjectResult UpdateMyPassword([FromBody] PasswordChangeRequest payload)
    {
        try 
        {
            if (!_userService.IsAuthenticated || !_userService.IsActive) 
                return StatusCode(401, new { message = "Unauthorized" });

            if (payload == null || string.IsNullOrWhiteSpace(payload.password)) 
                return StatusCode(400, new { message = "Password cannot be empty" });

            var userCollection = _db.Context.GetCollection<DbUser>("users");
            var user = userCollection.FindOne(i => i.Username == _userService.Username);
            
            if (user is null) return StatusCode(404, new { message = "User not found" });

            user.PasswordHash = Utility.HashPassword(payload.password);
            userCollection.Update(user);

            LogInfo($"User [{_userService.Username}] updated their own password successfully.");
            return StatusCode(200, new { message = "Password updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating password for user {User}", _userService.Username);
            return StatusCode(500, new { message = "Internal Server Error", detail = ex.Message });
        }
    }

    [HttpGet("/users/auth")]
    public ObjectResult AuthoriseUser()
    {
        string? username = Request.Headers["x-auth-user"];
        string? passwordHash = Request.Headers["x-auth-key"];

        if (username is null || passwordHash is null || !_userService.IsAuthenticated)
        {
            return StatusCode(401, new { message = "Invalid credentials" });
        }

        if (!_userService.IsActive) return StatusCode(401, new { message = "User is inactive" });

        LogInfo($"User [{username}] logged in.");
        return StatusCode(200, new { username = _userService.Username });
    }

    [HttpPost("/users/create")]
    public ObjectResult CreateUser([FromBody] UserCreateRequest payload)
    {
        var settingsCollection = _db.Context.GetCollection<SystemSetting>("system_settings");
        var regSetting = settingsCollection.FindOne(s => s.Key == "RegistrationDisabled");
        
        bool registrationDisabled = regSetting?.Value != null && 
                                   regSetting.Value.Equals("true", StringComparison.OrdinalIgnoreCase);

        if (registrationDisabled)
        {
            LogWarning($"Account creation BLOCKED for [{payload.username}] because RegistrationDisabled is set to true.");
            return StatusCode(402, new { message = "User registration is disabled" });
        }

        var userCollection = _db.Context.GetCollection<DbUser>("users");
        var existing = userCollection.FindOne(u => u.Username == payload.username);
        if (existing is not null) return StatusCode(402, new { message = "User already exists" });

        var user = new DbUser() { Username = payload.username, PasswordHash = payload.password };
        userCollection.Insert(user);
        userCollection.EnsureIndex(u => u.Username);

        LogInfo($"User [{payload.username}] created via public registration.");
        return StatusCode(201, new { username = payload.username });
    }

    [HttpPut("/syncs/progress")]
    public ObjectResult SyncProgress([FromBody] DocumentRequest payload)
    {
        if (!_userService.IsAuthenticated || !_userService.IsActive) return StatusCode(401, new { message = "Unauthorized" });

        var userCollection = _db.Context.GetCollection<DbUser>("users");
        var user = userCollection.FindOne(i => i.Username == _userService.Username);

        var document = user.Documents.SingleOrDefault(i => i.DocumentHash == payload.document);
        if (document is null)
        {
            document = new Document { DocumentHash = payload.document };
            user.Documents.Add(document);
        }

        document.Progress = payload.progress;
        document.Percentage = payload.percentage;
        document.Device = payload.device;
        document.DeviceId = payload.device_id;
        document.Timestamp = DateTime.UtcNow;

        userCollection.Update(user);
        return StatusCode(200, new { document = document.DocumentHash, timestamp = document.Timestamp });
    }

    [HttpGet("/syncs/progress/{documentHash}")]
    public IActionResult GetProgress(string documentHash)
    {
        if (!_userService.IsAuthenticated || !_userService.IsActive) return StatusCode(401, new { message = "Unauthorized" });

        var userCollection = _db.Context.GetCollection<DbUser>("users");
        var user = userCollection.FindOne(i => i.Username == _userService.Username);
        var document = user.Documents.SingleOrDefault(i => i.DocumentHash == documentHash);

        if (document is null) return StatusCode(502, new { message = "Document not found" });

        var time = new DateTimeOffset(document.Timestamp);
        var result = new { device = document.Device, device_id = document.DeviceId, document = document.DocumentHash, percentage = document.Percentage, progress = document.Progress, timestamp = time.ToUnixTimeSeconds() };

        return new ContentResult() { Content = System.Text.Json.JsonSerializer.Serialize(result), ContentType = "application/json", StatusCode = 200 };
    }

    private void LogInfo(string text) => Log(LogLevel.Information, text);
    private void LogWarning(string text) => Log(LogLevel.Warning, text);
    private void Log(LogLevel level, string text)
    {
        string logMsg = $"[{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")}] [{_ipService.ClientIP}]";
        if (_proxyService.TrustedProxies.Length > 0 && !_ipService.TrustedProxy) logMsg += "*";
        logMsg += $" {text}";
        _logger?.Log(level, logMsg);
    }
}

public class UserProfileUpdateRequest
{
    public string? preferences { get; set; }
    public string? metadata { get; set; }
}
