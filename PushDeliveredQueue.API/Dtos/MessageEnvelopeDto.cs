namespace PushDeliveredQueue.API.Dtos;
public record MessageEnvelopeDto(Guid Id, string Payload, string ExpiresIn);
