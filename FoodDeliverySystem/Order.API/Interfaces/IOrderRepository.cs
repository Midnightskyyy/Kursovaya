using Order.API.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Order.API.Interfaces
{
    public interface IOrderRepository
    {
        // Рестораны и блюда
        Task<IEnumerable<Restaurant>> GetRestaurantsAsync();
        Task<Restaurant> GetRestaurantAsync(Guid id);
        Task<IEnumerable<Dish>> GetDishesByRestaurantAsync(Guid restaurantId);
        Task<Dish> GetDishAsync(Guid id);

        // Корзина
        Task<ShoppingCart> GetCartAsync(Guid userId);
        Task AddToCartAsync(Guid userId, Guid dishId, int quantity);
        Task RemoveFromCartAsync(Guid userId, Guid itemId);
        Task UpdateCartItemAsync(Guid userId, Guid itemId, int quantity);
        Task ClearCartAsync(Guid userId);

        // Заказы
        Task<OrderEntity> CreateOrderAsync(Guid userId, string deliveryAddress, string specialInstructions);
        Task<OrderEntity> GetOrderAsync(Guid orderId, Guid userId);
        Task<IEnumerable<OrderEntity>> GetUserOrdersAsync(Guid userId);
        Task UpdateOrderStatusAsync(Guid orderId, string status);
        Task<bool> CancelOrderAsync(Guid orderId, Guid userId);

        // Утилиты
        Task<bool> SaveChangesAsync();
    }
}