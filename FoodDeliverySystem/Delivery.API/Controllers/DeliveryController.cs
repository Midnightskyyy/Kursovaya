using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Core.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Delivery.API.Interfaces;

namespace Delivery.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DeliveryController : ControllerBase
    {
        private readonly IDeliveryRepository _deliveryRepository;
        private readonly IDeliveryService _deliveryService;
        private readonly ILogger<DeliveryController> _logger;

        public DeliveryController(
            IDeliveryRepository deliveryRepository,
            IDeliveryService deliveryService,
            ILogger<DeliveryController> logger)
        {
            _deliveryRepository = deliveryRepository;
            _deliveryService = deliveryService;
            _logger = logger;
        }

        [HttpGet("order/{orderId}")]
        public async Task<IActionResult> GetDeliveryByOrder(Guid orderId)
        {
            try
            {
                _logger.LogInformation("Getting delivery for order {OrderId}", orderId);

                var delivery = await _deliveryRepository.GetDeliveryByOrderIdAsync(orderId);
                if (delivery == null)
                {
                    _logger.LogWarning("Delivery not found for order {OrderId}", orderId);
                    return NotFound(ApiResponse.Fail("Delivery not found for this order"));
                }

                // ДЕБАГ: выводим реальные значения из БД
                _logger.LogInformation("DEBUG: Prep={Prep}, Delivery={Delivery}, Total={Total}",
                    delivery.PreparationTimeMinutes,
                    delivery.DeliveryTimeMinutes,
                    delivery.EstimatedDurationMinutes);

                var now = DateTime.UtcNow;
                var estimatedTime = delivery.EstimatedDeliveryTime ?? now.AddMinutes(delivery.EstimatedDurationMinutes);
                var remainingMinutes = (int)Math.Ceiling((estimatedTime - now).TotalMinutes);

                // Рассчитываем фазы
                var prepRemaining = 0;
                var deliveryRemaining = 0;
                var currentPhase = "preparation";

                if (delivery.Status == "Preparing" || delivery.Status == "ReadyForPickup")
                {
                    currentPhase = "preparation";
                    if (delivery.PreparationStartedAt.HasValue)
                    {
                        var prepElapsed = (now - delivery.PreparationStartedAt.Value).TotalMinutes;
                        prepRemaining = Math.Max(0, delivery.PreparationTimeMinutes - (int)prepElapsed);
                    }
                    else
                    {
                        prepRemaining = delivery.PreparationTimeMinutes;
                    }
                }
                else if (delivery.Status == "Assigned" || delivery.Status == "PickedUp" || delivery.Status == "OnTheWay")
                {
                    currentPhase = "delivery";
                    if (delivery.DeliveryStartedAt.HasValue)
                    {
                        var deliveryElapsed = (now - delivery.DeliveryStartedAt.Value).TotalMinutes;
                        deliveryRemaining = Math.Max(0, delivery.DeliveryTimeMinutes - (int)deliveryElapsed);
                    }
                    else
                    {
                        deliveryRemaining = delivery.DeliveryTimeMinutes;
                    }
                }
                else if (delivery.Status == "Delivered")
                {
                    currentPhase = "completed";
                    remainingMinutes = 0;
                    prepRemaining = 0;
                    deliveryRemaining = 0;
                }

                var response = new
                {
                    delivery.Id,
                    delivery.OrderId,
                    delivery.Status,
                    delivery.DeliveryAddress,
                    EstimatedDeliveryTime = delivery.EstimatedDeliveryTime,

                    // Время - используем ПРАВИЛЬНЫЕ поля из БД
                    TotalMinutes = delivery.EstimatedDurationMinutes,
                    PreparationMinutes = delivery.PreparationTimeMinutes, // ← должно быть 2
                    DeliveryMinutes = delivery.DeliveryTimeMinutes, // ← должно быть 41
                    RemainingMinutes = Math.Max(0, remainingMinutes),
                    PreparationRemainingMinutes = prepRemaining,
                    DeliveryRemainingMinutes = deliveryRemaining,
                    CurrentPhase = currentPhase,

                    // Прогресс
                    ProgressPercentage = delivery.Status == "Delivered" ? 100 :
                        delivery.Status == "Cancelled" ? 0 :
                        Math.Min(100, Math.Max(0, (1 - (remainingMinutes / (double)delivery.EstimatedDurationMinutes)) * 100)),

                    // Временные метки
                    CreatedAt = delivery.CreatedAt,
                    UpdatedAt = delivery.UpdatedAt,
                    PreparationStartedAt = delivery.PreparationStartedAt,
                    DeliveryStartedAt = delivery.DeliveryStartedAt,

                    // Курьер
                    Courier = delivery.Courier != null ? new
                    {
                        delivery.Courier.Id,
                        delivery.Courier.Name,
                        delivery.Courier.PhoneNumber,
                        delivery.Courier.VehicleType,
                        delivery.Courier.Rating,
                        delivery.Courier.IsAvailable
                    } : null
                };

                _logger.LogInformation("Found delivery for order {OrderId}: {DeliveryId}, Prep={Prep}, Delivery={Delivery}",
                    orderId, delivery.Id, delivery.PreparationTimeMinutes, delivery.DeliveryTimeMinutes);

                return Ok(ApiResponse.Ok(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting delivery for order {OrderId}", orderId);
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpGet("track/{deliveryId}")]
        public async Task<IActionResult> TrackDelivery(Guid deliveryId)
        {
            try
            {
                var delivery = await _deliveryRepository.GetDeliveryAsync(deliveryId);
                if (delivery == null)
                    return NotFound(ApiResponse.Fail("Delivery not found"));

                return Ok(ApiResponse.Ok(new
                {
                    delivery.Status,
                    delivery.EstimatedDeliveryTime,
                    delivery.EstimatedDurationMinutes,
                    Courier = delivery.Courier != null ? new
                    {
                        delivery.Courier.Name,
                        delivery.Courier.PhoneNumber,
                        delivery.Courier.VehicleType,
                        delivery.Courier.Rating
                    } : null,
                    Timeline = new
                    {
                        delivery.CreatedAt
                    }
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking delivery {DeliveryId}", deliveryId);
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }


        [HttpGet("courier/{courierId}/deliveries")]
        [Authorize(Roles = "Courier,Admin")]
        public async Task<IActionResult> GetCourierDeliveries(Guid courierId)
        {
            try
            {
                // Проверяем, что курьер запрашивает свои доставки
                var currentUserId = GetUserId();
                // Здесь можно добавить проверку, что courierId соответствует currentUserId
                // или пользователь является администратором

                var deliveries = await _deliveryRepository.GetCourierDeliveriesAsync(courierId);
                return Ok(ApiResponse.Ok(deliveries));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting deliveries for courier {CourierId}", courierId);
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpPost("{deliveryId}/status")]
        [Authorize(Roles = "Courier,Admin")]
        public async Task<IActionResult> UpdateDeliveryStatus(Guid deliveryId, [FromBody] UpdateStatusRequest request)
        {
            try
            {
                var courierId = GetUserId();
                var delivery = await _deliveryService.UpdateDeliveryStatusAsync(deliveryId, request.Status, courierId);

                return Ok(ApiResponse.Ok(new
                {
                    delivery.Status,
                    UpdatedAt = delivery.UpdatedAt
                }, "Delivery status updated"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResponse.Fail(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating delivery {DeliveryId} status", deliveryId);
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpPost("{deliveryId}/assign")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignCourier(Guid deliveryId)
        {
            try
            {
                var delivery = await _deliveryService.AssignCourierAsync(deliveryId);

                return Ok(ApiResponse.Ok(new
                {
                    delivery.CourierId,
                    delivery.Status
                }, "Courier assigned successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning courier to delivery {DeliveryId}", deliveryId);
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpPost("{deliveryId}/simulate")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SimulateDeliveryProgress(Guid deliveryId)
        {
            try
            {
                var delivery = await _deliveryService.SimulateDeliveryProgressAsync(deliveryId);

                return Ok(ApiResponse.Ok(new
                {
                    delivery.Status,
                    delivery.CourierId,
                    EstimatedDelivery = delivery.EstimatedDeliveryTime
                }, "Delivery progress simulated"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error simulating delivery progress for {DeliveryId}", deliveryId);
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpGet("{deliveryId}/timer")]
        public async Task<IActionResult> GetDeliveryTimerInfo(Guid deliveryId)
        {
            try
            {
                var delivery = await _deliveryRepository.GetDeliveryAsync(deliveryId);
                if (delivery == null)
                    return NotFound(ApiResponse.Fail("Delivery not found"));

                var now = DateTime.UtcNow;
                var estimatedTime = delivery.EstimatedDeliveryTime ?? now.AddMinutes(delivery.EstimatedDurationMinutes);
                var remainingMinutes = (int)Math.Ceiling((estimatedTime - now).TotalMinutes);

                // Рассчитываем оставшееся время для каждой фазы
                var prepRemaining = 0;
                var deliveryRemaining = 0;
                var currentPhase = "preparation";

                if (delivery.Status == "Preparing" || delivery.Status == "ReadyForPickup")
                {
                    if (delivery.PreparationStartedAt.HasValue)
                    {
                        var prepElapsed = (now - delivery.PreparationStartedAt.Value).TotalMinutes;
                        prepRemaining = Math.Max(0, delivery.PreparationTimeMinutes - (int)prepElapsed);
                        currentPhase = "preparation";
                    }
                }
                else if (delivery.Status == "Assigned" || delivery.Status == "PickedUp" || delivery.Status == "OnTheWay")
                {
                    currentPhase = "delivery";
                    // Устанавливаем время начала доставки если его еще нет
                    if (delivery.Status == "Assigned" && !delivery.DeliveryStartedAt.HasValue)
                    {
                        delivery.DeliveryStartedAt = DateTime.UtcNow;
                        await _deliveryRepository.UpdateDeliveryAsync(delivery);
                    }

                    if (delivery.DeliveryStartedAt.HasValue)
                    {
                        var deliveryElapsed = (now - delivery.DeliveryStartedAt.Value).TotalMinutes;
                        deliveryRemaining = Math.Max(0, delivery.DeliveryTimeMinutes - (int)deliveryElapsed);
                    }
                    else
                    {
                        deliveryRemaining = delivery.DeliveryTimeMinutes;
                    }
                }
                else if (delivery.Status == "Delivered")
                {
                    currentPhase = "completed";
                    remainingMinutes = 0;
                    prepRemaining = 0;
                    deliveryRemaining = 0;
                }

                return Ok(ApiResponse.Ok(new
                {
                    delivery.Id,
                    delivery.OrderId,
                    delivery.Status,
                    EstimatedDeliveryTime = delivery.EstimatedDeliveryTime,
                    TotalMinutes = delivery.EstimatedDurationMinutes,
                    PreparationMinutes = delivery.PreparationTimeMinutes,
                    DeliveryMinutes = delivery.DeliveryTimeMinutes,
                    RemainingMinutes = Math.Max(0, remainingMinutes),
                    PreparationRemainingMinutes = prepRemaining,
                    DeliveryRemainingMinutes = deliveryRemaining,
                    CurrentPhase = currentPhase,
                    ProgressPercentage = delivery.Status == "Delivered" ? 100 :
                        delivery.Status == "Cancelled" ? 0 :
                        Math.Min(100, Math.Max(0, (1 - (remainingMinutes / (double)delivery.EstimatedDurationMinutes)) * 100)),
                    TimeCreated = delivery.CreatedAt,
                    TimeUpdated = delivery.UpdatedAt,
                    PreparationStartedAt = delivery.PreparationStartedAt,
                    DeliveryStartedAt = delivery.DeliveryStartedAt,
                    Courier = delivery.Courier != null ? new
                    {
                        delivery.Courier.Id,
                        delivery.Courier.Name,
                        delivery.Courier.PhoneNumber,
                        delivery.Courier.VehicleType,
                        delivery.Courier.Rating
                    } : null
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting delivery timer info");
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpGet("active")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetActiveDeliveries()
        {
            try
            {
                var deliveries = await _deliveryRepository.GetActiveDeliveriesAsync();
                return Ok(ApiResponse.Ok(deliveries.Select(d => new
                {
                    d.Id,
                    d.OrderId,
                    d.Status,
                    d.DeliveryAddress,
                    d.EstimatedDeliveryTime,
                    d.CreatedAt,
                    Courier = d.Courier != null ? new { d.Courier.Name, d.Courier.PhoneNumber } : null
                })));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active deliveries");
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



    public class UpdateStatusRequest
    {
        [Required]
        public string Status { get; set; }
    }
}