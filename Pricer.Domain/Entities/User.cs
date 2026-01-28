namespace Pricer.Domain.Entities;

public sealed class User
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public int Reputation { get; set; }
    public DateTime CreatedAt { get; set; }
}
