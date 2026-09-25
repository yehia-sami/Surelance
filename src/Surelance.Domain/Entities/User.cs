using Surelance.Domain.Common;
using Surelance.Domain.Enums;

namespace Surelance.Domain.Entities;

public class User : BaseEntity
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public ICollection<Contract> ClientContracts { get; private set; } = new List<Contract>();
    public ICollection<Contract> FreelancerContracts { get; private set; } = new List<Contract>();

    private User() { } // EF Core

    public User(Guid id, string email, string passwordHash, string fullName, UserRole role, DateTime createdAtUtc)
    {
        Id = id;
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        FullName = fullName.Trim();
        Role = role;
        CreatedAtUtc = createdAtUtc;
    }
}
