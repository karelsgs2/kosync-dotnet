
namespace Kosync.Database.Entities;

public class DbUser
{
    [BsonId]
    public int Id { get; set; }

    public string Username { get; set; } = default!;

    public string PasswordHash { get; set; } = default!;

    public bool IsActive { get; set; } = true;

    public bool IsAdministrator { get; set; } = false;

    public string? PreferencesJson { get; set; }

    public string? MetadataJson { get; set; }

    private List<Document> _documents = new();
    public List<Document> Documents 
    { 
        get => _documents ??= new(); 
        set => _documents = value; 
    }
}
