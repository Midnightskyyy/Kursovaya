using Shared.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace Order.API.Entities
{
    public class Restaurant : BaseEntity
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string ImageUrl { get; set; }
        public bool IsActive { get; set; } = true;

        public decimal AverageRating { get; set; } = 4.5m;
        public int TotalReviews { get; set; } = 0;

        public virtual ICollection<Dish> Dishes { get; set; }
        public virtual ICollection<OrderEntity> Orders { get; set; }
    }

    public class Dish : BaseEntity
    {
        public Guid RestaurantId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string Category { get; set; }
        public string ImageUrl { get; set; }
        public int PreparationTime { get; set; } // minutes
        public bool IsAvailable { get; set; } = true;

        public decimal AverageRating { get; set; } = 4.5m;

        public virtual Restaurant Restaurant { get; set; }
    }

    public class OrderEntity : BaseEntity
    {
        public Guid UserId { get; set; }
        public Guid? RestaurantId { get; set; }
        public string Status { get; set; } = "Preparing"; // Preparing, PickingUp, OnTheWay, Delivered, Cancelled
        public decimal TotalAmount { get; set; }
        public string DeliveryAddress { get; set; }
        public string SpecialInstructions { get; set; }
        public int? EstimatedCookingTime { get; set; } // minutes
        public DateTime? ReadyAt { get; set; }

        public virtual Restaurant Restaurant { get; set; }
        public virtual ICollection<OrderItem> OrderItems { get; set; }
    }

    public class OrderItem : BaseEntity
    {
        public Guid OrderId { get; set; }
        public Guid DishId { get; set; }
        public string DishName { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }

        public virtual OrderEntity Order { get; set; }
    }

    public class ShoppingCart : BaseEntity
    {
        public Guid UserId { get; set; }

        public virtual ICollection<CartItem> CartItems { get; set; }
    }

    public class CartItem : BaseEntity
    {
        public Guid CartId { get; set; }
        public Guid DishId { get; set; }
        public int Quantity { get; set; }
        public Guid RestaurantId { get; set; }


        public virtual ShoppingCart Cart { get; set; }
        public virtual Dish Dish { get; set; }
    }
}