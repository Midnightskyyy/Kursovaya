using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.API.Interfaces;
using Shared.Core.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Order.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly IOrderRepository _orderRepository;
        private readonly ILogger<CartController> _logger;

        public CartController(IOrderRepository orderRepository, ILogger<CartController> logger)
        {
            _orderRepository = orderRepository;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetCart()
        {
            try
            {
                var userId = GetUserId();
                var cart = await _orderRepository.GetCartAsync(userId);

                if (cart == null || cart.CartItems == null || !cart.CartItems.Any())
                {
                    return Ok(ApiResponse.Ok(new
                    {
                        Items = new List<object>(),
                        Restaurants = new List<object>(),
                        TotalAmount = 0
                    }));
                }

                // Группируем товары по ресторанам
                var itemsByRestaurant = cart.CartItems
                    .GroupBy(ci => ci.RestaurantId)
                    .Select(g => new
                    {
                        RestaurantId = g.Key,
                        RestaurantName = g.First().Dish?.Restaurant?.Name ?? "Unknown Restaurant",
                        Items = g.Select(ci => new
                        {
                            ci.Id,
                            ci.DishId,
                            DishName = ci.Dish?.Name ?? "Unknown",
                            ci.Quantity,
                            UnitPrice = ci.Dish?.Price ?? 0,
                            Total = (ci.Dish?.Price ?? 0) * ci.Quantity,
                            PreparationTime = ci.Dish?.PreparationTime ?? 15
                        }).ToList(),
                        RestaurantTotal = g.Sum(ci => (ci.Dish?.Price ?? 0) * ci.Quantity),
                        MaxPreparationTime = g.Max(ci => ci.Dish?.PreparationTime ?? 15)
                    }).ToList();

                var response = new
                {
                    CartId = cart.Id,
                    Items = cart.CartItems.Select(ci => new
                    {
                        ci.Id,
                        ci.DishId,
                        ci.RestaurantId,
                        RestaurantName = ci.Dish?.Restaurant?.Name ?? "Unknown",
                        DishName = ci.Dish?.Name ?? "Unknown",
                        ci.Quantity,
                        UnitPrice = ci.Dish?.Price ?? 0,
                        Total = (ci.Dish?.Price ?? 0) * ci.Quantity,
                        PreparationTime = ci.Dish?.PreparationTime ?? 15
                    }).ToList(),
                    GroupedByRestaurant = itemsByRestaurant,
                    TotalAmount = cart.CartItems.Sum(ci => (ci.Dish?.Price ?? 0) * ci.Quantity),
                    // Время доставки = максимальное время приготовления + 30 минут на доставку
                    EstimatedDeliveryTime = itemsByRestaurant.Any() ?
                        itemsByRestaurant.Max(r => r.MaxPreparationTime) + 30 : 45
                };

                return Ok(ApiResponse.Ok(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart");
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpPost("items")]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
        {
            try
            {
                var userId = GetUserId();
                await _orderRepository.AddToCartAsync(userId, request.DishId, request.Quantity);
                return Ok(ApiResponse.Ok(null, "Item added to cart"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to cart");
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpDelete("items/{itemId}")]
        public async Task<IActionResult> RemoveFromCart(Guid itemId)
        {
            try
            {
                var userId = GetUserId();
                await _orderRepository.RemoveFromCartAsync(userId, itemId);
                return Ok(ApiResponse.Ok(null, "Item removed from cart"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing item from cart");
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpPut("items/{itemId}")]
        public async Task<IActionResult> UpdateCartItem(Guid itemId, [FromBody] UpdateCartItemRequest request)
        {
            try
            {
                var userId = GetUserId();
                await _orderRepository.UpdateCartItemAsync(userId, itemId, request.Quantity);
                return Ok(ApiResponse.Ok(null, "Cart item updated"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cart item");
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            return Guid.Parse(userIdClaim.Value);
        }
    }

    public class AddToCartRequest
    {
        [Required]
        public Guid DishId { get; set; }

        [Range(1, 100)]
        public int Quantity { get; set; } = 1;
    }

    public class UpdateCartItemRequest
    {
        [Range(1, 100)]
        public int Quantity { get; set; }
    }
}