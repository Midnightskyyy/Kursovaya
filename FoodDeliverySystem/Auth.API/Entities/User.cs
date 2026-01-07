using Shared.Core.Models;

namespace Auth.API.Entities
{
    public class User : BaseEntity
    {
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string PhoneNumber { get; set; }
        public string Role { get; set; } = "Customer";
        public bool IsActive { get; set; } = true;

        // Navigation property
        public virtual UserProfile Profile { get; set; }
        public virtual ICollection<RefreshToken> RefreshTokens { get; set; }
    }

    public class UserProfile : BaseEntity
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string AvatarUrl { get; set; }

        public virtual User User { get; set; }
    }

    public class RefreshToken : BaseEntity
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string TokenHash { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual User User { get; set; }
    }
}
