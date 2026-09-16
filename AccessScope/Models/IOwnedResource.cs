namespace AccessScope.Models;


public interface IOwnedResource
{
    Guid OwnerId { get; }
}
