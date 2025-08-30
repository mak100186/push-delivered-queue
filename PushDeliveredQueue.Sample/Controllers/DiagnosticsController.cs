using Microsoft.AspNetCore.Mvc;

using PushDeliveredQueue.Core;
using PushDeliveredQueue.Sample.Dtos;
using PushDeliveredQueue.Sample.Extensions;

namespace PushDeliveredQueue.Sample.Controllers;

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
}
