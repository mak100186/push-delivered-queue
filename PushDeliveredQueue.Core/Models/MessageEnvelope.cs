namespace PushDeliveredQueue.Core.Models;

public record MessageEnvelope(Guid Id, DateTime Timestamp, string Payload);