namespace Bff.Service.Models;

public class EntityUpdateDto
{
    public required string EntityType { get; set; }
    public required string ChangeType { get; set; }
    public object? Data { get; set; }
}
