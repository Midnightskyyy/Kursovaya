using CreditCardValidator;
using Payment.API.Entities;
using Payment.API.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace Payment.API.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(IPaymentRepository paymentRepository, ILogger<PaymentService> logger)
        {
            _paymentRepository = paymentRepository;
            _logger = logger;
        }

        public async Task<UserCard> AddCardAsync(Guid userId, string cardNumber, string cardHolderName,
            int expiryMonth, int expiryYear, string cvv)
        {
            // Валидация карты
            if (!ValidateCardAsync(cardNumber, cardHolderName, expiryMonth, expiryYear, cvv).Result)
                throw new ArgumentException("Invalid card details");

            // Маскируем номер карты (оставляем последние 4 цифры)
            var lastFourDigits = cardNumber.Length >= 4 ? cardNumber.Substring(cardNumber.Length - 4) : cardNumber;

            // Генерируем токен (симуляция токена платежной системы)
            var tokenHash = GenerateCardToken(cardNumber, expiryMonth, expiryYear, cvv);

            var card = new UserCard
            {
                UserId = userId,
                CardLastFourDigits = lastFourDigits,
                CardHolderName = cardHolderName,
                ExpiryMonth = expiryMonth,
                ExpiryYear = expiryYear,
                TokenHash = tokenHash
            };

            return await _paymentRepository.AddCardAsync(card);
        }

        public async Task<bool> ValidateCardAsync(string cardNumber, string cardHolderName,
    int expiryMonth, int expiryYear, string cvv)
        {
            try
            {
                // УПРОЩЕННАЯ ВАЛИДАЦИЯ ДЛЯ ТЕСТОВОГО ПРИЛОЖЕНИЯ
                // Просто проверяем, что есть какие-то данные

                if (string.IsNullOrWhiteSpace(cardNumber) || cardNumber.Length < 13)
                {
                    _logger.LogWarning("Invalid card number length: {Length}", cardNumber.Length);
                    return false;
                }

                // Проверяем срок действия
                var currentDate = DateTime.UtcNow;
                if (expiryYear < currentDate.Year ||
                    (expiryYear == currentDate.Year && expiryMonth < currentDate.Month))
                {
                    _logger.LogWarning("Card expired: {Month}/{Year}", expiryMonth, expiryYear);
                    return false;
                }

                // Проверяем имя держателя
                if (string.IsNullOrWhiteSpace(cardHolderName) || cardHolderName.Length < 2)
                {
                    _logger.LogWarning("Invalid card holder name: {Name}", cardHolderName);
                    return false;
                }

                return true; // Всегда true для демо, можно добавить простые проверки
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating card");
                return false;
            }
        }

        public async Task<Transaction> ProcessPaymentAsync(Guid orderId, Guid userId, decimal amount,
    Guid? cardId = null, string cardToken = null)
        {
            try
            {
                _logger.LogInformation("Starting payment processing for order {OrderId}, user {UserId}", orderId, userId);

                // Проверяем, нет ли уже транзакции для этого заказа
                var existingTransaction = await _paymentRepository.GetTransactionByOrderIdAsync(orderId);
                if (existingTransaction != null)
                {
                    _logger.LogInformation("Transaction already exists for order {OrderId}: {TransactionId}",
                        orderId, existingTransaction.Id);
                    return existingTransaction;
                }

                // Создаем транзакцию
                var transaction = new Transaction
                {
                    OrderId = orderId,
                    UserId = userId,
                    Amount = amount,
                    Status = "Pending",
                    CardId = cardId,
                    CreatedAt = DateTime.UtcNow
                };

                _logger.LogInformation("Created transaction object: {@Transaction}", transaction);

                // Сохраняем транзакцию
                var savedTransaction = await _paymentRepository.CreateTransactionAsync(transaction);
                _logger.LogInformation("Transaction saved to database: {TransactionId}", savedTransaction.Id);

                // Симуляция обработки платежа
                await SimulatePaymentProcessingAsync(savedTransaction);

                return savedTransaction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment for order {OrderId}", orderId);
                throw;
            }
        }

        public async Task<Transaction> SimulatePaymentAsync(Guid orderId, Guid userId, decimal amount)
        {
            // Простая симуляция успешного платежа (всегда успешно для тестов)
            var transaction = new Transaction
            {
                OrderId = orderId,
                UserId = userId,
                Amount = amount,
                Status = "Success",
                ProviderTransactionId = $"SIM_{Guid.NewGuid().ToString("N").Substring(0, 8)}",
                ProcessedAt = DateTime.UtcNow
            };

            return await _paymentRepository.CreateTransactionAsync(transaction);
        }

        public async Task<bool> RefundPaymentAsync(Guid transactionId)
        {
            var transaction = await _paymentRepository.GetTransactionAsync(transactionId);
            if (transaction == null || transaction.Status != "Success")
                return false;

            // Симуляция возврата
            transaction.Status = "Refunded";
            transaction.UpdatedAt = DateTime.UtcNow;
            await _paymentRepository.SaveChangesAsync();

            _logger.LogInformation("Payment refunded: {TransactionId}", transactionId);
            return true;
        }

        private async Task SimulatePaymentProcessingAsync(Transaction transaction)
        {
            // Симуляция задержки платежной системы
            await Task.Delay(1000);

            // В 90% случаев платеж успешен, в 10% - неудачен (для тестирования)
            var random = new Random();
            bool isSuccess = random.Next(100) < 90;

            if (isSuccess)
            {
                await _paymentRepository.UpdateTransactionStatusAsync(
                    transaction.Id,
                    "Success",
                    $"PAY_{Guid.NewGuid().ToString("N").Substring(0, 8)}"
                );
                _logger.LogInformation("Payment successful for order {OrderId}", transaction.OrderId);
            }
            else
            {
                await _paymentRepository.UpdateTransactionStatusAsync(
                    transaction.Id,
                    "Failed",
                    failureReason: "Simulated payment failure"
                );
                _logger.LogWarning("Payment failed for order {OrderId}", transaction.OrderId);
            }
        }

        private string GenerateCardToken(string cardNumber, int expiryMonth, int expiryYear, string cvv)
        {
            // Простая симуляция генерации токена
            var data = $"{cardNumber}{expiryMonth}{expiryYear}{cvv}";
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hash);
        }
    }
}