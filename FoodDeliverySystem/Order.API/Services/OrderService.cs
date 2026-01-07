using Order.API.Controllers;
using Order.API.Interfaces;
using Order.API.Entities;
using System;
using System.Threading.Tasks;

namespace Order.API.Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly ILogger<OrderService> _logger;

        public OrderService(IOrderRepository orderRepository, ILogger<OrderService> logger)
        {
            _orderRepository = orderRepository;
            _logger = logger;
        }

        public async Task<OrderEntity> CreateOrderAsync(Guid userId, CreateOrderRequest request)
        {
            // Валидация
            await ValidateOrderAsync(userId);

            // Создание заказа
            var order = await _orderRepository.CreateOrderAsync(
                userId,
                request.DeliveryAddress,
                request.SpecialInstructions);

            _logger.LogInformation("Order {OrderId} created for user {UserId}", order.Id, userId);

            return order;
        }

        public async Task<bool> CancelOrderAsync(Guid orderId, Guid userId)
        {
            var success = await _orderRepository.CancelOrderAsync(orderId, userId);

            if (success)
            {
                _logger.LogInformation("Order {OrderId} cancelled by user {UserId}", orderId, userId);
            }

            return success;
        }

        public async Task<decimal> CalculateOrderTotalAsync(Guid userId)
        {
            var cart = await _orderRepository.GetCartAsync(userId);
            if (cart == null || !cart.CartItems.Any())
                return 0;

            decimal total = 0;
            foreach (var item in cart.CartItems)
            {
                var dish = await _orderRepository.GetDishAsync(item.DishId);
                if (dish != null && dish.IsAvailable)
                {
                    total += dish.Price * item.Quantity;
                }
            }

            return total;
        }

        public async Task ValidateOrderAsync(Guid userId)
        {
            var cart = await _orderRepository.GetCartAsync(userId);
            if (cart == null || !cart.CartItems.Any())
                throw new InvalidOperationException("Cart is empty");

            if (!cart.RestaurantId.HasValue)
                throw new InvalidOperationException("No restaurant selected");

            var restaurant = await _orderRepository.GetRestaurantAsync(cart.RestaurantId.Value);
            if (restaurant == null || !restaurant.IsActive)
                throw new InvalidOperationException("Restaurant not available");

            // Проверяем доступность всех блюд
            foreach (var item in cart.CartItems)
            {
                var dish = await _orderRepository.GetDishAsync(item.DishId);
                if (dish == null || !dish.IsAvailable)
                    throw new InvalidOperationException($"Dish {item.DishId} is not available");
            }
        }
    }
}