using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payment.API.Interfaces;
using Shared.Core.Models;
using Shared.Messages.Events;
using Shared.Messages.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

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
                return Ok(ApiResponse.Ok(cards));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cards");
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpPost("cards")]
        public async Task<IActionResult> AddCard([FromBody] AddCardRequest request)
        {
            try
            {
                var userId = GetUserId();
                var card = await _paymentService.AddCardAsync(
                    userId,
                    request.CardNumber,
                    request.CardHolderName,
                    request.ExpiryMonth,
                    request.ExpiryYear,
                    request.Cvv);

                return Ok(ApiResponse.Ok(new
                {
                    card.Id,
                    card.CardLastFourDigits,
                    card.CardHolderName
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
                var userId = GetUserId();
                var transaction = await _paymentService.ProcessPaymentAsync(
                    request.OrderId,
                    userId,
                    request.Amount,
                    request.CardId);

                // Публикация события в зависимости от результата
                if (transaction.Status == "Success")
                {
                    _messageBus.Publish(new PaymentConfirmedEvent
                    {
                        OrderId = transaction.OrderId,
                        TransactionId = transaction.Id,
                        Amount = transaction.Amount,
                        PaidAt = transaction.ProcessedAt ?? DateTime.UtcNow
                    }, "payment.events", "payment.confirmed");

                    _logger.LogInformation("Payment confirmed for order {OrderId}", request.OrderId);
                }
                else if (transaction.Status == "Failed")
                {
                    _messageBus.Publish(new PaymentFailedEvent
                    {
                        OrderId = transaction.OrderId,
                        Reason = transaction.FailureReason,
                        FailedAt = transaction.ProcessedAt ?? DateTime.UtcNow
                    }, "payment.events", "payment.failed");

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
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
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
        [Required, CreditCard]
        public string CardNumber { get; set; }

        [Required, MaxLength(255)]
        public string CardHolderName { get; set; }

        [Required, Range(1, 12)]
        public int ExpiryMonth { get; set; }

        [Required, Range(2023, 2030)]
        public int ExpiryYear { get; set; }

        [Required, MinLength(3), MaxLength(4)]
        public string Cvv { get; set; }
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