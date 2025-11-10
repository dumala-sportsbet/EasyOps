using Google.Protobuf;

namespace EasyOps.Services.Kafka;

public interface IPublisherService
{
    Task PublishMessage(IMessage message, string topic, string key, string correlationId);
}