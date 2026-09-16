namespace AccessScope.Models;


public record RegisterRequest(string Email, string Password, string Role = "User");
public record LoginRequest(string Email, string Password);
public record LoginResponse(string AccessToken);

public record CreateOrderRequest(string Details, decimal Amount);
public record OrderResponse(Guid Id, string Details, decimal Amount);

public record CreateNoteRequest(string Body);
public record NoteResponse(Guid Id, string Body);
