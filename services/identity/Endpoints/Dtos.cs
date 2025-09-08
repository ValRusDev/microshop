namespace MicroShop.Services.Identity.Endpoints;

public record RegisterDto(string Email, string Password);
public record LoginDto(string Email, string Password);
