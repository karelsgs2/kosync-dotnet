
namespace Kosync.Database.Entities;

public class Document
{
    public string DocumentHash { get; set; } = string.Empty;

    public string Progress { get; set; } = string.Empty;

    public double Percentage { get; set; }

    public string Device { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }
}
