using System.ComponentModel.DataAnnotations;

namespace TestProject.Domain;

public class Tag
{
    public Guid Id { get; set; }

    [MaxLength(64)] public required string Name { get; set; }

    [MaxLength(7)] public string? Color { get; set; }

    public ICollection<OrderTag> OrderTags { get; set; } = new List<OrderTag>();
}