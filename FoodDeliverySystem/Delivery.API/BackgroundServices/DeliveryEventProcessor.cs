using Delivery.API.Interfaces;
using Shared.Messages.Events;
using Shared.Messages.Interfaces;

namespace Delivery.API.BackgroundServices
{
    public class DeliveryEventProcessor : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessageBusClient _messageBus;
        private readonly ILogger<DeliveryEventProcessor> _logger;

        public DeliveryEventProcessor(
            IServiceProvider serviceProvider,
            IMessageBusClient messageBus,
            ILogger<DeliveryEventProcessor> logger)
        {
            _serviceProvider = serviceProvider;
            _messageBus = messageBus;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 Starting Delivery Event Processor...");

            Task.Delay(5000).Wait();

            try
            {
                // Подписываемся на события заказов
                _messageBus.Subscribe<OrderCreatedEvent>(
                    "delivery.order.created.queue",
                    "order.created",
                    async orderEvent => await ProcessOrderCreatedEvent(orderEvent));

                // Подписываемся на события готовности заказа
                _messageBus.Subscribe<OrderReadyForDeliveryEvent>(
                    "delivery.order.ready.queue",
                    "order.ready",
                    async orderEvent => await ProcessOrderReadyEvent(orderEvent));

                // Подписываемся на события оплаты
                _messageBus.Subscribe<PaymentConfirmedEvent>(
                    "delivery.payment.confirmed.queue",
                    "payment.confirmed",
                    async paymentEvent => await ProcessPaymentConfirmedEvent(paymentEvent));

                // ПОДПИСКА НА ОТМЕНУ ЗАКАЗА (добавьте это)
                _messageBus.Subscribe<OrderCancelledEvent>(
                    "delivery.order.cancelled.queue",
                    "order.cancelled",
                    async cancelledEvent => await ProcessOrderCancelledEvent(cancelledEvent));

                _logger.LogInformation("✅ Delivery Event Processor subscribed to events");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error subscribing to events");
            }

            return Task.CompletedTask;
        }

        // ДОБАВЬТЕ НОВЫЙ МЕТОД ДЛЯ ОБРАБОТКИ ОТМЕНЫ
        private async Task ProcessOrderCancelledEvent(OrderCancelledEvent cancelledEvent)
        {
            try
            {
                _logger.LogInformation("❌ Processing order cancelled event for order {OrderId}",
                    cancelledEvent.OrderId);

                using var scope = _serviceProvider.CreateScope();
                var deliveryRepository = scope.ServiceProvider.GetRequiredService<IDeliveryRepository>();
                var deliveryService = scope.ServiceProvider.GetRequiredService<IDeliveryService>();

                // Ищем доставку для этого заказа
                var delivery = await deliveryRepository.GetDeliveryByOrderIdAsync(cancelledEvent.OrderId);

                if (delivery != null)
                {
                    // Проверяем текущий статус доставки
                    if (delivery.Status != "Cancelled" && delivery.Status != "Delivered")
                    {
                        _logger.LogInformation("🔄 Cancelling delivery {DeliveryId} for order {OrderId}",
                            delivery.Id, cancelledEvent.OrderId);

                        // Останавливаем таймер доставки (ИСПРАВЬТЕ ЭТО!)
                        await deliveryService.StopDeliveryTimerAsync(delivery.Id);

                        // Отменяем доставку
                        await deliveryService.UpdateDeliveryStatusAsync(delivery.Id, "Cancelled");

                        _logger.LogInformation("✅ Delivery {DeliveryId} cancelled successfully", delivery.Id);
                    }
                    else
                    {
                        _logger.LogInformation("⚠️ Delivery {DeliveryId} already in final state: {Status}",
                            delivery.Id, delivery.Status);
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ No delivery found for cancelled order {OrderId}", cancelledEvent.OrderId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing order cancelled event for {OrderId}",
                    cancelledEvent.OrderId);
            }
        }

        private async Task StopDeliveryTimer(Guid deliveryId, IDeliveryService deliveryService)
        {
            try
            {
                // Если в DeliveryService есть активные таймеры, отменяем их
                // Это зависит от вашей реализации таймеров
                _logger.LogInformation("⏹️ Stopping timer for delivery {DeliveryId}", deliveryId);

                // Пример: если вы используете CancellationTokenSource
                // deliveryService.StopTimer(deliveryId);

                // В вашем случае, проверьте реализацию DeliveryTimerService
                // и добавьте метод для отмены таймера
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Error stopping delivery timer");
            }
        }

        private async Task ProcessOrderCreatedEvent(OrderCreatedEvent orderEvent)
        {
            try
            {
                _logger.LogInformation("📦 Processing order created event for order {OrderId}", orderEvent.OrderId);

                using var scope = _serviceProvider.CreateScope();
                var deliveryRepository = scope.ServiceProvider.GetRequiredService<IDeliveryRepository>();
                var deliveryService = scope.ServiceProvider.GetRequiredService<IDeliveryService>();

                // 1. СНАЧАЛА проверяем, не существует ли уже доставка для этого заказа
                var existingDelivery = await deliveryRepository.GetDeliveryByOrderIdAsync(orderEvent.OrderId);
                if (existingDelivery != null)
                {
                    _logger.LogWarning("⚠️ Delivery already exists for order {OrderId}. DeliveryId: {DeliveryId}",
                        orderEvent.OrderId, existingDelivery.Id);
                    return; // Не создаем новую доставку!
                }

                // 2. Только если не существует, создаем новую
                var delivery = await deliveryService.CreateDeliveryAsync(
                    orderEvent.OrderId,
                    orderEvent.DeliveryAddress,
                    orderEvent.TotalAmount,
                    orderEvent.MaxPreparationTime);

                _logger.LogInformation("✅ Delivery created for order {OrderId}: {DeliveryId} (Status: {Status})",
                    orderEvent.OrderId, delivery.Id, delivery.Status);

                // 3. Запускаем таймер
                await deliveryService.StartDeliveryTimerAsync(delivery.Id);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing order created event for {OrderId}",
                    orderEvent.OrderId);
            }
        }

        private async Task ProcessOrderReadyEvent(OrderReadyForDeliveryEvent orderEvent)
        {
            try
            {
                _logger.LogInformation("🔥 Processing order ready event for order {OrderId}", orderEvent.OrderId);

                using var scope = _serviceProvider.CreateScope();
                var deliveryRepository = scope.ServiceProvider.GetRequiredService<IDeliveryRepository>();
                var deliveryService = scope.ServiceProvider.GetRequiredService<IDeliveryService>();

                // Ищем доставку для этого заказа
                var delivery = await deliveryRepository.GetDeliveryByOrderIdAsync(orderEvent.OrderId);
                if (delivery != null && delivery.Status == "Pending")
                {
                    // Теперь можно назначить курьера, так как заказ готов
                    await deliveryService.AssignCourierAsync(delivery.Id);
                    _logger.LogInformation("✅ Courier assignment triggered for order {OrderId} (order is ready)",
                        orderEvent.OrderId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing order ready event for {OrderId}",
                    orderEvent.OrderId);
            }
        }

        private async Task ProcessPaymentConfirmedEvent(PaymentConfirmedEvent paymentEvent)
        {
            try
            {
                _logger.LogInformation("💰 Processing payment confirmed event for order {OrderId}", paymentEvent.OrderId);

                using var scope = _serviceProvider.CreateScope();
                var deliveryRepository = scope.ServiceProvider.GetRequiredService<IDeliveryRepository>();
                var deliveryService = scope.ServiceProvider.GetRequiredService<IDeliveryService>();

                // Ищем доставку для этого заказа
                var delivery = await deliveryRepository.GetDeliveryByOrderIdAsync(paymentEvent.OrderId);

                // Если оплата подтверждена, но заказ еще не готов, просто обновляем статус
                if (delivery != null && delivery.Status == "Pending")
                {
                    // Меняем статус на "Processing" или "AwaitingPreparation"
                    await deliveryService.UpdateDeliveryStatusAsync(delivery.Id, "Processing");
                    _logger.LogInformation("✅ Delivery {DeliveryId} status updated to Processing after payment",
                        delivery.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing payment confirmed event for {OrderId}",
                    paymentEvent.OrderId);
            }
        }
    }
}