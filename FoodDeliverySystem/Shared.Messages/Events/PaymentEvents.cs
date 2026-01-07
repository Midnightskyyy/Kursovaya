namespace Shared.Messages.Events
{
    public class PaymentConfirmedEvent
    {
        public Guid OrderId { get; set; }
        public Guid TransactionId { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaidAt { get; set; }
    }

    public class PaymentFailedEvent
    {
        public Guid OrderId { get; set; }
        public string Reason { get; set; }
        public DateTime FailedAt { get; set; }
    }
}