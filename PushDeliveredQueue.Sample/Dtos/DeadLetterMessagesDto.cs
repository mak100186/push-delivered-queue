namespace PushDeliveredQueue.Sample.Dtos;

public record DeadLetterMessagesDto(Guid Id, string Payload);
