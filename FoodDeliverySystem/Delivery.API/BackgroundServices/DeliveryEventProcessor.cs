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
            // Подписываемся на события заказов
            _messageBus.Subscribe<OrderCreatedEvent>(
                "delivery.order.created.queue",
                "order.events",
                "order.created",
                async orderEvent => await ProcessOrderCreatedEvent(orderEvent));

            // Подписываемся на события оплаты
            _messageBus.Subscribe<PaymentConfirmedEvent>(
                "delivery.payment.confirmed.queue",
                "payment.events",
                "payment.confirmed",
                async paymentEvent => await ProcessPaymentConfirmedEvent(paymentEvent));

            _logger.LogInformation("Delivery event processor started");

            return Task.CompletedTask;
        }

        private async Task ProcessOrderCreatedEvent(OrderCreatedEvent orderEvent)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var deliveryService = scope.ServiceProvider.GetRequiredService<IDeliveryService>();

                // Создаем доставку для заказа
                var delivery = await deliveryService.CreateDeliveryAsync(
                    orderEvent.OrderId,
                    orderEvent.DeliveryAddress,
                    orderEvent.TotalAmount);

                _logger.LogInformation("Delivery created for order {OrderId}: {DeliveryId}",
                    orderEvent.OrderId, delivery.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order created event for {OrderId}",
                    orderEvent.OrderId);
            }
        }

        private async Task ProcessPaymentConfirmedEvent(PaymentConfirmedEvent paymentEvent)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var deliveryRepository = scope.ServiceProvider.GetRequiredService<IDeliveryRepository>();

                // Ищем доставку для этого заказа
                var delivery = await deliveryRepository.GetDeliveryByOrderIdAsync(paymentEvent.OrderId);
                if (delivery != null && delivery.Status == "Pending")
                {
                    var deliveryService = scope.ServiceProvider.GetRequiredService<IDeliveryService>();

                    // Пытаемся назначить курьера после подтверждения оплаты
                    await deliveryService.AssignCourierAsync(delivery.Id);

                    _logger.LogInformation("Courier assignment triggered for order {OrderId} after payment",
                        paymentEvent.OrderId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment confirmed event for {OrderId}",
                    paymentEvent.OrderId);
            }
        }
    }
}