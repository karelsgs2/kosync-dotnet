
using Microsoft.AspNetCore.Mvc;
using Kosync.Database.Entities;

namespace Kosync.Controllers;

[ApiController]
public class SyncController : ControllerBase
{
    private readonly ILogger<SyncController> _logger;
    private readonly ProxyService _proxyService;
    private readonly IPService _ipService;
    private readonly KosyncDb _db;
    private readonly UserService _userService;

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
            username = user?.Username,
            preferences = user?.PreferencesJson,
            metadata = user?.MetadataJson
        });
    }

    [HttpPut("/users/profile")]
    public ObjectResult UpdateUserProfile([FromBody] UserProfileUpdateRequest payload)
    {
        if (!_userService.IsAuthenticated) return StatusCode(401, new { message = "Unauthorized" });

        var userCollection = _db.Context.GetCollection<DbUser>("users");
        var user = userCollection.FindOne(i => i.Username == _userService.Username);
        if (user == null) return StatusCode(404, new { message = "User not found" });

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
            
            if (user is null) return StatusCode(404, new { message = "User not found in database" });

            user.PasswordHash = Utility.HashPassword(payload.password);
            bool updated = userCollection.Update(user);

            if (!updated) {
                return StatusCode(500, new { message = "Database failed to update user record" });
            }

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
        if (!_userService.IsAuthenticated) return StatusCode(401, new { message = "Invalid credentials" });
        if (!_userService.IsActive) return StatusCode(401, new { message = "User is inactive" });

        LogInfo($"User [{_userService.Username}] authorized.");
        return StatusCode(200, new { username = _userService.Username });
    }

    [HttpPost("/users/create")]
    public ObjectResult CreateUser([FromBody] UserCreateRequest payload)
    {
        try {
            var settingsCollection = _db.Context.GetCollection<SystemSetting>("system_settings");
            var regSetting = settingsCollection.FindOne(s => s.Key == "RegistrationDisabled");
            
            bool registrationDisabled = regSetting?.Value != null && 
                                    regSetting.Value.Equals("true", StringComparison.OrdinalIgnoreCase);

            if (registrationDisabled) return StatusCode(402, new { message = "User registration is disabled" });

            var userCollection = _db.Context.GetCollection<DbUser>("users");
            var existing = userCollection.FindOne(u => u.Username == payload.username);
            if (existing is not null) return StatusCode(402, new { message = "User already exists" });

            var user = new DbUser() { Username = payload.username, PasswordHash = Utility.HashPassword(payload.password) };
            userCollection.Insert(user);
            userCollection.EnsureIndex(u => u.Username);

            LogInfo($"User [{payload.username}] created.");
            return StatusCode(201, new { username = payload.username });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error during user creation", detail = ex.Message });
        }
    }

    [HttpPut("/syncs/progress")]
    public ObjectResult SyncProgress([FromBody] DocumentRequest payload)
    {
        try {
            if (!_userService.IsAuthenticated || !_userService.IsActive) return StatusCode(401, new { message = "Unauthorized" });

            var userCollection = _db.Context.GetCollection<DbUser>("users");
            var user = userCollection.FindOne(i => i.Username == _userService.Username);
            if (user == null) return StatusCode(404, new { message = "User not found" });

            var document = user.Documents.FirstOrDefault(i => i.DocumentHash == payload.document);
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
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error during sync", detail = ex.Message });
        }
    }

    [HttpGet("/syncs/progress/{documentHash}")]
    public IActionResult GetProgress(string documentHash)
    {
        if (!_userService.IsAuthenticated || !_userService.IsActive) return StatusCode(401, new { message = "Unauthorized" });

        var userCollection = _db.Context.GetCollection<DbUser>("users");
        var user = userCollection.FindOne(i => i.Username == _userService.Username);
        
        var document = user?.Documents?.FirstOrDefault(i => i.DocumentHash == documentHash);
        if (document is null) return StatusCode(502, new { message = "Document not found" });

        var time = new DateTimeOffset(document.Timestamp);
        return Ok(new { 
            device = document.Device, 
            device_id = document.DeviceId, 
            document = document.DocumentHash, 
            percentage = document.Percentage, 
            progress = document.Progress, 
            timestamp = time.ToUnixTimeSeconds() 
        });
    }

    private void LogInfo(string text) => Log(LogLevel.Information, text);
    private void Log(LogLevel level, string text)
    {
        string clientIp = _ipService?.ClientIP ?? "unknown";
        string logMsg = $"[{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")}] [{clientIp}] {text}";
        _logger?.Log(level, logMsg);
    }
}

public class UserProfileUpdateRequest
{
    public string? preferences { get; set; }
    public string? metadata { get; set; }
}
