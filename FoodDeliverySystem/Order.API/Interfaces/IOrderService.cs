using Order.API.Controllers;
using Order.API.Entities;
using System.Threading.Tasks;

namespace Order.API.Interfaces
{
    public interface IOrderService
    {
        Task<OrderEntity> CreateOrderAsync(Guid userId, CreateOrderRequest request);
        Task<bool> CancelOrderAsync(Guid orderId, Guid userId);
        Task<decimal> CalculateOrderTotalAsync(Guid userId);
        Task ValidateOrderAsync(Guid userId);
    }
}