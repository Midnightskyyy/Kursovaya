using Payment.API.Entities;

namespace Payment.API.Interfaces
{
    public interface IPaymentService
    {
        Task<UserCard> AddCardAsync(Guid userId, string cardNumber, string cardHolderName, int expiryMonth, int expiryYear, string cvv);
        Task<bool> ValidateCardAsync(string cardNumber, string cardHolderName, int expiryMonth, int expiryYear, string cvv);
        Task<Transaction> ProcessPaymentAsync(Guid orderId, Guid userId, decimal amount, Guid? cardId = null, string cardToken = null);
        Task<Transaction> SimulatePaymentAsync(Guid orderId, Guid userId, decimal amount);
        Task<bool> RefundPaymentAsync(Guid transactionId);
    }
}