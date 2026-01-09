using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.API.Entities;
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

                _logger.LogInformation("GetCart: UserId={UserId}, CartExists={CartExists}, ItemsCount={ItemsCount}",
                    userId, cart != null, cart?.CartItems?.Count ?? 0);

                if (cart == null)
                {
                    return Ok(ApiResponse.Ok(new
                    {
                        cartItems = new List<object>(),
                        itemCount = 0,
                        totalAmount = 0
                    }));
                }

                // Вычисляем общее количество товаров
                var itemCount = cart.CartItems?.Sum(item => item.Quantity) ?? 0;
                var totalAmount = 0m;

                var cartItemsDto = new List<object>();

                if (cart.CartItems != null)
                {
                    foreach (var item in cart.CartItems)
                    {
                        var dishPrice = item.Dish?.Price ?? 0;
                        totalAmount += dishPrice * item.Quantity;

                        cartItemsDto.Add(new
                        {
                            item.Id,
                            item.CartId,
                            item.DishId,
                            item.Quantity,
                            item.RestaurantId,
                            Dish = item.Dish != null ? new
                            {
                                item.Dish.Id,
                                item.Dish.Name,
                                item.Dish.Description,
                                item.Dish.Price,
                                ImageUrl = item.Dish.ImageUrl ?? "https://via.placeholder.com/100x100?text=Dish",
                                item.Dish.Category,
                                Restaurant = item.Dish.Restaurant != null ? new
                                {
                                    item.Dish.Restaurant.Id,
                                    item.Dish.Restaurant.Name
                                } : null
                            } : null,
                            ItemTotal = dishPrice * item.Quantity
                        });
                    }
                }

                var cartDto = new
                {
                    cart.Id,
                    cart.UserId,
                    cartItems = cartItemsDto,
                    itemCount,
                    totalAmount
                };

                return Ok(ApiResponse.Ok(cartDto));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart for user");
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpPost("items")]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
        {
            try
            {
                var userId = GetUserId();
                _logger.LogInformation("AddToCart: UserId={UserId}, DishId={DishId}, Quantity={Quantity}",
                    userId, request.DishId, request.Quantity);

                await _orderRepository.AddToCartAsync(userId, request.DishId, request.Quantity);

                // Получаем обновленную корзину для ответа
                var cart = await _orderRepository.GetCartAsync(userId);

                _logger.LogInformation("Item added to cart successfully. Cart items count: {Count}",
                    cart?.CartItems?.Count ?? 0);

                return Ok(ApiResponse.Ok(new
                {
                    cartItems = cart?.CartItems?.Select(ci => new
                    {
                        ci.Id,
                        ci.DishId,
                        ci.Quantity,
                        ci.RestaurantId,
                        Dish = ci.Dish != null ? new
                        {
                            ci.Dish.Id,
                            ci.Dish.Name,
                            ci.Dish.Price,
                            ci.Dish.ImageUrl
                        } : null
                    }),
                    totalItems = cart?.CartItems?.Sum(ci => ci.Quantity) ?? 0
                }, "Item added to cart"));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Bad request in AddToCart");
                return BadRequest(ApiResponse.Fail(ex.Message));
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