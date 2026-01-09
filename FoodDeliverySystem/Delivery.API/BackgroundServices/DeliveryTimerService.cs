using Delivery.API.Entities;
using Delivery.API.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Delivery.API.BackgroundServices
{
    public class DeliveryTimerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DeliveryTimerService> _logger;
        private readonly Dictionary<Guid, DeliveryTimerState> _timerStates = new();

        public DeliveryTimerService(
            IServiceProvider serviceProvider,
            ILogger<DeliveryTimerService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 Delivery Timer Service started - Processing deliveries");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var deliveryRepository = scope.ServiceProvider.GetRequiredService<IDeliveryRepository>();
                    var deliveryService = scope.ServiceProvider.GetRequiredService<IDeliveryService>();

                    // Получаем активные доставки
                    var activeDeliveries = await deliveryRepository.GetActiveDeliveriesAsync();

                    _logger.LogInformation("📊 Active deliveries count: {Count}", activeDeliveries.Count());

                    foreach (var delivery in activeDeliveries)
                    {
                        try
                        {
                            _logger.LogInformation("📦 Processing delivery {DeliveryId}: Status={Status}",
                                delivery.Id, delivery.Status);

                            // ОБРАБАТЫВАЕМ КАЖДУЮ ДОСТАВКУ
                            await ProcessDeliveryAsync(delivery, deliveryService, deliveryRepository);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "❌ Error processing delivery {DeliveryId}", delivery.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in delivery timer service");
                }

                // Проверяем каждые 10 секунд (чаще для точности)
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        private async Task ProcessDeliveryAsync(
    DeliveryEntity delivery,
    IDeliveryService deliveryService,
    IDeliveryRepository deliveryRepository)
        {
            var now = DateTime.UtcNow;

            // Проверяем, не отменена ли доставка
            if (delivery.Status == "Cancelled")
            {
                _logger.LogInformation("🛑 Delivery {DeliveryId} is cancelled, skipping processing", delivery.Id);
                return;
            }

            // 1. Если заказ в статусе "Preparing" и время приготовления прошло
            if (delivery.Status == "Preparing" && delivery.PreparationStartedAt.HasValue)
            {
                var elapsedPreparation = (now - delivery.PreparationStartedAt.Value).TotalMinutes;

                if (elapsedPreparation >= delivery.PreparationTimeMinutes)
                {
                    _logger.LogInformation("✅ Время приготовления прошло для доставки {DeliveryId}", delivery.Id);

                    // Используем UpdateDeliveryStatusAsync для публикации событий
                    await deliveryService.UpdateDeliveryStatusAsync(delivery.Id, "PickingUp");

                    // Пытаемся сразу назначить курьера
                    await TryAssignCourierAndUpdateStatusAsync(delivery, deliveryService, deliveryRepository);
                }
            }
            // 2. Если заказ в статусе "PickingUp"
            else if (delivery.Status == "PickingUp")
            {
                _logger.LogInformation("🕒 Доставка {DeliveryId} ожидает курьера", delivery.Id);

                // Пытаемся назначить курьера
                await TryAssignCourierAndUpdateStatusAsync(delivery, deliveryService, deliveryRepository);
            }
            // 3. Если заказ в статусе "OnTheWay" и время доставки прошло
            else if (delivery.Status == "OnTheWay" && delivery.DeliveryStartedAt.HasValue)
            {
                var elapsedDelivery = (now - delivery.DeliveryStartedAt.Value).TotalMinutes;

                if (elapsedDelivery >= delivery.DeliveryTimeMinutes)
                {
                    _logger.LogInformation("✅ Время доставки прошло для {DeliveryId}", delivery.Id);
                    await deliveryService.UpdateDeliveryStatusAsync(delivery.Id, "Delivered");
                }
            }
        }
        private async Task TryAssignCourierAndUpdateStatusAsync(
    DeliveryEntity delivery,
    IDeliveryService deliveryService,
    IDeliveryRepository deliveryRepository)
        {
            try
            {
                // Назначаем курьера
                var courier = await deliveryRepository.AssignCourierToDeliveryAsync(delivery.Id);
                if (courier != null)
                {
                    _logger.LogInformation("✅ Курьер {CourierName} назначен на доставку {DeliveryId}",
                        courier.Name, delivery.Id);

                    // Переводим в статус OnTheWay
                    await UpdateToOnTheWayAsync(delivery, deliveryService, deliveryRepository, courier);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Не удалось назначить курьера для доставки {DeliveryId}", delivery.Id);
            }
        }

        private async Task UpdateToOnTheWayAsync(
            DeliveryEntity delivery,
            IDeliveryService deliveryService,
            IDeliveryRepository deliveryRepository,
            Courier courier = null)
        {
            try
            {
                // Если курьер не передан, получаем его из доставки
                if (courier == null && delivery.CourierId.HasValue)
                {
                    courier = await deliveryRepository.GetCourierAsync(delivery.CourierId.Value);
                }

                if (courier == null)
                {
                    _logger.LogWarning("⚠️ Не могу обновить статус без данных о курьере для доставки {DeliveryId}", delivery.Id);
                    return;
                }

                // Переводим в статус OnTheWay
                await deliveryService.UpdateDeliveryStatusAsync(delivery.Id, "OnTheWay", courier.Id);

                // Запускаем таймер доставки, если еще не запущен
                if (!delivery.DeliveryStartedAt.HasValue)
                {
                    delivery.DeliveryStartedAt = DateTime.UtcNow;
                    await deliveryRepository.UpdateDeliveryAsync(delivery);

                    _logger.LogInformation("⏰ Таймер доставки запущен для {DeliveryId}", delivery.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка при обновлении статуса на OnTheWay для доставки {DeliveryId}", delivery.Id);
            }
        }

        private async Task TryAssignCourierAsync(Guid deliveryId, IDeliveryService deliveryService)
        {
            try
            {
                // Получаем доставку
                using var scope = _serviceProvider.CreateScope();
                var deliveryRepository = scope.ServiceProvider.GetRequiredService<IDeliveryRepository>();
                var delivery = await deliveryRepository.GetDeliveryAsync(deliveryId);

                if (delivery == null || delivery.CourierId.HasValue) return;

                // Назначаем курьера
                var courier = await deliveryRepository.AssignCourierToDeliveryAsync(deliveryId);
                if (courier != null)
                {
                    _logger.LogInformation("✅ Курьер {CourierName} назначен на доставку {DeliveryId}",
                        courier.Name, deliveryId);

                    // Переводим в статус OnTheWay
                    await deliveryService.UpdateDeliveryStatusAsync(deliveryId, "OnTheWay", courier.Id);

                    // Запускаем таймер доставки
                    delivery.DeliveryStartedAt = DateTime.UtcNow;
                    await deliveryRepository.UpdateDeliveryAsync(delivery);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Не удалось назначить курьера для доставки {DeliveryId}", deliveryId);
            }
        }
    }

    public class DeliveryTimerState
    {
        public DateTime? PreparationStartedAt { get; set; }
        public DateTime? DeliveryStartedAt { get; set; }
        public bool IsWaitingForCourier { get; set; }
    }
}