namespace PushDeliveredQueue.Sample.Dtos;
public record MessageEnvelopeDto(Guid Id, string Payload, string ExpiresIn);
