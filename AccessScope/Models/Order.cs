namespace AccessScope.Models;

public class Order : IOwnedResource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Details { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
