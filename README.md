# AccessScope

Solves **Broken Object-Level Authorization (BOLA/IDOR)** — OWASP's #1 API
risk. The bug: an endpoint checks that you're *logged in*, but never checks
that the specific record you asked for actually belongs to *you*. Change the
id in the URL, get someone else's data.

## The actual problem this repo targets

It's rarely one missing check — it's the same ownership check
(`if (resource.OwnerId != currentUserId) return Forbid();`) copy-pasted into
every controller, which inevitably gets forgotten on one of them. That
forgotten instance is the breach.

## The fix: one generic, reusable authorization handler

```csharp
public interface IOwnedResource
{
    Guid OwnerId { get; }
}

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
```

Any model implements `IOwnedResource`, gets one line of DI registration
(`AddSingleton<IAuthorizationHandler, ResourceOwnerHandler<Order>>()`), and
every endpoint touching it is protected the same way — no new `if` checks
per controller. `Order` and `Note` are both included specifically to prove
this generalizes across unrelated resource types.

## The other deliberate design choice: 404, not 403

```csharp
var order = await _db.Orders.FindAsync(id);
if (order is null) return NotFound();

var authResult = await _authorizationService.AuthorizeAsync(User, order, new ResourceOwnerRequirement());
if (!authResult.Succeeded) return NotFound(); // NOT Forbid()
```

A resource that exists but isn't yours, and a resource that doesn't exist at
all, return the **identical** response. A `403` would quietly confirm to an
attacker that the id is real, even though they can't see it — which is
exactly the kind of leak that lets someone map out valid ids at scale. A
uniform `404` gives nothing away either way.

## Project layout

```
AccessScope/
├── Program.cs                              # DI, JWT auth, generic handler registration
├── Controllers/
│   ├── AuthController.cs                   # register, login
│   ├── OrdersController.cs                 # first protected resource
│   └── NotesController.cs                  # second resource — proves generalization
├── Authorization/
│   └── ResourceOwnerAuthorization.cs       # ResourceOwnerRequirement + ResourceOwnerHandler<T>
├── Models/
│   ├── IOwnedResource.cs
│   ├── User.cs / Order.cs / Note.cs
│   └── Dtos.cs
├── Services/
│   ├── TokenService.cs
│   └── AuthService.cs
├── Data/
│   └── AppDbContext.cs
└── Tests/
    ├── AccessScopeFactory.cs               # isolated SQLite DB per test run
    └── IdorAttackTests.cs                  # the attack-simulation suite
```

## Running locally

```bash
dotnet restore
dotnet run
```

SQLite by default (`accessscope.db`, auto-created).

## Try the attack yourself

```bash
# Two users
curl -X POST localhost:5000/auth/register -d '{"email":"alice@x.com","password":"Passw0rd!"}' -H "Content-Type: application/json"
curl -X POST localhost:5000/auth/register -d '{"email":"bob@x.com","password":"Passw0rd!"}' -H "Content-Type: application/json"

ALICE=$(curl -s -X POST localhost:5000/auth/login -d '{"email":"alice@x.com","password":"Passw0rd!"}' -H "Content-Type: application/json" | jq -r .accessToken)
BOB=$(curl -s -X POST localhost:5000/auth/login -d '{"email":"bob@x.com","password":"Passw0rd!"}' -H "Content-Type: application/json" | jq -r .accessToken)

# Alice creates an order
ORDER_ID=$(curl -s -X POST localhost:5000/orders -H "Authorization: Bearer $ALICE" -H "Content-Type: application/json" -d '{"details":"secret","amount":99}' | jq -r .id)

curl -i localhost:5000/orders/$ORDER_ID -H "Authorization: Bearer $ALICE"   # 200 — owner
curl -i localhost:5000/orders/$ORDER_ID -H "Authorization: Bearer $BOB"     # 404 — attacker, blocked
curl -i localhost:5000/orders/$(uuidgen) -H "Authorization: Bearer $BOB"    # 404 — SAME status as above
```
