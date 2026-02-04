
namespace Kosync.Database.Entities;

public class SystemSetting
{
    [BsonId]
    public int Id { get; set; }
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
}
