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
            return await _context.ShoppingCarts
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Dish)
                .Include(c => c.Restaurant)
                .FirstOrDefaultAsync(c => c.UserId == userId);
        }

        public async Task AddToCartAsync(Guid userId, Guid dishId, int quantity)
        {
            var cart = await GetOrCreateCartAsync(userId);
            var dish = await _context.Dishes.FindAsync(dishId);

            if (dish == null || !dish.IsAvailable)
                throw new ArgumentException("Dish not available");

            // Проверяем, можно ли добавлять блюда только из одного ресторана
            if (cart.RestaurantId.HasValue && cart.RestaurantId != dish.RestaurantId)
            {
                // Очищаем корзину перед добавлением блюда из другого ресторана
                _context.CartItems.RemoveRange(cart.CartItems);
                cart.RestaurantId = dish.RestaurantId;
            }
            else if (!cart.RestaurantId.HasValue)
            {
                cart.RestaurantId = dish.RestaurantId;
            }

            var existingItem = cart.CartItems.FirstOrDefault(ci => ci.DishId == dishId);
            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                cart.CartItems.Add(new CartItem
                {
                    DishId = dishId,
                    Quantity = quantity
                });
            }

            await _context.SaveChangesAsync();
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
                    cart.RestaurantId = null;
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
                cart.RestaurantId = null;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<OrderEntity> CreateOrderAsync(Guid userId, string deliveryAddress, string specialInstructions)
        {
            var cart = await GetCartAsync(userId);
            if (cart == null || !cart.CartItems.Any())
                throw new InvalidOperationException("Cart is empty");

            var restaurant = await _context.Restaurants.FindAsync(cart.RestaurantId);
            if (restaurant == null || !restaurant.IsActive)
                throw new InvalidOperationException("Restaurant not available");

            // Создаем заказ
            var order = new OrderEntity
            {
                UserId = userId,
                RestaurantId = restaurant.Id,
                DeliveryAddress = deliveryAddress,
                SpecialInstructions = specialInstructions,
                Status = "Pending"
            };

            // Добавляем позиции заказа
            decimal totalAmount = 0;
            int maxPreparationTime = 0;

            foreach (var cartItem in cart.CartItems)
            {
                var dish = await _context.Dishes.FindAsync(cartItem.DishId);
                if (dish == null || !dish.IsAvailable)
                    throw new InvalidOperationException($"Dish {cartItem.DishId} not available");

                order.OrderItems.Add(new OrderItem
                {
                    DishId = dish.Id,
                    DishName = dish.Name,
                    UnitPrice = dish.Price,
                    Quantity = cartItem.Quantity
                });

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

            // Загружаем связанные данные для возврата
            await _context.Entry(order)
                .Reference(o => o.Restaurant)
                .LoadAsync();

            await _context.Entry(order)
                .Collection(o => o.OrderItems)
                .LoadAsync();

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
            return await _context.Orders
                .Include(o => o.Restaurant)
                .Include(o => o.OrderItems)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
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

            if (order == null || order.Status != "Pending")
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
            var cart = await GetCartAsync(userId);
            if (cart == null)
            {
                cart = new ShoppingCart { UserId = userId };
                await _context.ShoppingCarts.AddAsync(cart);
                await _context.SaveChangesAsync();
            }
            return cart;
        }
    }
}
