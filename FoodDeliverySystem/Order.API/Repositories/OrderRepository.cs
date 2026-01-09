using Microsoft.EntityFrameworkCore;
using Order.API.Entities;
using Order.API.Interfaces;
using Order.API.Data;

namespace Order.API.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly OrderDbContext _context;

        public OrderRepository(OrderDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Restaurant>> GetRestaurantsAsync()
        {
            return await _context.Restaurants
                .Where(r => r.IsActive)
                .Include(r => r.Dishes.Where(d => d.IsAvailable))
                .ToListAsync();
        }

        public async Task<Restaurant> GetRestaurantAsync(Guid id)
        {
            return await _context.Restaurants
                .Include(r => r.Dishes.Where(d => d.IsAvailable))
                .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);
        }

        public async Task<IEnumerable<Dish>> GetDishesByRestaurantAsync(Guid restaurantId)
        {
            return await _context.Dishes
                .Where(d => d.RestaurantId == restaurantId && d.IsAvailable)
                .ToListAsync();
        }

        public async Task<Dish> GetDishAsync(Guid id)
        {
            return await _context.Dishes.FindAsync(id);
        }

        public async Task<ShoppingCart> GetCartAsync(Guid userId)
        {
            try
            {
                var cart = await _context.ShoppingCarts
                    .Include(c => c.CartItems)
                        .ThenInclude(ci => ci.Dish)
                            .ThenInclude(d => d.Restaurant)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart != null)
                {
                    // Явная загрузка для гарантии
                    await _context.Entry(cart)
                        .Collection(c => c.CartItems)
                        .LoadAsync();

                    foreach (var item in cart.CartItems)
                    {
                        await _context.Entry(item)
                            .Reference(i => i.Dish)
                            .LoadAsync();

                        if (item.Dish != null)
                        {
                            await _context.Entry(item.Dish)
                                .Reference(d => d.Restaurant)
                                .LoadAsync();
                        }
                    }
                }

                return cart;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        // Order.API/Repositories/OrderRepository.cs
        public async Task AddToCartAsync(Guid userId, Guid dishId, int quantity)
        {
            try
            {
                // Получаем корзину
                var cart = await _context.ShoppingCarts
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart == null)
                {
                    cart = new ShoppingCart
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _context.ShoppingCarts.AddAsync(cart);
                }

                // Получаем блюдо с включением ресторана
                var dish = await _context.Dishes
                    .Include(d => d.Restaurant) // ВАЖНО: включаем ресторан
                    .FirstOrDefaultAsync(d => d.Id == dishId);

                if (dish == null)
                {
                    throw new ArgumentException("Dish not found");
                }

                if (!dish.IsAvailable)
                {
                    throw new ArgumentException("Dish not available");
                }

                // Проверяем ресторан
                if (dish.Restaurant == null)
                {
                    throw new ArgumentException("Dish restaurant not found");
                }

                if (!dish.Restaurant.IsActive)
                {
                    throw new ArgumentException("Restaurant is not active");
                }

                cart.UpdatedAt = DateTime.UtcNow;

                // Проверяем, есть ли уже такой товар в корзине
                var existingItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci => ci.CartId == cart.Id && ci.DishId == dishId);

                if (existingItem != null)
                {
                    existingItem.Quantity += quantity;
                    existingItem.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    var newItem = new CartItem
                    {
                        Id = Guid.NewGuid(),
                        CartId = cart.Id,
                        DishId = dishId,
                        RestaurantId = dish.Restaurant.Id, // ВАЖНО: устанавливаем RestaurantId
                        Quantity = quantity,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _context.CartItems.AddAsync(newItem);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task RemoveFromCartAsync(Guid userId, Guid itemId)
        {
            var cart = await GetCartAsync(userId);
            if (cart == null) return;

            var item = cart.CartItems.FirstOrDefault(ci => ci.Id == itemId);
            if (item != null)
            {
                _context.CartItems.Remove(item);
                await _context.SaveChangesAsync();

                // Если корзина пуста, сбрасываем ресторан
                if (!cart.CartItems.Any())
                {
                    await _context.SaveChangesAsync();
                }
            }
        }

        public async Task UpdateCartItemAsync(Guid userId, Guid itemId, int quantity)
        {
            if (quantity <= 0)
            {
                await RemoveFromCartAsync(userId, itemId);
                return;
            }

            var cart = await GetCartAsync(userId);
            if (cart == null) return;

            var item = cart.CartItems.FirstOrDefault(ci => ci.Id == itemId);
            if (item != null)
            {
                item.Quantity = quantity;
                await _context.SaveChangesAsync();
            }
        }

        public async Task ClearCartAsync(Guid userId)
        {
            var cart = await GetCartAsync(userId);
            if (cart != null)
            {
                _context.CartItems.RemoveRange(cart.CartItems);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<OrderEntity> CreateOrderAsync(Guid userId, string deliveryAddress, string specialInstructions)
        {
            var cart = await GetCartAsync(userId);
            if (cart == null || !cart.CartItems.Any())
                throw new InvalidOperationException("Cart is empty");

            // Получаем все блюда из корзины
            var cartItems = cart.CartItems.ToList();
            var dishIds = cartItems.Select(ci => ci.DishId).ToList();
            var dishes = await _context.Dishes
                .Where(d => dishIds.Contains(d.Id))
                .Include(d => d.Restaurant)
                .ToListAsync();

            // Проверяем доступность всех блюд
            foreach (var dish in dishes)
            {
                if (!dish.IsAvailable)
                    throw new InvalidOperationException($"Dish {dish.Name} is not available");
                if (!dish.Restaurant?.IsActive ?? true)
                    throw new InvalidOperationException($"Restaurant {dish.Restaurant?.Name} is not available");
            }

            // Создаем заказ
            var order = new OrderEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                // Для мульти-ресторанных заказов RestaurantId может быть null или первым рестораном
                RestaurantId = dishes.FirstOrDefault()?.RestaurantId,
                DeliveryAddress = deliveryAddress,
                SpecialInstructions = specialInstructions,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Добавляем позиции заказа
            decimal totalAmount = 0;
            int maxPreparationTime = 0;

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
                maxPreparationTime = Math.Max(maxPreparationTime, dish.PreparationTime);
            }

            order.TotalAmount = totalAmount;
            order.EstimatedCookingTime = maxPreparationTime;
            order.ReadyAt = DateTime.UtcNow.AddMinutes(maxPreparationTime);

            // Сохраняем заказ
            await _context.Orders.AddAsync(order);

            // Очищаем корзину
            await ClearCartAsync(userId);

            await _context.SaveChangesAsync();

            return order;
        }

        public async Task<OrderEntity> GetOrderAsync(Guid orderId, Guid userId)
        {
            return await _context.Orders
                .Include(o => o.Restaurant)
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
        }

        public async Task<IEnumerable<OrderEntity>> GetUserOrdersAsync(Guid userId)
        {
            // Определяем порядок сортировки по статусу (активные вверху)
            var statusOrder = new Dictionary<string, int>
            {
                ["Preparing"] = 1,    // Готовится
                ["PickingUp"] = 2,    // Ожидает курьера
                ["OnTheWay"] = 3,     // В пути
                ["Delivered"] = 4,    // Доставлен
                ["Cancelled"] = 5     // Отменен
            };

            var orders = await _context.Orders
                .Include(o => o.Restaurant)
                .Include(o => o.OrderItems)
                .Where(o => o.UserId == userId)
                .AsNoTracking()
                .ToListAsync();

            // Сортируем по статусу, затем по времени
            return orders
                .OrderBy(o => statusOrder.TryGetValue(o.Status, out var order) ? order : 6)
                .ThenByDescending(o => o.CreatedAt)
                .ToList();
        }

        public async Task UpdateOrderStatusAsync(Guid orderId, string status)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.Status = status;
                order.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> CancelOrderAsync(Guid orderId, Guid userId)
        {
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            if (order == null)
                return false;

            // Проверяем, можно ли отменить заказ
            // Разрешаем отмену только если заказ еще не готовится или не в доставке
            var canCancelStatuses = new[] { "Pending", "Preparing", "PickingUp" };

            if (!canCancelStatuses.Contains(order.Status))
                return false;

            order.Status = "Cancelled";
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }

        private async Task<ShoppingCart> GetOrCreateCartAsync(Guid userId)
        {
            var cart = await _context.ShoppingCarts
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new ShoppingCart
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _context.ShoppingCarts.AddAsync(cart);
                await _context.SaveChangesAsync();
            }

            return cart;
        }
    }
 }
