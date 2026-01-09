namespace Shared.Messages.Interfaces
{
    public interface IMessageBusClient
    {
        void Publish<T>(T message, string routingKey);
        void Subscribe<T>(string queue, string routingKey, Action<T> handler);
        void Dispose();
    }
}