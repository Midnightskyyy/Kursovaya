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
        public string Status { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class DeliveryCompletedEvent
    {
        public Guid OrderId { get; set; }
        public Guid DeliveryId { get; set; }
        public DateTime DeliveredAt { get; set; }
    }
}