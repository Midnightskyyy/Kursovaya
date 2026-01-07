namespace Shared.Messages.Interfaces
{
    public interface IMessageBusClient
    {
        void Publish<T>(T message, string exchange, string routingKey);
        void Subscribe<T>(string queue, string exchange, string routingKey, Action<T> handler);
        void Dispose();
    }
}