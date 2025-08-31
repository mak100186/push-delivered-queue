namespace PushDeliveredQueue.API.Dtos;

public record DeadLetterMessagesDto(Guid Id, string Payload);
