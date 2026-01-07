using Delivery.API.Entities;

namespace Delivery.API.Interfaces
{
    public interface IDeliveryService
    {
        Task<DeliveryEntity> CreateDeliveryAsync(Guid orderId, string deliveryAddress, decimal orderTotal);
        Task<DeliveryEntity> AssignCourierAsync(Guid deliveryId);
        Task<DeliveryEntity> UpdateDeliveryStatusAsync(Guid deliveryId, string status, Guid? courierId = null);
        Task<DeliveryEntity> SimulateDeliveryProgressAsync(Guid deliveryId);
        Task<int> CalculateDeliveryTimeAsync(string address, decimal orderTotal);
    }
}