using Microsoft.AspNetCore.Mvc;

using PushDeliveredQueue.Core;
using PushDeliveredQueue.Core.Models;

namespace PushDeliveredQueue.Sample.Controllers;
[ApiController]
[Route("[controller]")]
public class SubscribaleQueueController(SubscribableQueue queue, SubscribedMessageHandler handler, ILogger<SubscribaleQueueController> logger) : ControllerBase
{
    [HttpPost("enqueue")]
    public IActionResult Enqueue([FromBody] string payload)
    {
        var messageId = queue.Enqueue(payload);
        logger.LogInformation("Enqueued: MessageId:[{MessageId}] Payload:[{Payload}]", messageId, payload);
        return Ok(messageId);
    }
    [HttpPost("subscribe")]
    public IActionResult Subscribe()
    {
        var subId = queue.Subscribe(handler.HandlerAsync);
        logger.LogInformation("Subscribed with ID: {SubId}", subId);
        return Ok(subId);
    }
    [HttpPost("unsubscribe/{subId}")]
    public IActionResult Unsubscribe(Guid subId)
    {
        queue.Unsubscribe(subId);
        logger.LogInformation("Unsubscribed ID: {SubId}", subId);
        return Ok();
    }
}

public class SubscribedMessageHandler(ILogger<SubscribedMessageHandler> logger)
{
    public async Task<DeliveryResult> HandlerAsync(MessageEnvelope msg)
    {
        logger.LogInformation("Processing: MessageId:[{MessageId}] Payload:[{Payload}]", msg.Id, msg.Payload);
        if (msg.Payload.Contains("fail"))
        {
            return DeliveryResult.Nack;
        }
        await Task.Delay(10); // Simulate work

        logger.LogInformation("Processed: MessageId:[{MessageId}] Payload:[{Payload}]", msg.Id, msg.Payload);
        return DeliveryResult.Ack;
    }
}
