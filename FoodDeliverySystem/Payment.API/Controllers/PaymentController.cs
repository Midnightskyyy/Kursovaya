using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payment.API.Entities;
using Payment.API.Interfaces;
using Shared.Core.Models;
using Shared.Messages.Events;
using Shared.Messages.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;

namespace Payment.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IPaymentService _paymentService;
        private readonly IMessageBusClient _messageBus;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IPaymentRepository paymentRepository,
            IPaymentService paymentService,
            IMessageBusClient messageBus,
            ILogger<PaymentController> logger)
        {
            _paymentRepository = paymentRepository;
            _paymentService = paymentService;
            _messageBus = messageBus;
            _logger = logger;
        }

        [HttpGet("cards")]
        public async Task<IActionResult> GetCards()
        {
            try
            {
                var userId = GetUserId();
                var cards = await _paymentRepository.GetUserCardsAsync(userId);

                // Маппинг для безопасного возврата данных
                var safeCards = cards.Select(c => new
                {
                    c.Id,
                    c.CardLastFourDigits,
                    c.CardHolderName,
                    Expiry = $"{c.ExpiryMonth:D2}/{c.ExpiryYear}",
                    c.IsActive,
                    c.CreatedAt
                }).ToList();

                return Ok(ApiResponse.Ok(safeCards));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cards");
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpGet("cards/default")]
        public async Task<IActionResult> GetDefaultCard()
        {
            try
            {
                var userId = GetUserId();
                var cards = await _paymentRepository.GetUserCardsAsync(userId);
                var defaultCard = cards.FirstOrDefault(c => c.IsActive);

                if (defaultCard == null)
                    return Ok(ApiResponse.Ok(null, "No default card"));

                return Ok(ApiResponse.Ok(new
                {
                    defaultCard.Id,
                    defaultCard.CardLastFourDigits,
                    defaultCard.CardHolderName,
                    Expiry = $"{defaultCard.ExpiryMonth:D2}/{defaultCard.ExpiryYear}"
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting default card");
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpPost("cards/{cardId}/set-default")]
        public async Task<IActionResult> SetDefaultCard(Guid cardId)
        {
            try
            {
                var userId = GetUserId();

                // Получаем все карты пользователя
                var cards = await _paymentRepository.GetUserCardsAsync(userId);

                // Снимаем флаг default со всех карт
                foreach (var card in cards)
                {
                    if (card.Id == cardId)
                    {
                        card.IsActive = true;
                    }
                    else
                    {
                        card.IsActive = false;
                    }
                }

                await _paymentRepository.SaveChangesAsync();

                return Ok(ApiResponse.Ok(null, "Default card updated"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default card");
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpPost("pay/with-card")]
        public async Task<IActionResult> PayWithSavedCard([FromBody] PayWithCardRequest request)
        {
            try
            {
                _logger.LogInformation("Processing payment with saved card for order {OrderId}", request.OrderId);

                var userId = GetUserId();

                // Проверяем, что карта принадлежит пользователю
                var card = await _paymentRepository.GetCardAsync(request.CardId, userId);
                if (card == null)
                {
                    return NotFound(ApiResponse.Fail("Card not found"));
                }

                // Процессим платеж
                var transaction = await _paymentService.ProcessPaymentAsync(
                    request.OrderId,
                    userId,
                    request.Amount,
                    request.CardId);

                // Публикация события
                if (transaction.Status == "Success")
                {
                    _messageBus.Publish(new PaymentConfirmedEvent
                    {
                        OrderId = transaction.OrderId,
                        TransactionId = transaction.Id,
                        Amount = transaction.Amount,
                        PaidAt = transaction.ProcessedAt ?? DateTime.UtcNow
                    }, "payment.confirmed");
                }

                return Ok(ApiResponse.Ok(new
                {
                    transaction.Id,
                    transaction.Status,
                    transaction.Amount,
                    TransactionDate = transaction.ProcessedAt,
                    CardUsed = new
                    {
                        card.CardLastFourDigits,
                        card.CardHolderName
                    }
                }, $"Payment {transaction.Status.ToLower()}"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment with saved card");
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpPost("cards")]
        public async Task<IActionResult> AddCard([FromBody] AddCardRequest request)
        {
            try
            {
                var userId = GetUserId();

                // ПРОСТАЯ ВАЛИДАЦИЯ ДЛЯ ТЕСТОВОГО ПРИЛОЖЕНИЯ
                if (string.IsNullOrWhiteSpace(request.CardNumber) ||
                    string.IsNullOrWhiteSpace(request.CardHolderName) ||
                    request.ExpiryMonth < 1 || request.ExpiryMonth > 12 ||
                    request.ExpiryYear < 2023 || request.ExpiryYear > 2030)
                {
                    return BadRequest(ApiResponse.Fail("Invalid card data"));
                }

                // Берем последние 4 цифры для отображения
                string lastFourDigits = request.CardNumber.Length >= 4
                    ? request.CardNumber.Substring(request.CardNumber.Length - 4)
                    : request.CardNumber;

                // ПРОСТАЯ ТОКЕНИЗАЦИЯ ДЛЯ ДЕМО (в реальном приложении использовать безопасное шифрование)
                string tokenHash = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{request.CardNumber}|{request.ExpiryMonth}|{request.ExpiryYear}|{userId}")
                );

                var card = new UserCard
                {
                    UserId = userId,
                    CardLastFourDigits = lastFourDigits,
                    CardHolderName = request.CardHolderName,
                    ExpiryMonth = request.ExpiryMonth,
                    ExpiryYear = request.ExpiryYear,
                    TokenHash = tokenHash
                };

                var savedCard = await _paymentRepository.AddCardAsync(card);

                return Ok(ApiResponse.Ok(new
                {
                    savedCard.Id,
                    savedCard.CardLastFourDigits,
                    savedCard.CardHolderName,
                    savedCard.ExpiryMonth,
                    savedCard.ExpiryYear
                }, "Card added successfully"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding card");
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpDelete("cards/{cardId}")]
        public async Task<IActionResult> DeleteCard(Guid cardId)
        {
            try
            {
                var userId = GetUserId();
                var success = await _paymentRepository.DeleteCardAsync(cardId, userId);

                if (!success)
                    return NotFound(ApiResponse.Fail("Card not found"));

                return Ok(ApiResponse.Ok(null, "Card deleted successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting card");
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpPost("pay")]
        public async Task<IActionResult> Pay([FromBody] PayRequest request)
        {
            try
            {
                _logger.LogInformation("Processing payment for order {OrderId}, amount {Amount}, cardId {CardId}",
                    request.OrderId, request.Amount, request.CardId);

                var userId = GetUserId();
                _logger.LogInformation("User ID from token: {UserId}", userId);

                // Проверяем наличие карты, если указана
                if (request.CardId.HasValue)
                {
                    var card = await _paymentRepository.GetCardAsync(request.CardId.Value, userId);
                    if (card == null)
                    {
                        _logger.LogWarning("Card {CardId} not found for user {UserId}", request.CardId, userId);
                        return BadRequest(ApiResponse.Fail("Payment card not found"));
                    }
                }

                var transaction = await _paymentService.ProcessPaymentAsync(
                    request.OrderId,
                    userId,
                    request.Amount,
                    request.CardId);

                _logger.LogInformation("Transaction created: {TransactionId}, status: {Status}",
                    transaction.Id, transaction.Status);

                // Публикация события в зависимости от результата
                if (transaction.Status == "Success")
                {
                    _messageBus.Publish(new PaymentConfirmedEvent
                    {
                        OrderId = transaction.OrderId,
                        TransactionId = transaction.Id,
                        Amount = transaction.Amount,
                        PaidAt = transaction.ProcessedAt ?? DateTime.UtcNow
                    }, "payment.confirmed");

                    _logger.LogInformation("Payment confirmed for order {OrderId}", request.OrderId);
                }
                else if (transaction.Status == "Failed")
                {
                    _messageBus.Publish(new PaymentFailedEvent
                    {
                        OrderId = transaction.OrderId,
                        Reason = transaction.FailureReason,
                        FailedAt = transaction.ProcessedAt ?? DateTime.UtcNow
                    }, "payment.failed");

                    _logger.LogWarning("Payment failed for order {OrderId}", request.OrderId);
                }

                return Ok(ApiResponse.Ok(new
                {
                    transaction.Id,
                    transaction.Status,
                    transaction.Amount,
                    TransactionDate = transaction.ProcessedAt
                }, $"Payment {transaction.Status.ToLower()}"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment for order {OrderId}", request.OrderId);
                return StatusCode(500, ApiResponse.Fail($"Internal server error: {ex.Message}"));
            }
        }

        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions()
        {
            try
            {
                var userId = GetUserId();
                var transactions = await _paymentRepository.GetUserTransactionsAsync(userId);
                return Ok(ApiResponse.Ok(transactions));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transactions");
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpGet("transactions/{transactionId}")]
        public async Task<IActionResult> GetTransaction(Guid transactionId)
        {
            try
            {
                var userId = GetUserId();
                var transaction = await _paymentRepository.GetTransactionAsync(transactionId);

                if (transaction == null || transaction.UserId != userId)
                    return NotFound(ApiResponse.Fail("Transaction not found"));

                return Ok(ApiResponse.Ok(transaction));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transaction {TransactionId}", transactionId);
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpPost("refund/{transactionId}")]
        public async Task<IActionResult> Refund(Guid transactionId)
        {
            try
            {
                var success = await _paymentService.RefundPaymentAsync(transactionId);

                if (!success)
                    return BadRequest(ApiResponse.Fail("Refund not possible"));

                return Ok(ApiResponse.Ok(null, "Refund processed successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing refund for {TransactionId}", transactionId);
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpGet("order/{orderId}")]
        public async Task<IActionResult> GetOrderPaymentStatus(Guid orderId)
        {
            try
            {
                var transaction = await _paymentRepository.GetTransactionByOrderIdAsync(orderId);
                if (transaction == null)
                    return NotFound(ApiResponse.Fail("No payment found for this order"));

                return Ok(ApiResponse.Ok(new
                {
                    transaction.Status,
                    transaction.Amount,
                    PaymentDate = transaction.ProcessedAt,
                    TransactionId = transaction.Id
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment status for order {OrderId}", orderId);
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                throw new UnauthorizedAccessException("User ID not found in token");

            return Guid.Parse(userIdClaim.Value);
        }
    }

    public class AddCardRequest
    {
        [Required]
        public string CardNumber { get; set; }

        [Required, MaxLength(255)]
        public string CardHolderName { get; set; }

        [Required, Range(1, 12)]
        public int ExpiryMonth { get; set; }

        [Required, Range(2023, 2035)]
        public int ExpiryYear { get; set; }

        [MinLength(3), MaxLength(4)]
        public string Cvv { get; set; }
    }

    public class PayWithCardRequest
    {
        [Required]
        public Guid OrderId { get; set; }

        [Required, Range(0.01, 100000)]
        public decimal Amount { get; set; }

        [Required]
        public Guid CardId { get; set; }

        public string Cvv { get; set; } // Для дополнительной аутентификации
    }

    public class PayRequest
    {
        [Required]
        public Guid OrderId { get; set; }

        [Required, Range(0.01, 100000)]
        public decimal Amount { get; set; }

        public Guid? CardId { get; set; }
    }
}