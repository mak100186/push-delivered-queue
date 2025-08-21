using Microsoft.AspNetCore.Mvc;

using PushDeliveredQueue.Core;
using PushDeliveredQueue.Core.Models;

namespace PushDeliveredQueue.Sample.Controllers;

[ApiController]
[Route("diagnostics")]
public class DiagnosticsController(SubscribableQueue queue) : ControllerBase
{
    [HttpGet("state")]
    public ActionResult<SubscribableQueueState> GetQueueState()
    {
        var state = queue.GetState();
        return Ok(state);
    }
}