using Delivery.API.Interfaces;
using Delivery.API.Entities;
using Shared.Messages.Events;
using Shared.Messages.Interfaces;

namespace Delivery.API.Services
{
    public class DeliveryService : IDeliveryService
    {
        private readonly IDeliveryRepository _deliveryRepository;
        private readonly IMessageBusClient _messageBus;
        private readonly ILogger<DeliveryService> _logger;

        public DeliveryService(
            IDeliveryRepository deliveryRepository,
            IMessageBusClient messageBus,
            ILogger<DeliveryService> logger)
        {
            _deliveryRepository = deliveryRepository;
            _messageBus = messageBus;
            _logger = logger;
        }

        public async Task<DeliveryEntity> CreateDeliveryAsync(Guid orderId, string deliveryAddress, decimal orderTotal)
        {
            try
            {
                // Используем await для получения результата Task<int>
                var deliveryTime = await CalculateDeliveryTimeAsync(deliveryAddress, orderTotal);

                var delivery = new DeliveryEntity
                {
                    OrderId = orderId,
                    DeliveryAddress = deliveryAddress,
                    Status = "Pending",
                    EstimatedDurationMinutes = deliveryTime,
                    EstimatedDeliveryTime = DateTime.UtcNow.AddMinutes(deliveryTime)
                };

                var createdDelivery = await _deliveryRepository.CreateDeliveryAsync(delivery);

                _logger.LogInformation("Delivery created for order {OrderId}", orderId);

                return createdDelivery;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating delivery for order {OrderId}", orderId);
                throw;
            }
        }

        public async Task<DeliveryEntity> AssignCourierAsync(Guid deliveryId)
        {
            try
            {
                var delivery = await _deliveryRepository.GetDeliveryAsync(deliveryId);
                if (delivery == null)
                    throw new ArgumentException($"Delivery {deliveryId} not found");

                if (delivery.Status != "Pending")
                    throw new InvalidOperationException($"Delivery already has status: {delivery.Status}");

                // Назначаем курьера
                var courier = await _deliveryRepository.AssignCourierAsync(deliveryId);
                if (courier == null)
                    throw new InvalidOperationException("No available couriers");

                delivery.CourierId = courier.Id;
                delivery.Status = "Assigned";
                delivery.AssignedAt = DateTime.UtcNow;

                await _deliveryRepository.UpdateDeliveryAsync(delivery);

                // Публикуем событие
                _messageBus.Publish(new DeliveryAssignedEvent
                {
                    OrderId = delivery.OrderId,
                    DeliveryId = delivery.Id,
                    CourierId = courier.Id,
                    CourierName = courier.Name,
                    EstimatedDeliveryTime = delivery.EstimatedDeliveryTime ?? DateTime.UtcNow.AddMinutes(30),
                    AssignedAt = delivery.AssignedAt.Value
                }, "delivery.events", "delivery.assigned");

                _logger.LogInformation("Courier {CourierId} assigned to delivery {DeliveryId}",
                    courier.Id, deliveryId);

                return delivery;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning courier to delivery {DeliveryId}", deliveryId);
                throw;
            }
        }

        public async Task<DeliveryEntity> UpdateDeliveryStatusAsync(Guid deliveryId, string status, Guid? courierId = null)
        {
            try
            {
                var delivery = await _deliveryRepository.GetDeliveryAsync(deliveryId);
                if (delivery == null)
                    throw new ArgumentException($"Delivery {deliveryId} not found");

                // Проверяем права курьера (если указан)
                if (courierId.HasValue && delivery.CourierId != courierId)
                    throw new UnauthorizedAccessException("Courier not authorized for this delivery");

                var oldStatus = delivery.Status;
                delivery.Status = status;

                // Обновляем временные метки
                switch (status)
                {
                    case "PickedUp":
                        delivery.PickedUpAt = DateTime.UtcNow;
                        break;
                    case "Delivered":
                        delivery.DeliveredAt = DateTime.UtcNow;
                        break;
                }

                await _deliveryRepository.UpdateDeliveryAsync(delivery);

                // Публикуем событие изменения статуса
                _messageBus.Publish(new DeliveryStatusChangedEvent
                {
                    OrderId = delivery.OrderId,
                    DeliveryId = delivery.Id,
                    Status = status,
                    ChangedAt = DateTime.UtcNow
                }, "delivery.events", "delivery.status.changed");

                // Если доставка завершена, публикуем отдельное событие
                if (status == "Delivered")
                {
                    _messageBus.Publish(new DeliveryCompletedEvent
                    {
                        OrderId = delivery.OrderId,
                        DeliveryId = delivery.Id,
                        DeliveredAt = delivery.DeliveredAt.Value
                    }, "delivery.events", "delivery.completed");
                }

                _logger.LogInformation("Delivery {DeliveryId} status changed from {OldStatus} to {NewStatus}",
                    deliveryId, oldStatus, status);

                return delivery;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating delivery {DeliveryId} status to {Status}",
                    deliveryId, status);
                throw;
            }
        }

        public async Task<DeliveryEntity> SimulateDeliveryProgressAsync(Guid deliveryId)
        {
            var delivery = await _deliveryRepository.GetDeliveryAsync(deliveryId);
            if (delivery == null || delivery.Status == "Delivered" || delivery.Status == "Cancelled")
                return delivery;

            // Симуляция прогресса доставки
            var random = new Random();
            var progressChance = random.Next(100);

            switch (delivery.Status)
            {
                case "Pending":
                    if (progressChance < 80) // 80% шанс назначения курьера
                    {
                        try
                        {
                            return await AssignCourierAsync(deliveryId);
                        }
                        catch
                        {
                            // Не удалось назначить курьера
                            return delivery;
                        }
                    }
                    break;

                case "Assigned":
                    if (progressChance < 70) // 70% шанс что курьер забрал заказ
                    {
                        await UpdateDeliveryStatusAsync(deliveryId, "PickedUp");
                    }
                    break;

                case "PickedUp":
                    if (progressChance < 60) // 60% шанс доставки
                    {
                        await UpdateDeliveryStatusAsync(deliveryId, "Delivered");
                    }
                    else if (progressChance < 80) // 20% шанс что в пути
                    {
                        await UpdateDeliveryStatusAsync(deliveryId, "OnTheWay");
                    }
                    break;

                case "OnTheWay":
                    if (progressChance < 90) // 90% шанс доставки
                    {
                        await UpdateDeliveryStatusAsync(deliveryId, "Delivered");
                    }
                    break;
            }

            return await _deliveryRepository.GetDeliveryAsync(deliveryId);
        }

        public async Task<int> CalculateDeliveryTimeAsync(string address, decimal orderTotal)
        {
            // Простая симуляция расчета времени доставки
            var random = new Random();

            // Базовое время: 15-45 минут в зависимости от "сложности адреса"
            var baseTime = 15 + random.Next(30);

            // Добавляем время в зависимости от суммы заказа (больше заказ = больше времени на упаковку)
            var orderTime = (int)(orderTotal / 10); // 1 минута на каждые 10 единиц суммы

            // Итоговое время
            var totalTime = baseTime + orderTime;

            // Максимум 90 минут
            return Math.Min(totalTime, 90);
        }
    }
}