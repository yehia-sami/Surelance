using Surelance.Domain.Enums;

namespace Surelance.Application.Features.Auth;

public record AuthResponseDto(
    Guid UserId,
    string Email,
    string FullName,
    UserRole Role,
    string Token);
