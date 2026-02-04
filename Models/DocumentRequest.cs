
namespace Kosync.Models;

public class DocumentRequest
{
    public string document { get; set; } = default!;

    public string progress { get; set; } = default!;

    public double percentage { get; set; }

    public string device { get; set; } = default!;

    public string device_id { get; set; } = default!;
}
