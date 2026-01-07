using Shared.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace Delivery.API.Entities
{
    public class Courier : BaseEntity
    {
        public Guid UserId { get; set; } // Ссылка на пользователя в Auth сервисе

        [Required, MaxLength(255)]
        public string Name { get; set; }

        [MaxLength(20)]
        public string PhoneNumber { get; set; }

        [Required, MaxLength(50)]
        public string VehicleType { get; set; } = "Bicycle"; // Bicycle, Motorcycle, Car

        [Required]
        public bool IsAvailable { get; set; } = true;

        [Range(0, 5)]
        public decimal Rating { get; set; } = 4.5m;

        public int TotalDeliveries { get; set; } = 0;

        public virtual ICollection<DeliveryEntity> Deliveries { get; set; }
    }

    public class DeliveryEntity : BaseEntity
    {
        public Guid OrderId { get; set; }
        public Guid? CourierId { get; set; }

        [Required, MaxLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, Assigned, PickedUp, OnTheWay, Delivered, Cancelled

        [Required, MaxLength(500)]
        public string DeliveryAddress { get; set; }

        [MaxLength(500)]
        public string PickupAddress { get; set; }

        public DateTime? EstimatedDeliveryTime { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? PickedUpAt { get; set; }
        public DateTime? DeliveredAt { get; set; }

        [Range(0, 300)]
        public int? EstimatedDurationMinutes { get; set; } // Оценочное время доставки в минутах

        [MaxLength(1000)]
        public string Notes { get; set; }

        public virtual Courier Courier { get; set; }
    }
}