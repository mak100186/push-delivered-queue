using Microsoft.AspNetCore.Mvc;

using PushDeliveredQueue.Core;
using PushDeliveredQueue.API.Dtos;
using PushDeliveredQueue.API.Extensions;

namespace PushDeliveredQueue.API.Controllers;

[ApiController]
[Route("diagnostics")]
public class DiagnosticsController(SubscribableQueue queue) : ControllerBase
{
    [HttpGet("state")]
    public ActionResult<SubscribableQueueStateDto> GetQueueState()
    {
        var state = queue.GetState();

        return Ok(state.ToExternalDto());
    }

    [HttpPost("changePayload")]
    public IActionResult ChangePayload(Guid messageId, string payload, CancellationToken cancellationToken)
    {
        queue.ChangeMessagePayload(messageId, payload, cancellationToken);
        return Ok();
    }
}
