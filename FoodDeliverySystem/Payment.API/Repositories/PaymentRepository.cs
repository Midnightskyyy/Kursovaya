using Microsoft.EntityFrameworkCore;
using Payment.API.Data;
using Payment.API.Entities;
using Payment.API.Interfaces;

namespace Payment.API.Repositories
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly PaymentDbContext _context;

        public PaymentRepository(PaymentDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<UserCard>> GetUserCardsAsync(Guid userId)
        {
            return await _context.UserCards
                .Where(c => c.UserId == userId && c.IsActive)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<UserCard> GetCardAsync(Guid cardId, Guid userId)
        {
            return await _context.UserCards
                .FirstOrDefaultAsync(c => c.Id == cardId && c.UserId == userId && c.IsActive);
        }

        public async Task<UserCard> AddCardAsync(UserCard card)
        {
            await _context.UserCards.AddAsync(card);
            await _context.SaveChangesAsync();
            return card;
        }

        public async Task<bool> DeleteCardAsync(Guid cardId, Guid userId)
        {
            var card = await GetCardAsync(cardId, userId);
            if (card == null) return false;

            card.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<Transaction> GetTransactionAsync(Guid transactionId)
        {
            return await _context.Transactions
                .Include(t => t.Card)
                .FirstOrDefaultAsync(t => t.Id == transactionId);
        }

        public async Task<Transaction> GetTransactionByOrderIdAsync(Guid orderId)
        {
            return await _context.Transactions
                .Include(t => t.Card)
                .FirstOrDefaultAsync(t => t.OrderId == orderId);
        }

        public async Task<Transaction> CreateTransactionAsync(Transaction transaction)
        {
            await _context.Transactions.AddAsync(transaction);
            await _context.SaveChangesAsync();
            return transaction;
        }

        public async Task UpdateTransactionStatusAsync(Guid transactionId, string status, string providerId = null, string failureReason = null)
        {
            var transaction = await _context.Transactions.FindAsync(transactionId);
            if (transaction != null)
            {
                transaction.Status = status;
                transaction.ProviderTransactionId = providerId;
                transaction.FailureReason = failureReason;
                transaction.ProcessedAt = DateTime.UtcNow;
                transaction.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<Transaction>> GetUserTransactionsAsync(Guid userId)
        {
            return await _context.Transactions
                .Include(t => t.Card)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }
    }
}