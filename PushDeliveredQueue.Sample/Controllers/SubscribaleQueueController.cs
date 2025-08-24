using Microsoft.AspNetCore.Mvc;

using PushDeliveredQueue.Core;
using PushDeliveredQueue.Sample.Handlers;

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
