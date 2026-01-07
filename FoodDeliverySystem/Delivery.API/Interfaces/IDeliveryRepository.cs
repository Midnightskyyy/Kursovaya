using Delivery.API.Entities;

namespace Delivery.API.Interfaces
{
    public interface IDeliveryRepository
    {
        // Курьеры
        Task<IEnumerable<Courier>> GetAvailableCouriersAsync();
        Task<Courier> GetCourierAsync(Guid courierId);
        Task<Courier> AssignCourierAsync(Guid deliveryId);

        // Доставки
        Task<DeliveryEntity> GetDeliveryAsync(Guid deliveryId);
        Task<DeliveryEntity> GetDeliveryByOrderIdAsync(Guid orderId);
        Task<DeliveryEntity> CreateDeliveryAsync(DeliveryEntity delivery);
        Task UpdateDeliveryStatusAsync(Guid deliveryId, string status);
        Task UpdateDeliveryAsync(DeliveryEntity delivery);
        Task<IEnumerable<DeliveryEntity>> GetCourierDeliveriesAsync(Guid courierId);
        Task<IEnumerable<DeliveryEntity>> GetActiveDeliveriesAsync();

        // Утилиты
        Task<bool> SaveChangesAsync();
    }
}