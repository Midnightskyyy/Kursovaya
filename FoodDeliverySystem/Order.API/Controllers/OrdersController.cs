using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.API.Interfaces;
using Shared.Core.Models;
using Shared.Messages.Events;
using Shared.Messages.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Order.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IOrderService _orderService;
        private readonly IMessageBusClient _messageBus;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            IOrderRepository orderRepository,
            IOrderService orderService,
            IMessageBusClient messageBus,
            ILogger<OrdersController> logger)
        {
            _orderRepository = orderRepository;
            _orderService = orderService;
            _messageBus = messageBus;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            try
            {
                var userId = GetUserId();
                var order = await _orderService.CreateOrderAsync(userId, request);

                // Публикация события
                _messageBus.Publish(new OrderCreatedEvent
                {
                    OrderId = order.Id,
                    UserId = order.UserId,
                    RestaurantId = order.RestaurantId,
                    TotalAmount = order.TotalAmount,
                    DeliveryAddress = order.DeliveryAddress,
                    CreatedAt = order.CreatedAt,
                    Items = order.OrderItems.Select(i => new OrderItemDto
                    {
                        DishId = i.DishId,
                        DishName = i.DishName,
                        UnitPrice = i.UnitPrice,
                        Quantity = i.Quantity
                    }).ToList()
                }, "order.events", "order.created");

                _logger.LogInformation("Order created: {OrderId}", order.Id);

                return Accepted(ApiResponse.Ok(new
                {
                    order.Id,
                    order.Status,
                    order.TotalAmount,
                    EstimatedTotalTime = order.EstimatedCookingTime + 30 // + время доставки
                }, "Order created successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order");
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetOrders()
        {
            try
            {
                var userId = GetUserId();
                var orders = await _orderRepository.GetUserOrdersAsync(userId);
                return Ok(ApiResponse.Ok(orders));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders");
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpGet("{orderId}")]
        public async Task<IActionResult> GetOrder(Guid orderId)
        {
            try
            {
                var userId = GetUserId();
                var order = await _orderRepository.GetOrderAsync(orderId, userId);
                if (order == null)
                    return NotFound(ApiResponse.Fail("Order not found"));

                return Ok(ApiResponse.Ok(order));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order {OrderId}", orderId);
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpPost("{orderId}/cancel")]
        public async Task<IActionResult> CancelOrder(Guid orderId)
        {
            try
            {
                var userId = GetUserId();
                var success = await _orderService.CancelOrderAsync(orderId, userId);

                if (!success)
                    return BadRequest(ApiResponse.Fail("Order cannot be cancelled"));

                return Ok(ApiResponse.Ok(null, "Order cancelled"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order {OrderId}", orderId);
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

    public class CreateOrderRequest
    {
        [Required]
        public string DeliveryAddress { get; set; }

        public string SpecialInstructions { get; set; }
    }
}