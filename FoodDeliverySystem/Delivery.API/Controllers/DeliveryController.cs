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
                var delivery = await _deliveryRepository.GetDeliveryByOrderIdAsync(orderId);
                if (delivery == null)
                    return NotFound(ApiResponse.Fail("Delivery not found"));

                return Ok(ApiResponse.Ok(delivery));
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
                        delivery.CreatedAt,
                        delivery.AssignedAt,
                        delivery.PickedUpAt,
                        delivery.DeliveredAt
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
                    delivery.Status,
                    AssignedAt = delivery.AssignedAt
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

        [HttpGet("active")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetActiveDeliveries()
        {
            try
            {
                var deliveries = await _deliveryRepository.GetActiveDeliveriesAsync();
                return Ok(ApiResponse.Ok(deliveries));
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