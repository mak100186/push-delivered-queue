using Microsoft.AspNetCore.Mvc;

using PushDeliveredQueue.Core;
using PushDeliveredQueue.API.Handlers;

namespace PushDeliveredQueue.API.Controllers;
[ApiController]
[Route("[controller]")]
public class SubscribaleQueueController(SubscribableQueue queue, SubscribedMessageHandler handler, ILogger<SubscribaleQueueController> logger) : ControllerBase
{
    [HttpPost("enqueueMultiple")]
    public ActionResult<List<Guid>> EnqueueXMessages(int count)
    {
        var currentMessageCount = queue.GetState().Buffer.Count;

        var messageIds = new List<Guid>(count);
        for (var i = 0; i < count; i++)
        {
            var payload = $"{currentMessageCount + i}";
            var messageId = queue.Enqueue(payload);
            logger.LogInformation("Enqueued: MessageId:[{MessageId}] Payload:[{Payload}]", messageId, payload);

            messageIds.Add(messageId);
        }

        return Ok(messageIds);
    }

    [HttpPost("enqueue")]
    public ActionResult<Guid> Enqueue([FromBody] string payload)
    {
        var messageId = queue.Enqueue(payload);
        logger.LogInformation("Enqueued: MessageId:[{MessageId}] Payload:[{Payload}]", messageId, payload);
        return Ok(messageId);
    }

    [HttpPost("enqueueSingle")]
    public ActionResult<Guid> EnqueueSingle([FromBody] string payload)
    {
        var messageId = queue.Enqueue(payload);
        logger.LogInformation("Enqueued: MessageId:[{MessageId}] Payload:[{Payload}]", messageId, payload);
        return Ok(messageId);
    }

    [HttpPost("subscribe")]
    public ActionResult<Guid> Subscribe()
    {
        var subId = queue.Subscribe(handler);
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
