using Payment.API.Entities;

namespace Payment.API.Interfaces
{
    public interface IPaymentRepository
    {
        // Карты пользователя
        Task<IEnumerable<UserCard>> GetUserCardsAsync(Guid userId);
        Task<UserCard> GetCardAsync(Guid cardId, Guid userId);
        Task<UserCard> AddCardAsync(UserCard card);
        Task<bool> DeleteCardAsync(Guid cardId, Guid userId);

        // Транзакции
        Task<Transaction> GetTransactionAsync(Guid transactionId);
        Task<Transaction> GetTransactionByOrderIdAsync(Guid orderId);
        Task<Transaction> CreateTransactionAsync(Transaction transaction);
        Task UpdateTransactionStatusAsync(Guid transactionId, string status, string providerId = null, string failureReason = null);
        Task<IEnumerable<Transaction>> GetUserTransactionsAsync(Guid userId);

        // Утилиты
        Task<bool> SaveChangesAsync();
    }
}