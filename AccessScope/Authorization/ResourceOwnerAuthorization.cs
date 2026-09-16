using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using AccessScope.Models;

namespace AccessScope.Authorization;

public class ResourceOwnerRequirement : IAuthorizationRequirement { }


public class ResourceOwnerHandler<TResource> : AuthorizationHandler<ResourceOwnerRequirement, TResource>
    where TResource : IOwnedResource
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ResourceOwnerRequirement requirement,
        TResource resource)
    {
        var subClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (subClaim is not null &&
            Guid.TryParse(subClaim, out var userId) &&
            (resource.OwnerId == userId || context.User.IsInRole("Admin")))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
