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
                HostName = configuration["RabbitMQ:Host"],
                Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
                UserName = configuration["RabbitMQ:Username"],
                Password = configuration["RabbitMQ:Password"]
            };
            
            try
            {
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                
                // Объявление обмена
                _channel.ExchangeDeclare(_exchangeName, ExchangeType.Topic, durable: true);
                
                _logger.LogInformation("Connected to RabbitMQ");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not connect to RabbitMQ");
                throw;
            }
        }
        
        public void Publish<T>(T message, string exchange, string routingKey)
        {
            try
            {
                var json = JsonConvert.SerializeObject(message);
                var body = Encoding.UTF8.GetBytes(json);
                
                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.ContentType = "application/json";
                
                _channel.BasicPublish(
                    exchange: exchange,
                    routingKey: routingKey,
                    basicProperties: properties,
                    body: body);
                
                _logger.LogDebug("Published message to {Exchange} with routing key {RoutingKey}", 
                    exchange, routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing message to RabbitMQ");
            }
        }
        
        public void Subscribe<T>(string queue, string exchange, string routingKey, Action<T> handler)
        {
            _channel.QueueDeclare(queue: queue, durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind(queue, exchange, routingKey);
            
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var eventMessage = JsonConvert.DeserializeObject<T>(message);
                    
                    handler(eventMessage);
                    
                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message from RabbitMQ");
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                }
            };
            
            _channel.BasicConsume(queue: queue, autoAck: false, consumer: consumer);
            _logger.LogInformation("Subscribed to queue {Queue} with routing key {RoutingKey}", 
                queue, routingKey);
        }
        
        public void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
        }
    }
}
