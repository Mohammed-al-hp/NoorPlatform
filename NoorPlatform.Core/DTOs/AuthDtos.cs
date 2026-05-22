namespace NoorPlatform.Core.DTOs;

public record LoginDto(string Phone, string Password);
public record RegisterDto(string FullName, string Phone, string Password, string Role);
public record AuthResponse(string Token, string FullName, string Role);
