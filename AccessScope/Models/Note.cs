namespace AccessScope.Models;


public class Note : IOwnedResource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Body { get; set; } = string.Empty;
}
