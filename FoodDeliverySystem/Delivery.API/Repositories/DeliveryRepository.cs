using Microsoft.EntityFrameworkCore;
using Delivery.API.Data;
using Delivery.API.Entities;
using Delivery.API.Interfaces;
using Npgsql;
using Delivery.API.Services;

namespace Delivery.API.Repositories
{
    public class DeliveryRepository : IDeliveryRepository
    {
        private readonly DeliveryDbContext _context;
        private readonly ILogger<DeliveryService> _logger;

        public DeliveryRepository(DeliveryDbContext context, ILogger<DeliveryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<Courier>> GetAvailableCouriersAsync()
        {
            return await _context.Couriers
                .Where(c => c.IsAvailable)
                .OrderBy(c => c.TotalDeliveries)
                .ToListAsync();
        }

        public async Task<Courier> GetCourierAsync(Guid courierId)
        {
            return await _context.Couriers
                .FirstOrDefaultAsync(c => c.Id == courierId);
        }

        public async Task<Courier> AssignCourierToDeliveryAsync(Guid deliveryId)
        {
            try
            {
                var availableCouriers = await GetAvailableCouriersAsync();
                if (!availableCouriers.Any())
                    return null;

                var random = new Random();
                var courier = availableCouriers.ElementAt(random.Next(availableCouriers.Count()));

                // Находим доставку
                var delivery = await _context.Deliveries.FindAsync(deliveryId);
                if (delivery != null)
                {
                    // ИСПРАВЛЕНИЕ: Меняем доступность только у выбранного курьера
                    delivery.CourierId = courier.Id;
                    courier.IsAvailable = false; // Только этот курьер становится недоступен
                    courier.UpdatedAt = DateTime.UtcNow;

                    // Сохраняем изменения
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("✅ Курьер {CourierName} (ID: {CourierId}) назначен на доставку {DeliveryId}",
                        courier.Name, courier.Id, deliveryId);
                }

                return courier;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка назначения курьера для доставки {DeliveryId}", deliveryId);
                return null;
            }
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
            _logger.LogInformation("💾 Repository: Creating delivery for OrderId: {OrderId}", delivery.OrderId);

            try
            {
                // Используем транзакцию с высоким уровнем изоляции
                using var transaction = await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

                try
                {
                    // Проверяем существование с блокировкой
                    var existing = await _context.Deliveries
                        .FirstOrDefaultAsync(d => d.OrderId == delivery.OrderId);

                    if (existing != null)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogWarning("⚠️ Repository: Delivery already exists for OrderId {OrderId} (in transaction)",
                            delivery.OrderId);
                        return existing;
                    }

                    // Сохраняем
                    await _context.Deliveries.AddAsync(delivery);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("✅ Repository: Delivery saved. DeliveryId: {DeliveryId}", delivery.Id);
                    return delivery;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx &&
                                               pgEx.SqlState == "23505") // unique_violation
            {
                _logger.LogWarning("⚠️ Repository: Unique constraint violation for OrderId {OrderId}", delivery.OrderId);

                // Возвращаем существующую
                var existing = await _context.Deliveries
                    .FirstOrDefaultAsync(d => d.OrderId == delivery.OrderId);

                return existing ?? throw new Exception($"Failed to create delivery for order {delivery.OrderId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Repository: Error creating delivery");
                throw;
            }
        }

        public async Task UpdateDeliveryStatusAsync(Guid deliveryId, string status)
{
    var delivery = await _context.Deliveries
        .Include(d => d.Courier)
        .FirstOrDefaultAsync(d => d.Id == deliveryId);
    
    if (delivery == null) return;

    var oldStatus = delivery.Status;
    delivery.Status = status;
    delivery.UpdatedAt = DateTime.UtcNow;

    _logger.LogInformation("📦 Обновление статуса доставки {DeliveryId}: {OldStatus} -> {NewStatus}",
        deliveryId, oldStatus, status);

    // При завершении доставки или отмене освобождаем курьера
    if ((status == "Delivered" || status == "Cancelled") && delivery.CourierId.HasValue)
    {
        var courier = delivery.Courier;
        if (courier != null)
        {
            courier.IsAvailable = true;
            courier.UpdatedAt = DateTime.UtcNow;
            
            if (status == "Delivered")
            {
                courier.TotalDeliveries++;
                _logger.LogInformation("✅ Курьер {CourierName} выполнил доставку. Всего доставок: {Total}",
                    courier.Name, courier.TotalDeliveries);
            }
            else if (status == "Cancelled")
            {
                _logger.LogInformation("✅ Курьер {CourierName} освобожден (заказ отменен)",
                    courier.Name);
            }
        }
        else
        {
            _logger.LogWarning("⚠️ Курьер не найден для доставки {DeliveryId}", deliveryId);
        }
    }

    // Устанавливаем временные метки
    switch (status)
    {
        case "PickingUp":
            break;
        case "OnTheWay":
            if (!delivery.DeliveryStartedAt.HasValue)
            {
                delivery.DeliveryStartedAt = DateTime.UtcNow;
                _logger.LogInformation("⏱️ Таймер доставки запущен");
            }
            break;
        case "Delivered":
            break;
        case "Cancelled":
            _logger.LogInformation("❌ Доставка отменена");
            break;
    }

    await _context.SaveChangesAsync();
    _logger.LogInformation("💾 Статус сохранен в БД");
}

        public async Task UpdateDeliveryAsync(DeliveryEntity delivery)
        {
            // Проверяем, нужно ли освободить курьера
            if ((delivery.Status == "Delivered" || delivery.Status == "Cancelled")
                && delivery.CourierId.HasValue)
            {
                var courier = await _context.Couriers.FindAsync(delivery.CourierId.Value);
                if (courier != null)
                {
                    courier.IsAvailable = true;
                    courier.UpdatedAt = DateTime.UtcNow;

                    if (delivery.Status == "Delivered")
                    {
                        courier.TotalDeliveries++;
                    }
                }
            }

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