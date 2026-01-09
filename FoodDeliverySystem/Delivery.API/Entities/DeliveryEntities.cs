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
        public string Status { get; set; } = "Pending"; // Pending, Preparing, PickingUp, OnTheWay, Delivered, Cancelled

        [Required, MaxLength(500)]
        public string DeliveryAddress { get; set; }

        public DateTime? EstimatedDeliveryTime { get; set; }

        [Range(0, 300)]
        public int EstimatedDurationMinutes { get; set; } // Оценочное время доставки в минутах

        [Range(0, 180)]
        public int PreparationTimeMinutes { get; set; } = 30; // Время приготовления

        [Range(0, 180)]
        public int DeliveryTimeMinutes { get; set; } = 15; // Время доставки

        public DateTime? PreparationStartedAt { get; set; }
        public DateTime? DeliveryStartedAt { get; set; }

        [MaxLength(1000)]
        public string Notes { get; set; }

        public virtual Courier Courier { get; set; }
    }
}