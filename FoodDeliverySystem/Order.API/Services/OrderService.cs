using Order.API.Controllers;
using Order.API.Interfaces;
using Order.API.Entities;
using Microsoft.EntityFrameworkCore;
using Order.API.Data;
using Shared.Messages.Events;
using Shared.Messages.Interfaces;

namespace Order.API.Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly OrderDbContext _context;
        private readonly ILogger<OrderService> _logger;
        private readonly IMessageBusClient _messageBus;
        private readonly HashSet<Guid> _publishedOrders = new();

        public OrderService(
            IOrderRepository orderRepository,
            OrderDbContext context,
            ILogger<OrderService> logger,
            IMessageBusClient messageBus)
        {
            _orderRepository = orderRepository;
            _context = context;
            _logger = logger;
            _messageBus = messageBus;
        }

        public async Task<OrderEntity> CreateOrderAsync(Guid userId, CreateOrderRequest request)
        {
            try
            {
                _logger.LogInformation("=== НАЧАЛО СОЗДАНИЯ ЗАКАЗА ===");
                _logger.LogInformation("Пользователь: {UserId}", userId);
                _logger.LogInformation("Адрес доставки: {Address}", request.DeliveryAddress);

                // Получаем корзину
                var cart = await _orderRepository.GetCartAsync(userId);
                if (cart == null || cart.CartItems == null || !cart.CartItems.Any())
                {
                    _logger.LogError("❌ Корзина пуста или не найдена");
                    throw new InvalidOperationException("Cart is empty");
                }

                _logger.LogInformation("Найдена корзина с {Count} товарами", cart.CartItems.Count);

                var cartItems = cart.CartItems.ToList();
                var dishIds = cartItems.Select(ci => ci.DishId).ToList();

                // Получаем блюда с информацией о ресторанах
                var dishes = await _context.Dishes
                    .Where(d => dishIds.Contains(d.Id))
                    .Include(d => d.Restaurant)
                    .ToListAsync();

                _logger.LogInformation("Загружено {DishCount} блюд из БД", dishes.Count);

                // Проверка доступности всех блюд
                foreach (var cartItem in cartItems)
                {
                    var dish = dishes.FirstOrDefault(d => d.Id == cartItem.DishId);
                    if (dish == null)
                    {
                        _logger.LogError("Блюдо {DishId} не найдено", cartItem.DishId);
                        throw new InvalidOperationException($"Dish {cartItem.DishId} not found");
                    }

                    if (!dish.IsAvailable)
                    {
                        _logger.LogError("Блюдо {DishName} недоступно", dish.Name);
                        throw new InvalidOperationException($"Dish {dish.Name} is not available");
                    }

                    if (dish.Restaurant == null)
                    {
                        _logger.LogError("Ресторан для блюда {DishName} не найден", dish.Name);
                        throw new InvalidOperationException($"Restaurant not found for dish {dish.Name}");
                    }

                    if (!dish.Restaurant.IsActive)
                    {
                        _logger.LogError("Ресторан {RestaurantName} не активен", dish.Restaurant.Name);
                        throw new InvalidOperationException($"Restaurant {dish.Restaurant.Name} is not available");
                    }
                }

                // Получаем максимальное время приготовления из всех блюд в заказе
                int maxPreparationTime = dishes.Any() ? dishes.Max(d => d.PreparationTime) : 20;
                _logger.LogInformation("Максимальное время приготовления: {MaxPrepTime} минут", maxPreparationTime);

                // Создаем заказ
                var order = new OrderEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    RestaurantId = dishes.FirstOrDefault()?.RestaurantId,
                    DeliveryAddress = request.DeliveryAddress,
                    SpecialInstructions = request.SpecialInstructions ?? string.Empty,
                    Status = "Preparing",
                    TotalAmount = 0, // Будет рассчитано ниже
                    EstimatedCookingTime = maxPreparationTime,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    OrderItems = new List<OrderItem>()
                };

                // Добавляем позиции и рассчитываем сумму
                decimal totalAmount = 0;

                foreach (var cartItem in cartItems)
                {
                    var dish = dishes.FirstOrDefault(d => d.Id == cartItem.DishId);
                    if (dish == null) continue;

                    var orderItem = new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        DishId = dish.Id,
                        DishName = dish.Name,
                        UnitPrice = dish.Price,
                        Quantity = cartItem.Quantity,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    order.OrderItems.Add(orderItem);
                    totalAmount += dish.Price * cartItem.Quantity;

                    _logger.LogInformation("Добавлено блюдо: {DishName} x {Quantity} = {ItemTotal}",
                        dish.Name, cartItem.Quantity, dish.Price * cartItem.Quantity);
                }

                if (order.OrderItems.Count == 0)
                {
                    _logger.LogError("❌ Нет валидных товаров в корзине");
                    throw new InvalidOperationException("No valid items in cart");
                }

                order.TotalAmount = totalAmount;
                _logger.LogInformation("Общая сумма заказа: {TotalAmount}", order.TotalAmount);

                // Сохраняем заказ в БД
                await _context.Orders.AddAsync(order);

                // Очищаем корзину
                await _orderRepository.ClearCartAsync(userId);

                // Сохраняем изменения
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Заказ {OrderId} успешно сохранен в БД", order.Id);

                // Создаем событие для публикации в RabbitMQ
                var orderEvent = new OrderCreatedEvent
                {
                    OrderId = order.Id,
                    UserId = order.UserId,
                    RestaurantId = order.RestaurantId ?? Guid.Empty,
                    TotalAmount = order.TotalAmount,
                    DeliveryAddress = order.DeliveryAddress,
                    CreatedAt = order.CreatedAt,
                    MaxPreparationTime = maxPreparationTime,
                    Items = order.OrderItems.Select(i => new OrderItemDto
                    {
                        DishId = i.DishId,
                        DishName = i.DishName,
                        UnitPrice = i.UnitPrice,
                        Quantity = i.Quantity
                    }).ToList()
                };

                _logger.LogInformation("📤 Публикую событие OrderCreatedEvent:");
                _logger.LogInformation("  OrderId: {OrderId}", orderEvent.OrderId);
                _logger.LogInformation("  TotalAmount: {TotalAmount}", orderEvent.TotalAmount);
                _logger.LogInformation("  DeliveryAddress: {Address}", orderEvent.DeliveryAddress);
                _logger.LogInformation("  MaxPreparationTime: {MaxPrepTime}", orderEvent.MaxPreparationTime);
                _logger.LogInformation("  Items count: {ItemsCount}", orderEvent.Items.Count);

                // Публикуем событие в RabbitMQ
                try
                {
                    _messageBus.Publish(orderEvent, "order.created");
                    _publishedOrders.Add(order.Id);
                    _logger.LogInformation("✅ Событие успешно опубликовано в RabbitMQ");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Ошибка публикации события в RabbitMQ");
                    // Не прерываем выполнение - заказ уже сохранен
                }
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Ждем время приготовления
                        await Task.Delay(TimeSpan.FromMinutes(maxPreparationTime));

                        // Публикуем событие готовности заказа
                        _messageBus.Publish(new OrderReadyForDeliveryEvent
                        {
                            OrderId = order.Id,
                            RestaurantId = order.RestaurantId ?? Guid.Empty,
                            ReadyAt = DateTime.UtcNow,
                            PreparationTime = maxPreparationTime
                        }, "order.ready");

                        _logger.LogInformation("✅ Order {OrderId} is ready for delivery", order.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error publishing order ready event");
                    }
                });

                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ КРИТИЧЕСКАЯ ОШИБКА в CreateOrderAsync");
                throw;
            }
        }

        public async Task<bool> UpdateOrderStatusAsync(Guid orderId, string status)
        {
            try
            {
                _logger.LogInformation("🔄 Updating order {OrderId} status to '{Status}'", orderId, status);

                var order = await _context.Orders.FindAsync(orderId);
                if (order == null)
                {
                    _logger.LogWarning("⚠️ Order {OrderId} not found", orderId);
                    return false;
                }

                // Обновленный список валидных статусов
                var validStatuses = new[] {
            "Pending",
            "Preparing",
            "PickingUp",
            "OnTheWay",  // ← Добавьте OnTheWay
            "Delivered",
            "Cancelled"
        };

                if (!validStatuses.Contains(status))
                {
                    _logger.LogWarning("⚠️ Invalid order status: {Status}", status);
                    return false;
                }

                var oldStatus = order.Status;

                // Не обновляем, если статус не изменился
                if (oldStatus == status)
                {
                    _logger.LogInformation("ℹ️ Order {OrderId} already has status '{Status}'", orderId, status);
                    return true;
                }

                order.Status = status;
                order.UpdatedAt = DateTime.UtcNow;

                // Устанавливаем время готовности если заказ перешел в PickingUp
                if (status == "PickingUp" && !order.ReadyAt.HasValue)
                {
                    order.ReadyAt = DateTime.UtcNow;
                    _logger.LogInformation("⏰ Order {OrderId} ready at {ReadyAt}", orderId, order.ReadyAt);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Order {OrderId} status updated: {OldStatus} -> {NewStatus}",
                    orderId, oldStatus, status);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating order {OrderId} status", orderId);
                return false;
            }
        }

        public async Task UpdateOrderStatusFromDeliveryAsync(Guid orderId, string status)
        {
            try
            {
                _logger.LogInformation("Обновление статуса заказа {OrderId} на {Status} из доставки",
                    orderId, status);

                var order = await _context.Orders.FindAsync(orderId);
                if (order != null)
                {
                    order.Status = status;
                    order.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("✅ Статус заказа {OrderId} обновлен на {Status}", orderId, status);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка обновления статуса заказа {OrderId}", orderId);
            }
        }

        public async Task<bool> CancelOrderAsync(Guid orderId, Guid userId)
        {
            try
            {
                _logger.LogInformation("Отмена заказа {OrderId} пользователем {UserId}", orderId, userId);

                // Получаем заказ
                var order = await _context.Orders
                    .Include(o => o.OrderItems) // Добавляем для полной информации
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

                if (order == null)
                {
                    _logger.LogWarning("⚠️ Заказ {OrderId} не найден", orderId);
                    return false;
                }

                // Проверяем, можно ли отменить заказ
                // Разрешаем отмену только если заказ еще не готовится или не в доставке
                var canCancelStatuses = new[] { "Pending", "Preparing", "PickingUp" };

                if (!canCancelStatuses.Contains(order.Status))
                {
                    _logger.LogWarning("⚠️ Нельзя отменить заказ {OrderId} в статусе {Status}",
                        orderId, order.Status);
                    return false;
                }

                var oldStatus = order.Status;

                // Обновляем статус
                order.Status = "Cancelled";
                order.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Заказ {OrderId} успешно отменен: {OldStatus} -> Cancelled",
                    orderId, oldStatus);

                // Публикуем событие отмены заказа
                try
                {
                    var cancelledEvent = new OrderCancelledEvent
                    {
                        OrderId = orderId,
                        UserId = userId,
                        CancelledAt = DateTime.UtcNow,
                        Reason = "User cancelled order"
                    };

                    _messageBus.Publish(cancelledEvent, "order.cancelled");
                    _logger.LogInformation("📤 Событие OrderCancelledEvent опубликовано");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Ошибка публикации события отмены заказа");
                }

                // Также публикуем событие обновления статуса
                try
                {
                    var statusEvent = new OrderStatusUpdatedEvent
                    {
                        OrderId = orderId,
                        Status = "Cancelled",
                        UpdatedAt = DateTime.UtcNow
                    };

                    _messageBus.Publish(statusEvent, "order.status.updated");
                    _logger.LogInformation("📤 Событие OrderStatusUpdatedEvent опубликовано");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Ошибка публикации события статуса");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка при отмене заказа {OrderId}", orderId);
                return false;
            }
        }

        public async Task<decimal> CalculateOrderTotalAsync(Guid userId)
        {
            try
            {
                var cart = await _orderRepository.GetCartAsync(userId);
                if (cart == null || !cart.CartItems.Any())
                {
                    _logger.LogInformation("Корзина пуста для пользователя {UserId}", userId);
                    return 0;
                }

                decimal total = 0;
                foreach (var item in cart.CartItems)
                {
                    var dish = await _orderRepository.GetDishAsync(item.DishId);
                    if (dish != null && dish.IsAvailable)
                    {
                        total += dish.Price * item.Quantity;
                    }
                }

                _logger.LogInformation("Сумма корзины для пользователя {UserId}: {Total}", userId, total);
                return total;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка расчета суммы корзины для пользователя {UserId}", userId);
                return 0;
            }
        }

        public async Task<int> CalculateDeliveryTimeAsync(Guid userId)
        {
            try
            {
                var cart = await _orderRepository.GetCartAsync(userId);
                if (cart == null || !cart.CartItems.Any())
                {
                    _logger.LogInformation("Корзина пуста, используем базовое время доставки");
                    return 45; // Базовое время доставки
                }

                // Группируем по ресторанам и берем максимальное время приготовления
                var maxPreparationTime = cart.CartItems
                    .GroupBy(ci => ci.RestaurantId)
                    .Max(g => g.Max(ci => ci.Dish?.PreparationTime ?? 15));

                // Время доставки = максимальное время приготовления + 30 минут на доставку
                var totalDeliveryTime = maxPreparationTime + 30;

                _logger.LogInformation("Расчетное время доставки: {TotalTime} мин (приготовление: {PrepTime} мин)",
                    totalDeliveryTime, maxPreparationTime);

                return totalDeliveryTime;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка расчета времени доставки для пользователя {UserId}", userId);
                return 45;
            }
        }

        public async Task ValidateOrderAsync(Guid userId)
        {
            try
            {
                var cart = await _orderRepository.GetCartAsync(userId);
                if (cart == null || !cart.CartItems.Any())
                {
                    _logger.LogError("❌ Корзина пуста для пользователя {UserId}", userId);
                    throw new InvalidOperationException("Cart is empty");
                }

                var cartItems = cart.CartItems.ToList();
                _logger.LogInformation("Валидация {Count} товаров в корзине", cartItems.Count);

                // Проверяем доступность всех блюд
                foreach (var cartItem in cartItems)
                {
                    var dish = await _orderRepository.GetDishAsync(cartItem.DishId);
                    if (dish == null || !dish.IsAvailable)
                    {
                        _logger.LogError("❌ Блюдо {DishId} недоступно", cartItem.DishId);
                        throw new InvalidOperationException($"Dish {cartItem.DishId} is not available");
                    }

                    // Проверяем доступность ресторана
                    if (dish.Restaurant != null && !dish.Restaurant.IsActive)
                    {
                        _logger.LogError("❌ Ресторан {RestaurantName} не активен", dish.Restaurant.Name);
                        throw new InvalidOperationException($"Restaurant {dish.Restaurant.Name} is not available");
                    }
                }

                _logger.LogInformation("✅ Корзина прошла валидацию");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка валидации корзины для пользователя {UserId}", userId);
                throw;
            }
        }
    }
}