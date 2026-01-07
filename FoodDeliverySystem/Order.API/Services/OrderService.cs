using Order.API.Controllers;
using Order.API.Interfaces;
using Order.API.Entities;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Order.API.Data;

namespace Order.API.Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly OrderDbContext _context;
        private readonly ILogger<OrderService> _logger;

        public OrderService(IOrderRepository orderRepository, OrderDbContext context, ILogger<OrderService> logger)
        {
            _orderRepository = orderRepository;
            _context = context;
            _logger = logger;
        }

        public async Task<OrderEntity> CreateOrderAsync(Guid userId, CreateOrderRequest request)
        {
            var cart = await _orderRepository.GetCartAsync(userId);
            if (cart == null || !cart.CartItems.Any())
                throw new InvalidOperationException("Cart is empty");

            // Группируем товары по ресторанам
            var itemsByRestaurant = cart.CartItems
                .GroupBy(ci => ci.RestaurantId)
                .ToList();

            List<OrderEntity> orders = new();

            foreach (var restaurantGroup in itemsByRestaurant)
            {
                var restaurantId = restaurantGroup.Key;
                var restaurant = await _orderRepository.GetRestaurantAsync(restaurantId);

                if (restaurant == null || !restaurant.IsActive)
                    throw new InvalidOperationException($"Restaurant {restaurantId} not available");

                var order = new OrderEntity
                {
                    Id = Guid.NewGuid(), // Добавляем ID
                    UserId = userId,
                    RestaurantId = restaurantId,
                    DeliveryAddress = request.DeliveryAddress,
                    SpecialInstructions = request.SpecialInstructions,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };

                decimal totalAmount = 0;
                int maxPreparationTime = 0;

                foreach (var cartItem in restaurantGroup)
                {
                    var dish = await _orderRepository.GetDishAsync(cartItem.DishId);
                    if (dish == null || !dish.IsAvailable)
                        throw new InvalidOperationException($"Dish {cartItem.DishId} not available");

                    // Создаем OrderItem
                    var orderItem = new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id, // Связываем с заказом
                        DishId = dish.Id,
                        DishName = dish.Name,
                        UnitPrice = dish.Price,
                        Quantity = cartItem.Quantity,
                        CreatedAt = DateTime.UtcNow
                    };

                    order.OrderItems.Add(orderItem);

                    totalAmount += dish.Price * cartItem.Quantity;
                    maxPreparationTime = Math.Max(maxPreparationTime, dish.PreparationTime);
                }

                order.TotalAmount = totalAmount;
                order.EstimatedCookingTime = maxPreparationTime;
                order.ReadyAt = DateTime.UtcNow.AddMinutes(maxPreparationTime);

                // Сохраняем заказ НАПРЯМУЮ через контекст
                await _context.Orders.AddAsync(order);
                await _context.SaveChangesAsync();

                orders.Add(order);
            }

            // Очищаем корзину
            await _orderRepository.ClearCartAsync(userId);

            // Возвращаем первый заказ
            return orders.First();
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

        public async Task<int> CalculateDeliveryTimeAsync(Guid userId)
        {
            var cart = await _orderRepository.GetCartAsync(userId);
            if (cart == null || !cart.CartItems.Any())
                return 45; // Базовое время доставки

            // Группируем по ресторанам и берем максимальное время приготовления
            var maxPreparationTime = cart.CartItems
                .GroupBy(ci => ci.RestaurantId)
                .Max(g => g.Max(ci => ci.Dish?.PreparationTime ?? 15));

            // Время доставки = максимальное время приготовления + 30 минут на доставку
            return maxPreparationTime + 30;
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