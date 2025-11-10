using Google.Protobuf;
using Smf.KafkaLib.Helpers;
using Smf.KafkaLib.Producer;

namespace EasyOps.Services.Kafka;

public class PublisherService(IKafkaProducer<string, IMessage> producer) : IPublisherService
{
    private readonly IKafkaProducer<string, IMessage> _producer = producer;

    public async Task PublishMessage(IMessage message, string topic, string key, string correlationId)
    {
        var headers = new Confluent.Kafka.Headers();
        headers.AddCorrelationIdHeader(correlationId);
        headers.AddHeader("content_type", "application/x-protobuf");
        headers.AddHeader("correlation_id", correlationId);

        await _producer.ProduceAsync(topic, key, message);
    }
}