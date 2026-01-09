using Delivery.API.Interfaces;
using Delivery.API.Entities;
using Shared.Messages.Events;
using Shared.Messages.Interfaces;

namespace Delivery.API.Services
{
    public class DeliveryService : IDeliveryService
    {
        private readonly CancellationTokenSource _stoppingCts = new CancellationTokenSource();
        private readonly IDeliveryRepository _deliveryRepository;
        private readonly IMessageBusClient _messageBus;
        private readonly ILogger<DeliveryService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<Guid, CancellationTokenSource> _activeTimers = new();

        public DeliveryService(
            IDeliveryRepository deliveryRepository,
            IMessageBusClient messageBus,
            ILogger<DeliveryService> logger,
            IServiceProvider serviceProvider)
        {
            _deliveryRepository = deliveryRepository;
            _messageBus = messageBus;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task<DeliveryEntity> CreateDeliveryAsync(
    Guid orderId,
    string deliveryAddress,
    decimal orderTotal,
    int maxPreparationTime)
        {
            try
            {
                _logger.LogInformation("=== СОЗДАНИЕ ДОСТАВКИ ===");

                // Проверяем существование
                var existingDelivery = await _deliveryRepository.GetDeliveryByOrderIdAsync(orderId);
                if (existingDelivery != null)
                {
                    _logger.LogWarning("⚠️ Доставка для заказа {OrderId} уже существует: {DeliveryId}",
                        orderId, existingDelivery.Id);
                    return existingDelivery;
                }

                // 1. Рассчитываем время ПРИГОТОВЛЕНИЯ (используем реальное из заказа)
                var preparationTime = maxPreparationTime; 

                // 2. Рассчитываем время ДОСТАВКИ 
                var random = new Random();
                var baseDeliveryTime = 15; 
                var randomExtra = random.Next(15); 
                var deliveryTime = baseDeliveryTime + randomExtra; 

                // 3. ОБЩЕЕ время = приготовление + доставка
                var totalTime = preparationTime + deliveryTime; 

                _logger.LogInformation("РАСЧЕТ: Prep={Prep}min, Delivery={Delivery}min, Total={Total}min",
                    preparationTime, deliveryTime, totalTime);

                var estimatedDeliveryTime = DateTime.UtcNow.AddMinutes(totalTime);

                var delivery = new DeliveryEntity
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    DeliveryAddress = deliveryAddress,
                    Status = "Preparing",
                    EstimatedDurationMinutes = totalTime, // ← должно быть 17-27
                    PreparationTimeMinutes = preparationTime, // ← 2
                    DeliveryTimeMinutes = deliveryTime, // ← 15-25
                    PreparationStartedAt = DateTime.UtcNow,
                    EstimatedDeliveryTime = estimatedDeliveryTime,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Notes = $"Заказ #{orderId.ToString().Substring(0, 8)}. Готовка: {preparationTime}min, Доставка: {deliveryTime}min"
                };

                var createdDelivery = await _deliveryRepository.CreateDeliveryAsync(delivery);

                _logger.LogInformation("✅ Доставка создана: Prep={Prep}min, Delivery={Delivery}min, Total={Total}min",
                    preparationTime, deliveryTime, totalTime);

                // Публикуем событие
                _messageBus.Publish(new DeliveryCreatedEvent
                {
                    OrderId = orderId,
                    DeliveryId = createdDelivery.Id,
                    EstimatedDeliveryTime = estimatedDeliveryTime,
                    TotalMinutes = totalTime,
                    PreparationMinutes = preparationTime,
                    DeliveryMinutes = deliveryTime,
                    DeliveryAddress = deliveryAddress,
                    OrderTotal = orderTotal
                }, "delivery.created");

                return createdDelivery;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка создания доставки");
                throw;
            }
        }

        public async Task<DeliveryEntity> CreateAndStartDeliveryAsync(
            Guid orderId,
            string deliveryAddress,
            decimal orderTotal,
            int maxPreparationTime)
        {
            try
            {
                _logger.LogInformation("=== СОЗДАНИЕ И ЗАПУСК ДОСТАВКИ ===");

                // 1. Создаем доставку
                var delivery = await CreateDeliveryAsync(orderId, deliveryAddress, orderTotal, maxPreparationTime);

                // 2. Немедленно назначаем курьера (в реальной системе может быть ожидание оплаты)
                try
                {
                    await AssignCourierAsync(delivery.Id);
                    _logger.LogInformation("✅ Курьер назначен сразу после создания доставки");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Не удалось назначить курьера сразу, доставка осталась в статусе Pending");
                }

                // 3. Запускаем таймер доставки в фоновом режиме
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Ждем 1 минуту перед запуском таймера (имитация обработки)
                        await Task.Delay(TimeSpan.FromMinutes(1));

                        // Запускаем таймер
                        await StartDeliveryTimerAsync(delivery.Id);

                        // После завершения таймера начинаем симуляцию прогресса
                        await StartDeliverySimulationAsync(delivery.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Ошибка в фоновом процессе доставки {DeliveryId}", delivery.Id);
                    }
                });

                return delivery;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка создания и запуска доставки");
                throw;
            }
        }
        //public async Task StopAsync(CancellationToken cancellationToken)
        //{
        //    _stoppingCts.Cancel();

        //    // Останавливаем все таймеры
        //    foreach (var timer in _activeTimers.Values)
        //    {
        //        timer.Cancel();
        //    }

        //    await Task.CompletedTask;
        //}

        public void Dispose()
        {
            _stoppingCts?.Cancel();
            _stoppingCts?.Dispose();

            foreach (var timer in _activeTimers.Values)
            {
                timer?.Cancel();
                timer?.Dispose();
            }
            _activeTimers.Clear();

            _logger.LogInformation("🛑 DeliveryService disposed");
        }

        public async Task StartDeliverySimulationAsync(Guid deliveryId)
        {
            try
            {
                _logger.LogInformation("🚚 Начинаем симуляцию прогресса доставки {DeliveryId}", deliveryId);

                var random = new Random();
                var delivery = await _deliveryRepository.GetDeliveryAsync(deliveryId);

                if (delivery == null || delivery.Status == "Delivered" || delivery.Status == "Cancelled")
                    return;

                // Шаг 1: Заказ принят -> Готовится (через 1 минуту)
                await Task.Delay(TimeSpan.FromMinutes(1));

                // Обновляем доставку после задержки
                delivery = await _deliveryRepository.GetDeliveryAsync(deliveryId);
                if (delivery != null && delivery.Status == "Assigned")
                {
                    await UpdateDeliveryStatusAsync(deliveryId, "PickedUp", delivery.CourierId);
                    _logger.LogInformation("📦 Курьер забрал заказ");
                }

                // Шаг 2: В пути (через 2-5 минут после получения)
                var onTheWayDelay = random.Next(2, 6);
                await Task.Delay(TimeSpan.FromMinutes(onTheWayDelay));

                // Обновляем доставку после задержки
                delivery = await _deliveryRepository.GetDeliveryAsync(deliveryId);
                if (delivery != null && delivery.Status == "PickedUp")
                {
                    await UpdateDeliveryStatusAsync(deliveryId, "OnTheWay", delivery.CourierId);
                    _logger.LogInformation("🏍️ Курьер в пути");
                }

                // Шаг 3: Доставлен (через оставшееся время)
                var remainingTime = delivery.EstimatedDurationMinutes - (1 + onTheWayDelay);
                if (remainingTime > 0)
                {
                    await Task.Delay(TimeSpan.FromMinutes(remainingTime));

                    // Обновляем доставку после задержки
                    delivery = await _deliveryRepository.GetDeliveryAsync(deliveryId);
                    if (delivery != null && delivery.Status == "OnTheWay")
                    {
                        await UpdateDeliveryStatusAsync(deliveryId, "Delivered", delivery.CourierId);
                        _logger.LogInformation("✅ Заказ доставлен!");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка симуляции доставки {DeliveryId}", deliveryId);
            }
        }

        public async Task<DeliveryEntity> AssignCourierAsync(Guid deliveryId)
        {
            try
            {
                _logger.LogInformation("Назначение курьера для доставки {DeliveryId}", deliveryId);

                var delivery = await _deliveryRepository.GetDeliveryAsync(deliveryId);
                if (delivery == null)
                {
                    _logger.LogError("❌ Доставка {DeliveryId} не найдена", deliveryId);
                    throw new ArgumentException($"Delivery {deliveryId} not found");
                }

                // Разрешаем назначение из статусов: Preparing, ReadyForPickup
                if (delivery.Status != "Preparing" && delivery.Status != "PickingUp")
                {
                    _logger.LogError("❌ Доставка уже имеет статус: {Status}", delivery.Status);
                    throw new InvalidOperationException($"Cannot assign courier. Delivery status: {delivery.Status}");
                }

                // Назначаем курьера
                var courier = await _deliveryRepository.AssignCourierToDeliveryAsync(deliveryId);
                if (courier == null)
                {
                    _logger.LogWarning("⚠️ Нет доступных курьеров для доставки {DeliveryId}", deliveryId);
                    throw new InvalidOperationException("No available couriers");
                }

                _logger.LogInformation("Выбран курьер: {CourierName} (ID: {CourierId})",
                    courier.Name, courier.Id);

                // ВАЖНО: Используем UpdateDeliveryStatusAsync для публикации событий
                var updatedDelivery = await UpdateDeliveryStatusAsync(deliveryId, "OnTheWay", courier.Id);

                updatedDelivery.DeliveryStartedAt = DateTime.UtcNow;

                await _deliveryRepository.UpdateDeliveryAsync(updatedDelivery);

                _logger.LogInformation("✅ Курьер {CourierId} назначен на доставку {DeliveryId}",
                    courier.Id, deliveryId);

                return updatedDelivery;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка назначения курьера для доставки {DeliveryId}", deliveryId);
                throw;
            }
        }
        public async Task StopDeliveryTimerAsync(Guid deliveryId)
        {
            try
            {
                _logger.LogInformation("⏹️ Stopping delivery timer for {DeliveryId}", deliveryId);

                // Проверяем и отменяем активные таймеры
                if (_activeTimers.ContainsKey(deliveryId))
                {
                    _activeTimers[deliveryId].Cancel();
                    _activeTimers.Remove(deliveryId);

                    _logger.LogInformation("✅ Delivery timer stopped for {DeliveryId}", deliveryId);
                }
                else
                {
                    _logger.LogInformation("ℹ️ No active timer found for {DeliveryId}", deliveryId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error stopping delivery timer for {DeliveryId}", deliveryId);
            }
        }


        public async Task<DeliveryEntity> UpdateDeliveryStatusAsync(
    Guid deliveryId,
    string status,
    Guid? courierId = null)
        {
            try
            {
                _logger.LogInformation("Обновление статуса доставки {DeliveryId} на '{Status}'",
                    deliveryId, status);

                var delivery = await _deliveryRepository.GetDeliveryAsync(deliveryId);
                if (delivery == null)
                {
                    _logger.LogError("❌ Доставка {DeliveryId} не найдена", deliveryId);
                    throw new ArgumentException($"Delivery {deliveryId} not found");
                }

                var oldStatus = delivery.Status;

                if ((status == "Cancelled" || status == "Delivered") &&
           oldStatus != status)
                {
                    await StopDeliveryTimerAsync(deliveryId);
                }

                // Если статус меняется на "Cancelled", освобождаем курьера
                if (status == "Cancelled" && delivery.CourierId.HasValue)
                {
                    await FreeCourierAsync(delivery.CourierId.Value);
                }

                delivery.Status = status;
                delivery.UpdatedAt = DateTime.UtcNow;

                _logger.LogInformation("Смена статуса доставки: {OldStatus} -> {NewStatus}", oldStatus, status);

                // Маппинг статусов доставки на статусы заказов
                var orderStatusMap = new Dictionary<string, string>
                {
                    ["Preparing"] = "Preparing",
                    ["PickingUp"] = "PickingUp",
                    ["OnTheWay"] = "OnTheWay",
                    ["Delivered"] = "Delivered",
                    ["Cancelled"] = "Cancelled"
                };

                // Сохраняем изменения в БД
                await _deliveryRepository.UpdateDeliveryAsync(delivery);

                // Публикуем события
                await PublishStatusEventsAsync(delivery, oldStatus, status);

                return delivery;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка обновления статуса доставки {DeliveryId}", deliveryId);
                throw;
            }
        }
        private async Task FreeCourierAsync(Guid courierId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var deliveryRepository = scope.ServiceProvider.GetRequiredService<IDeliveryRepository>();

                var courier = await deliveryRepository.GetCourierAsync(courierId);
                if (courier != null)
                {
                    courier.IsAvailable = true;
                    courier.UpdatedAt = DateTime.UtcNow;

                    // Сохраняем изменения
                    await deliveryRepository.SaveChangesAsync();

                    _logger.LogInformation("✅ Курьер {CourierName} (ID: {CourierId}) освобожден",
                        courier.Name, courierId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка освобождения курьера {CourierId}", courierId);
            }
        }

        private async Task PublishStatusEventsAsync(
    DeliveryEntity delivery,
    string oldStatus,
    string newStatus)
        {
            try
            {
                _logger.LogInformation("📤 Publishing status events for delivery {DeliveryId}: {OldStatus} -> {NewStatus}",
                    delivery.Id, oldStatus, newStatus);

                // 1. Всегда публикуем DeliveryStatusChangedEvent
                var deliveryStatusEvent = new DeliveryStatusChangedEvent
                {
                    OrderId = delivery.OrderId,
                    DeliveryId = delivery.Id,
                    OldStatus = oldStatus,
                    NewStatus = newStatus,
                    ChangedAt = DateTime.UtcNow
                };

                _messageBus.Publish(deliveryStatusEvent, "delivery.status.changed");
                _logger.LogInformation("✅ DeliveryStatusChangedEvent published");

                // 2. Всегда публикуем OrderStatusUpdatedEvent
                var orderStatusMap = new Dictionary<string, string>
                {
                    ["Preparing"] = "Preparing",
                    ["PickingUp"] = "PickingUp",
                    ["OnTheWay"] = "OnTheWay",
                    ["Delivered"] = "Delivered",
                    ["Cancelled"] = "Cancelled"
                };

                if (orderStatusMap.TryGetValue(newStatus, out var orderStatus))
                {
                    var orderStatusEvent = new OrderStatusUpdatedEvent
                    {
                        OrderId = delivery.OrderId,
                        Status = orderStatus,
                        UpdatedAt = DateTime.UtcNow,
                        DeliveryId = delivery.Id,
                        CourierId = delivery.CourierId
                    };

                    _messageBus.Publish(orderStatusEvent, "order.status.updated");
                    _logger.LogInformation("✅ OrderStatusUpdatedEvent published: {OrderStatus} for order {OrderId}",
                        orderStatus, delivery.OrderId);
                }
                else
                {
                    _logger.LogWarning("⚠️ No order status mapping for delivery status: {DeliveryStatus}", newStatus);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error publishing status events");
            }
        }

        public async Task<DeliveryEntity> SimulateDeliveryProgressAsync(Guid deliveryId)
        {
            try
            {
                _logger.LogInformation("Симуляция прогресса доставки {DeliveryId}", deliveryId);

                var delivery = await _deliveryRepository.GetDeliveryAsync(deliveryId);
                if (delivery == null || delivery.Status == "Delivered" || delivery.Status == "Cancelled")
                {
                    _logger.LogInformation("Доставка {DeliveryId} завершена или отменена", deliveryId);
                    return delivery;
                }

                var random = new Random();
                var progressChance = random.Next(100);
                var oldStatus = delivery.Status;

                _logger.LogInformation("Текущий статус: {Status}, Шанс прогресса: {Chance}%",
                    delivery.Status, progressChance);

                switch (delivery.Status)
                {
                    case "Pending":
                        if (progressChance < 80) // 80% шанс назначить курьера
                        {
                            _logger.LogInformation("Назначаем курьера (шанс сработал)");
                            try
                            {
                                return await AssignCourierAsync(deliveryId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Не удалось назначить курьера");
                                return delivery;
                            }
                        }
                        break;

                    case "Assigned":
                        if (progressChance < 70) // 70% шанс забрать заказ
                        {
                            _logger.LogInformation("Курьер забирает заказ");
                            await UpdateDeliveryStatusAsync(deliveryId, "PickedUp");
                        }
                        break;

                    case "PickedUp":
                        if (progressChance < 60) // 60% шанс доставить
                        {
                            _logger.LogInformation("Заказ доставлен");
                            await UpdateDeliveryStatusAsync(deliveryId, "Delivered");
                        }
                        else if (progressChance < 80) // 20% шанс быть в пути
                        {
                            _logger.LogInformation("Курьер в пути");
                            await UpdateDeliveryStatusAsync(deliveryId, "OnTheWay");
                        }
                        break;

                    case "OnTheWay":
                        if (progressChance < 90) // 90% шанс доставить
                        {
                            _logger.LogInformation("Заказ доставлен");
                            await UpdateDeliveryStatusAsync(deliveryId, "Delivered");
                        }
                        break;
                }

                if (oldStatus != delivery.Status)
                {
                    _logger.LogInformation("Статус изменился: {OldStatus} -> {NewStatus}",
                        oldStatus, delivery.Status);
                }
                else
                {
                    _logger.LogInformation("Статус не изменился");
                }

                return await _deliveryRepository.GetDeliveryAsync(deliveryId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка симуляции прогресса доставки {DeliveryId}", deliveryId);
                return null;
            }
        }

        public async Task<int> CalculateDeliveryTimeAsync(
    string address,
    decimal orderTotal,
    int maxPreparationTime)
        {
            try
            {
                // Простой расчет: приготовление + доставка
                var preparationTime = maxPreparationTime; 
                var random = new Random();
                var deliveryTime = 15 + random.Next(15);

                return preparationTime + deliveryTime; 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка расчета времени доставки");
                return 30; // Дефолтное значение
            }
        }



        public async Task StartDeliveryTimerAsync(Guid deliveryId)
        {
            try
            {
                var delivery = await _deliveryRepository.GetDeliveryAsync(deliveryId);
                if (delivery == null || delivery.Status == "Delivered" || delivery.Status == "Cancelled")
                    return;

                var now = DateTime.UtcNow;
                if (delivery.Status == "Preparing" &&
            delivery.PreparationStartedAt.HasValue &&
            (now - delivery.PreparationStartedAt.Value).TotalMinutes >= delivery.PreparationTimeMinutes)
                {
                    _logger.LogInformation("⏰ Время приготовления уже прошло, назначаем курьера немедленно");
                    await UpdateDeliveryStatusAsync(deliveryId, "PickingUp");
                    try
                    {
                        await AssignCourierAsync(deliveryId);
                        _logger.LogInformation("🚀 Courier assigned immediately for delivery {DeliveryId}", deliveryId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Failed to assign courier for delivery {DeliveryId}", deliveryId);
                    }
                    return;
                }
                // Отменяем старый таймер если есть
                if (_activeTimers.ContainsKey(deliveryId))
                {
                    _activeTimers[deliveryId].Cancel();
                    _activeTimers.Remove(deliveryId);
                }

                // Используем реальные значения из БД
                var preparationTime = delivery.PreparationTimeMinutes; // ← должно быть 2
                var deliveryTime = delivery.DeliveryTimeMinutes; // ← должно быть 15-25
                var totalTime = delivery.EstimatedDurationMinutes;

                _logger.LogInformation("⏰ Starting delivery timer for {DeliveryId}: Prep={Prep}min, Delivery={Delivery}min, Total={Total}min",
                    deliveryId, preparationTime, deliveryTime, totalTime);

                var cts = new CancellationTokenSource();
                _activeTimers[deliveryId] = cts;

                // Запускаем таймер в фоне
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Фаза приготовления (2 минуты)
                        if (!cts.Token.IsCancellationRequested && preparationTime > 0)
                        {
                            _logger.LogInformation("🍳 Delivery {DeliveryId} started preparation for {Minutes} minutes",
                                deliveryId, preparationTime);

                            // Ждем время приготовления
                            await Task.Delay(TimeSpan.FromMinutes(preparationTime), cts.Token);
                        }

                        // Переход в ReadyForPickup
                        if (!cts.Token.IsCancellationRequested)
                        {
                            _logger.LogInformation("✅ Delivery {DeliveryId} is ready for pickup", deliveryId);

                            // Обновляем статус
                            await UpdateDeliveryStatusAsync(deliveryId, "PickingUp");

                            // НЕМЕДЛЕННО назначаем курьера (без ожидания)
                            try
                            {
                                await AssignCourierAsync(deliveryId);
                                _logger.LogInformation("🚀 Courier assigned immediately for delivery {DeliveryId}", deliveryId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "⚠️ Failed to assign courier for delivery {DeliveryId}. Will retry...", deliveryId);

                                // Пробуем еще раз через 30 секунд
                                await Task.Delay(TimeSpan.FromSeconds(30), cts.Token);
                                if (!cts.Token.IsCancellationRequested)
                                {
                                    try
                                    {
                                        await AssignCourierAsync(deliveryId);
                                    }
                                    catch
                                    {
                                        // Игнорируем повторную ошибку
                                    }
                                }
                            }
                        }

                        // Фаза доставки (15-25 минут)
                        if (!cts.Token.IsCancellationRequested && deliveryTime > 0)
                        {
                            _logger.LogInformation("🏍️ Delivery {DeliveryId} started delivery phase for {Minutes} minutes",
                                deliveryId, deliveryTime);

                            await Task.Delay(TimeSpan.FromMinutes(deliveryTime), cts.Token);
                        }

                        // Автоматически завершаем доставку
                        if (!cts.Token.IsCancellationRequested)
                        {
                            var currentDelivery = await _deliveryRepository.GetDeliveryAsync(deliveryId);
                            if (currentDelivery != null &&
                                currentDelivery.Status != "Delivered" &&
                                currentDelivery.Status != "Cancelled")
                            {
                                await UpdateDeliveryStatusAsync(deliveryId, "Delivered");
                                _logger.LogInformation("🎯 Delivery {DeliveryId} automatically marked as delivered", deliveryId);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("⏹️ Timer cancelled for delivery {DeliveryId}", deliveryId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Error in delivery timer for {DeliveryId}", deliveryId);
                    }
                    finally
                    {
                        _activeTimers.Remove(deliveryId);
                    }
                }, cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error starting delivery timer for {DeliveryId}", deliveryId);
            }
        }

        public async Task<DeliveryEntity> GetDeliveryAsync(Guid deliveryId)
        {
            try
            {
                _logger.LogInformation("Получение доставки {DeliveryId}", deliveryId);
                var delivery = await _deliveryRepository.GetDeliveryAsync(deliveryId);

                if (delivery == null)
                {
                    _logger.LogWarning("Доставка {DeliveryId} не найдена", deliveryId);
                }
                else
                {
                    _logger.LogInformation("✅ Найдена доставка {DeliveryId} со статусом {Status}",
                        deliveryId, delivery.Status);
                }

                return delivery;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка получения доставки {DeliveryId}", deliveryId);
                throw;
            }
        }

        public async Task<DeliveryEntity> GetDeliveryByOrderIdAsync(Guid orderId)
        {
            try
            {
                _logger.LogInformation("Получение доставки по OrderId: {OrderId}", orderId);
                var delivery = await _deliveryRepository.GetDeliveryByOrderIdAsync(orderId);

                if (delivery == null)
                {
                    _logger.LogWarning("Доставка для заказа {OrderId} не найдена", orderId);
                }
                else
                {
                    _logger.LogInformation("✅ Найдена доставка {DeliveryId} для заказа {OrderId}",
                        delivery.Id, orderId);
                }

                return delivery;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка получения доставки по OrderId {OrderId}", orderId);
                throw;
            }
        }

    }
}