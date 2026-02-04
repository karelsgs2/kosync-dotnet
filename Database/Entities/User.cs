
namespace Kosync.Database.Entities;

public class DbUser
{
    [BsonId]
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool IsAdministrator { get; set; } = false;

    public string? PreferencesJson { get; set; }

    public string? MetadataJson { get; set; }

    public List<Document> Documents { get; set; } = new();
}
