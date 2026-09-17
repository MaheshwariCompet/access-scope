using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccessScope.Authorization;
using AccessScope.Data;
using AccessScope.Models;

namespace AccessScope.Controllers;

/// <summary>
/// A second, unrelated resource type. Deliberately included to demonstrate
/// that ResourceOwnerHandler&lt;T&gt; generalizes: Note gets the exact same
/// ownership + admin-override protection as Order, with no new authorization
/// logic written here at all — only IOwnedResource on the model (see Note.cs).
/// </summary>
[ApiController]
[Route("notes")]
[Authorize]
public class NotesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuthorizationService _authorizationService;

    public NotesController(AppDbContext db, IAuthorizationService authorizationService)
    {
        _db = db;
        _authorizationService = authorizationService;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    [HttpPost]
    public async Task<ActionResult<NoteResponse>> Create(CreateNoteRequest request)
    {
        var note = new Note { OwnerId = CurrentUserId, Body = request.Body };
        _db.Notes.Add(note);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = note.Id }, new NoteResponse(note.Id, note.Body));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NoteResponse>> GetById(Guid id)
    {
        var note = await _db.Notes.FindAsync(id);
        if (note is null) return NotFound();

        var authResult = await _authorizationService.AuthorizeAsync(User, note, new ResourceOwnerRequirement());
        if (!authResult.Succeeded) return NotFound();

        return Ok(new NoteResponse(note.Id, note.Body));
    }
}
