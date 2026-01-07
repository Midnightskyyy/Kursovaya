using Shared.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace Payment.API.Entities
{
    public class UserCard : BaseEntity
    {
        public Guid UserId { get; set; }

        [Required, MaxLength(4)]
        public string CardLastFourDigits { get; set; }

        [Required, MaxLength(255)]
        public string CardHolderName { get; set; }

        [Range(1, 12)]
        public int ExpiryMonth { get; set; }

        [Range(2023, 2030)]
        public int ExpiryYear { get; set; }

        [Required]
        public string TokenHash { get; set; } // Симулируем токен платежной системы

        public bool IsActive { get; set; } = true;

        public virtual ICollection<Transaction> Transactions { get; set; }
    }

    public class Transaction : BaseEntity
    {
        public Guid OrderId { get; set; }
        public Guid UserId { get; set; }
        public Guid? CardId { get; set; }

        [Required]
        public string Status { get; set; } = "Pending"; // Pending, Success, Failed

        [Range(0.01, 100000)]
        public decimal Amount { get; set; }

        [MaxLength(255)]
        public string ProviderTransactionId { get; set; } // Симуляция ID от платежной системы

        [MaxLength(500)]
        public string FailureReason { get; set; }

        public DateTime? ProcessedAt { get; set; }

        public virtual UserCard Card { get; set; }
    }
}