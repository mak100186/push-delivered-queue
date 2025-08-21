using Microsoft.AspNetCore.Mvc;

using PushDeliveredQueue.Core;
using PushDeliveredQueue.Core.Models;

namespace PushDeliveredQueue.Sample.Controllers;
[ApiController]
[Route("[controller]")]
public class SubscribaleQueueController(SubscribableQueue queue,ILogger<SubscribaleQueueController> logger) : ControllerBase
{
    [HttpPost("enqueue")]
    public IActionResult Enqueue([FromBody] string payload)
    {
        queue.Enqueue(payload);
        logger.LogInformation("Enqueued message: {Payload}", payload);
        return Ok();
    }
    [HttpPost("subscribe")]
    public IActionResult Subscribe()
    {
        var subId = queue.Subscribe(async msg =>
        {
            logger.LogInformation("Processing message: {Payload}", msg.Payload);
            if (msg.Payload.Contains("fail"))
            {
                return DeliveryResult.Nack;
            }
            await Task.Delay(10); // Simulate work

            logger.LogInformation("{MessageId} with {Payload} is processed", msg.Id, msg.Payload);
            return DeliveryResult.Ack;
        });
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