using Microsoft.AspNetCore.Mvc;

using PushDeliveredQueue.Core;

namespace PushDeliveredQueue.Sample.Controllers;

[ApiController]
[Route("[controller]")]
public class ReplayQueueController(SubscribableQueue queue, ILogger<ReplayQueueController> logger) : ControllerBase
{
    [HttpPost("replayFrom")]
    public IActionResult ReplayFrom(Guid subscriberId, Guid messageId)
    {
        queue.ReplayFrom(subscriberId, messageId);
        return Ok();
    }

    [HttpPost("replayAllDlq")]
    public async Task<IActionResult> ReplayAllDlqAsync(Guid subscriberId, CancellationToken cancellationToken)
    {
        await queue.ReplayAllDlqMessagesAsync(subscriberId, cancellationToken);
        return Ok();
    }

    [HttpPost("replayAllSubscribers")]
    public IActionResult ReplayAllDlqSubscribers(CancellationToken cancellationToken)
    {
        queue.ReplayAllDlqSubscribers(cancellationToken);
        return Ok();
    }

    [HttpPost("replayFromDlq")]
    public async Task<IActionResult> ReplayFromDlqAsync(Guid subscriberId, Guid messageId, CancellationToken cancellationToken)
    {
        await queue.ReplayFromDlqAsync(subscriberId, messageId, cancellationToken);
        return Ok();
    }
}
