using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.API.Interfaces;
using Shared.Core.Models;

namespace Order.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MenuController : ControllerBase
    {
        private readonly IOrderRepository _orderRepository;
        private readonly ILogger<MenuController> _logger;

        public MenuController(IOrderRepository orderRepository, ILogger<MenuController> logger)
        {
            _orderRepository = orderRepository;
            _logger = logger;
        }

        [HttpGet("restaurants")]
        public async Task<IActionResult> GetRestaurants()
        {
            try
            {
                var restaurants = await _orderRepository.GetRestaurantsAsync();
                return Ok(ApiResponse.Ok(restaurants));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting restaurants");
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpGet("restaurants/{restaurantId}/dishes")]
        public async Task<IActionResult> GetDishes(Guid restaurantId)
        {
            try
            {
                var dishes = await _orderRepository.GetDishesByRestaurantAsync(restaurantId);
                return Ok(ApiResponse.Ok(dishes));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dishes for restaurant {RestaurantId}", restaurantId);
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }
    }
}