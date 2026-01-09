namespace Shared.Messages.Events
{
    public class DeliveryAssignedEvent
    {
        public Guid OrderId { get; set; }
        public Guid DeliveryId { get; set; }
        public Guid CourierId { get; set; }
        public string CourierName { get; set; }
        public DateTime EstimatedDeliveryTime { get; set; }
        public DateTime AssignedAt { get; set; }
    }

    public class DeliveryStatusChangedEvent
    {
        public Guid OrderId { get; set; }
        public Guid DeliveryId { get; set; }
        public string NewStatus { get; set; }
        public string OldStatus { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class DeliveryCancelledEvent
    {
        public Guid OrderId { get; set; }
        public Guid DeliveryId { get; set; }
        public DateTime CancelledAt { get; set; }
        public string Reason { get; set; }
    }

    public class OrderStatusUpdateEvent
    {
        public Guid OrderId { get; set; }
        public string Status { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class DeliveryCreatedEvent
    {
        public Guid OrderId { get; set; }
        public Guid DeliveryId { get; set; }
        public DateTime EstimatedDeliveryTime { get; set; }

        // Добавляем новые поля для времени
        public int TotalMinutes { get; set; }
        public int PreparationMinutes { get; set; }
        public int DeliveryMinutes { get; set; }

        // Дополнительная информация
        public string DeliveryAddress { get; set; }
        public decimal OrderTotal { get; set; }
    }
    public class OrderReadyForDeliveryEvent
    {
        public Guid OrderId { get; set; }
        public Guid RestaurantId { get; set; }
        public DateTime ReadyAt { get; set; }
        public int PreparationTime { get; set; }
    }
    public class DeliveryCompletedEvent
    {
        public Guid OrderId { get; set; }
        public Guid DeliveryId { get; set; }
        public DateTime DeliveredAt { get; set; }
    }
}