namespace Shared.Messages.Events
{
    public class OrderCreatedEvent
    {
        public Guid OrderId { get; set; }
        public Guid UserId { get; set; }
        public Guid RestaurantId { get; set; }
        public decimal TotalAmount { get; set; }
        public string DeliveryAddress { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<OrderItemDto> Items { get; set; } = new();
    }

    public class OrderItemDto
    {
        public Guid DishId { get; set; }
        public string DishName { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
    }

    public class OrderStatusChangedEvent
    {
        public Guid OrderId { get; set; }
        public string OldStatus { get; set; }
        public string NewStatus { get; set; }
        public DateTime ChangedAt { get; set; }
    }
}