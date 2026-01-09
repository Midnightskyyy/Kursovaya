using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Order.API.Interfaces;
using Shared.Messages.Events;
using Shared.Messages.Interfaces;

namespace Order.API.BackgroundServices
{
    public class OrderEventProcessor : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessageBusClient _messageBus;
        private readonly ILogger<OrderEventProcessor> _logger;

        public OrderEventProcessor(
            IServiceProvider serviceProvider,
            IMessageBusClient messageBus,
            ILogger<OrderEventProcessor> logger)
        {
            _serviceProvider = serviceProvider;
            _messageBus = messageBus;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 Starting Order Event Processor...");

            // Используем await вместо Task.Delay().Wait()
            await Task.Delay(5000, stoppingToken);

            try
            {
                // Подписываемся на события изменения статуса доставки
                _messageBus.Subscribe<DeliveryStatusChangedEvent>(
                    "order.delivery.status.queue",
                    "delivery.status.changed",
                    async deliveryEvent => await ProcessDeliveryStatusChanged(deliveryEvent));

                // Подписываемся на события обновления статуса заказа (для синхронизации)
                _messageBus.Subscribe<OrderStatusUpdatedEvent>(
                    "order.status.updated.queue",
                    "order.status.updated",
                    async orderEvent => await ProcessOrderStatusUpdate(orderEvent));

                _logger.LogInformation("✅ Order Event Processor subscribed to events");

                // Ждем отмены
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "❌ Error subscribing to events");
            }
        }

        private async Task ProcessDeliveryStatusChanged(DeliveryStatusChangedEvent deliveryEvent)
        {
            try
            {
                _logger.LogInformation("🔄 Processing delivery status change for order {OrderId}: {NewStatus}",
                    deliveryEvent.OrderId, deliveryEvent.NewStatus);

                using var scope = _serviceProvider.CreateScope();
                var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();

                // Маппим статусы доставки на статусы заказа
                var orderStatus = MapDeliveryStatusToOrderStatus(deliveryEvent.NewStatus);

                if (orderStatus != null)
                {
                    // Обновляем статус заказа
                    var success = await orderService.UpdateOrderStatusAsync(deliveryEvent.OrderId, orderStatus);

                    if (success)
                    {
                        _logger.LogInformation("✅ Order {OrderId} status updated to {OrderStatus} (from delivery status {DeliveryStatus})",
                            deliveryEvent.OrderId, orderStatus, deliveryEvent.NewStatus);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Failed to update order {OrderId} status", deliveryEvent.OrderId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing delivery status change for {OrderId}",
                    deliveryEvent.OrderId);
            }
        }

        private async Task ProcessOrderStatusUpdate(OrderStatusUpdatedEvent orderEvent)
        {
            try
            {
                _logger.LogInformation("🔄 Processing order status update for order {OrderId}: {Status}",
                    orderEvent.OrderId, orderEvent.Status);

                using var scope = _serviceProvider.CreateScope();
                var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

                // Получаем заказ
                var order = await orderRepository.GetOrderAsync(orderEvent.OrderId, orderEvent.OrderId);
                if (order != null && order.Status != orderEvent.Status)
                {
                    // Обновляем статус заказа
                    order.Status = orderEvent.Status;
                    order.UpdatedAt = DateTime.UtcNow;

                    // Устанавливаем время готовности если заказ перешел в PickingUp
                    if (orderEvent.Status == "PickingUp" && !order.ReadyAt.HasValue)
                    {
                        order.ReadyAt = DateTime.UtcNow;
                    }

                    // Сохраняем изменения
                    await orderRepository.SaveChangesAsync();

                    _logger.LogInformation("✅ Статус заказа {OrderId} обновлен: {Status}",
                        orderEvent.OrderId, orderEvent.Status);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing order status update for {OrderId}",
                    orderEvent.OrderId);
            }
        }

        private string? MapDeliveryStatusToOrderStatus(string deliveryStatus)
        {
            var statusMap = new Dictionary<string, string>
            {
                ["Preparing"] = "Preparing",
                ["PickingUp"] = "PickingUp",
                ["OnTheWay"] = "OnTheWay",
                ["Delivered"] = "Delivered",
                ["Cancelled"] = "Cancelled"
            };

            if (statusMap.TryGetValue(deliveryStatus, out var orderStatus))
            {
                _logger.LogDebug("Mapped '{DeliveryStatus}' -> '{OrderStatus}'", deliveryStatus, orderStatus);
                return orderStatus;
            }

            _logger.LogWarning("No mapping found for delivery status: {DeliveryStatus}", deliveryStatus);
            return null;
        }
    }
}