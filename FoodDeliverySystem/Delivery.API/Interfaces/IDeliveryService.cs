using Delivery.API.Entities;

namespace Delivery.API.Interfaces
{
    public interface IDeliveryService
    {
        // Основные методы доставки
        Task<DeliveryEntity> CreateDeliveryAsync(Guid orderId, string deliveryAddress, decimal orderTotal, int maxPreparationTime);
        Task<DeliveryEntity> AssignCourierAsync(Guid deliveryId);
        Task<DeliveryEntity> UpdateDeliveryStatusAsync(Guid deliveryId, string status, Guid? courierId = null);
        Task<DeliveryEntity> SimulateDeliveryProgressAsync(Guid deliveryId);
        //Task<int> CalculateDeliveryTimeAsync(string address, decimal orderTotal, int maxPreparationTime);

        // Новый метод для запуска таймера
        Task StartDeliveryTimerAsync(Guid deliveryId);
        Task StopDeliveryTimerAsync(Guid deliveryId);
        // Методы получения данных
        Task<DeliveryEntity> GetDeliveryAsync(Guid deliveryId);
        Task<DeliveryEntity> GetDeliveryByOrderIdAsync(Guid orderId);
    }
}