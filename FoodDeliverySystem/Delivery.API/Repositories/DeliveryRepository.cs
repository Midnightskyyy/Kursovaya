using Microsoft.EntityFrameworkCore;
using Delivery.API.Data;
using Delivery.API.Entities;
using Delivery.API.Interfaces;

namespace Delivery.API.Repositories
{
    public class DeliveryRepository : IDeliveryRepository
    {
        private readonly DeliveryDbContext _context;

        public DeliveryRepository(DeliveryDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Courier>> GetAvailableCouriersAsync()
        {
            return await _context.Couriers
                .Where(c => c.IsAvailable)
                .OrderBy(c => c.TotalDeliveries) // Берем курьера с наименьшим количеством доставок
                .ToListAsync();
        }

        public async Task<Courier> GetCourierAsync(Guid courierId)
        {
            return await _context.Couriers.FindAsync(courierId);
        }

        public async Task<Courier> AssignCourierAsync(Guid deliveryId)
        {
            var availableCouriers = await GetAvailableCouriersAsync();
            if (!availableCouriers.Any())
                return null;

            // Выбираем случайного курьера (можно улучшить логику)
            var random = new Random();
            var courier = availableCouriers.ElementAt(random.Next(availableCouriers.Count()));

            // Помечаем курьера как занятого
            courier.IsAvailable = false;
            await _context.SaveChangesAsync();

            return courier;
        }

        public async Task<DeliveryEntity> GetDeliveryAsync(Guid deliveryId)
        {
            return await _context.Deliveries
                .Include(d => d.Courier)
                .FirstOrDefaultAsync(d => d.Id == deliveryId);
        }

        public async Task<DeliveryEntity> GetDeliveryByOrderIdAsync(Guid orderId)
        {
            return await _context.Deliveries
                .Include(d => d.Courier)
                .FirstOrDefaultAsync(d => d.OrderId == orderId);
        }

        public async Task<DeliveryEntity> CreateDeliveryAsync(DeliveryEntity delivery)
        {
            await _context.Deliveries.AddAsync(delivery);
            await _context.SaveChangesAsync();
            return delivery;
        }

        public async Task UpdateDeliveryStatusAsync(Guid deliveryId, string status)
        {
            var delivery = await _context.Deliveries.FindAsync(deliveryId);
            if (delivery != null)
            {
                delivery.Status = status;
                delivery.UpdatedAt = DateTime.UtcNow;

                // Обновляем временные метки в зависимости от статуса
                switch (status)
                {
                    case "Assigned":
                        delivery.AssignedAt = DateTime.UtcNow;
                        break;
                    case "PickedUp":
                        delivery.PickedUpAt = DateTime.UtcNow;
                        break;
                    case "Delivered":
                        delivery.DeliveredAt = DateTime.UtcNow;
                        // Освобождаем курьера
                        if (delivery.CourierId.HasValue)
                        {
                            var courier = await _context.Couriers.FindAsync(delivery.CourierId.Value);
                            if (courier != null)
                            {
                                courier.IsAvailable = true;
                                courier.TotalDeliveries++;
                            }
                        }
                        break;
                    case "Cancelled":
                        // Освобождаем курьера
                        if (delivery.CourierId.HasValue)
                        {
                            var courier = await _context.Couriers.FindAsync(delivery.CourierId.Value);
                            if (courier != null)
                            {
                                courier.IsAvailable = true;
                            }
                        }
                        break;
                }

                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateDeliveryAsync(DeliveryEntity delivery)
        {
            _context.Deliveries.Update(delivery);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<DeliveryEntity>> GetCourierDeliveriesAsync(Guid courierId)
        {
            return await _context.Deliveries
                .Include(d => d.Courier)
                .Where(d => d.CourierId == courierId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<DeliveryEntity>> GetActiveDeliveriesAsync()
        {
            return await _context.Deliveries
                .Include(d => d.Courier)
                .Where(d => d.Status != "Delivered" && d.Status != "Cancelled")
                .OrderBy(d => d.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }
    }
}