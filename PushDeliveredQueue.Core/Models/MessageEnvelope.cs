namespace PushDeliveredQueue.Core.Models;

public record MessageEnvelope(Guid Id, DateTime CreatedAt, string Payload);
