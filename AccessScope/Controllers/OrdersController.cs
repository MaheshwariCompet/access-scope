using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccessScope.Authorization;
using AccessScope.Data;
using AccessScope.Models;

namespace AccessScope.Controllers;

[ApiController]
[Route("orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuthorizationService _authorizationService;

    public OrdersController(AppDbContext db, IAuthorizationService authorizationService)
    {
        _db = db;
        _authorizationService = authorizationService;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderRequest request)
    {
        var order = new Order
        {
            OwnerId = CurrentUserId,
            Details = request.Details,
            Amount = request.Amount
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = order.Id },
            new OrderResponse(order.Id, order.Details, order.Amount));
    }

    /// <summary>Lists only the caller's own orders — never a cross-user listing endpoint.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderResponse>>> GetMine()
    {
        var orders = await _db.Orders
            .Where(o => o.OwnerId == CurrentUserId)
            .Select(o => new OrderResponse(o.Id, o.Details, o.Amount))
            .ToListAsync();
        return Ok(orders);
    }

    /// <summary>
    /// The classic IDOR target: fetch-by-id. Deliberately returns 404 — not 403 —
    /// for a resource that exists but isn't yours, so the response is identical
    /// to "this id doesn't exist." A 403 would confirm the record's existence to
    /// an attacker probing ids; a uniform 404 leaks nothing either way.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> GetById(Guid id)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order is null) return NotFound();

        var authResult = await _authorizationService.AuthorizeAsync(User, order, new ResourceOwnerRequirement());
        if (!authResult.Succeeded) return NotFound(); // see note above: NotFound, not Forbid

        return Ok(new OrderResponse(order.Id, order.Details, order.Amount));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order is null) return NotFound();

        var authResult = await _authorizationService.AuthorizeAsync(User, order, new ResourceOwnerRequirement());
        if (!authResult.Succeeded) return NotFound();

        _db.Orders.Remove(order);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
