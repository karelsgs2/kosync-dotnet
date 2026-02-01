
namespace Kosync.Database.Entities;

public class SystemSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
}
