using FluentValidation;
using MediatR;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Common.Models;
using Surelance.Domain.Entities;
using Surelance.Domain.Enums;

namespace Surelance.Application.Features.Auth;

public record RegisterCommand(
    string Email,
    string Password,
    string FullName,
    UserRole Role) : IRequest<Result<AuthResponseDto>>;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(100).WithMessage("Full name cannot exceed 100 characters.");

        RuleFor(x => x.Role)
            .Must(r => r is UserRole.Client or UserRole.Freelancer)
            .WithMessage("You can only register as a Client or a Freelancer. Arbitrators are provisioned by the platform.");
    }
}

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthResponseDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AuthResponseDto>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await _userRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken))
        {
            return Result<AuthResponseDto>.Conflict($"User with email '{request.Email}' already exists.");
        }

        var passwordHash = _passwordHasher.HashPassword(request.Password);
        var user = new User(
            id: Guid.NewGuid(),
            email: normalizedEmail,
            passwordHash: passwordHash,
            fullName: request.FullName,
            role: request.Role,
            createdAtUtc: _dateTimeProvider.UtcNow);

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var token = _tokenGenerator.GenerateToken(user);
        return Result<AuthResponseDto>.Success(new AuthResponseDto(user.Id, user.Email, user.FullName, user.Role, token));
    }
}
