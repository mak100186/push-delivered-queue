namespace PushDeliveredQueue.Core.Models;

public class MessageEnvelope(Guid id, DateTime createdAt, string payload)
{
    public Guid Id { get; init; } = id;
    public DateTime CreatedAt { get; init; } = createdAt;
    public string Payload { get; set; } = payload;

    public void ChangePayload(string newPayload) => Payload = newPayload;

    public override string ToString() => $"MessageEnvelope(Id={Id}, CreatedAt={CreatedAt}, Payload={Payload})";

}
