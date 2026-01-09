using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Messages.Interfaces;

namespace Shared.Messages.Infrastructure
{
    public class RabbitMQClient : IMessageBusClient, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMQClient> _logger;
        private readonly string _exchangeName = "fooddelivery.events";

        public RabbitMQClient(IConfiguration configuration, ILogger<RabbitMQClient> logger)
        {
            _logger = logger;

            var factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMQ:Host"] ?? "rabbitmq",
                Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
                UserName = configuration["RabbitMQ:Username"] ?? "guest",
                Password = configuration["RabbitMQ:Password"] ?? "guest",
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            try
            {
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Создаем topic exchange
                _channel.ExchangeDeclare(
                    exchange: _exchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false);

                _logger.LogInformation("✅ Connected to RabbitMQ at {Host}:{Port}",
                    factory.HostName, factory.Port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Could not connect to RabbitMQ");
                throw;
            }
        }

        public void Publish<T>(T message, string routingKey)
        {
            try
            {
                var json = JsonConvert.SerializeObject(message, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
                var body = Encoding.UTF8.GetBytes(json);

                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.ContentType = "application/json";
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                _channel.BasicPublish(
                    exchange: _exchangeName,
                    routingKey: routingKey,
                    mandatory: true,
                    basicProperties: properties,
                    body: body);

                _logger.LogDebug("📤 Published message with routing key: {RoutingKey}", routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error publishing message to RabbitMQ with routing key: {RoutingKey}", routingKey);
                throw;
            }
        }

        public void Subscribe<T>(string queue, string routingKey, Action<T> handler)
        {
            try
            {
                // Создаем durable очередь
                _channel.QueueDeclare(
                    queue: queue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                // Биндим очередь к exchange с routing key
                _channel.QueueBind(
                    queue: queue,
                    exchange: _exchangeName,
                    routingKey: routingKey);

                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += (model, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        _logger.LogDebug("📥 Received message: {Message} with routing key: {RoutingKey}",
                            message, ea.RoutingKey);

                        var eventMessage = JsonConvert.DeserializeObject<T>(message);

                        if (eventMessage != null)
                        {
                            handler(eventMessage);
                        }

                        _channel.BasicAck(ea.DeliveryTag, multiple: false);
                        _logger.LogDebug("✅ Successfully processed message from queue: {Queue}", queue);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Error processing message from queue: {Queue}", queue);
                        _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                    }
                };

                _channel.BasicConsume(
                    queue: queue,
                    autoAck: false,
                    consumer: consumer);

                _logger.LogInformation("✅ Subscribed to queue: {Queue} with routing key: {RoutingKey}",
                    queue, routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error subscribing to queue: {Queue}", queue);
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                _channel?.Close();
                _connection?.Close();
                _logger.LogInformation("📴 RabbitMQ connection closed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing RabbitMQ client");
            }
        }
    }
}