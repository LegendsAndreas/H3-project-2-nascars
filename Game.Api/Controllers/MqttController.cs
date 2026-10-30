using System.Text.Json;
using Game.Api.Contracts;
using Game.Api.Mqtt;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers;

[ApiController]
[Route("api/mqtt")]
public sealed class MqttController(MqttService mqtt) : ControllerBase
{
    [HttpGet("status")]
    public ActionResult<MqttStatusResponse> GetStatus() => Ok(mqtt.GetStatus());

    [HttpGet("input")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult<MqttMessageResponse> GetInput() =>
        mqtt.GetLastInput() is { } message ? Ok(message) : NoContent();

    [HttpGet("state")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult<MqttMessageResponse> GetState() =>
        mqtt.GetLastState() is { } message ? Ok(message) : NoContent();

    [HttpPost("state")]
    [RequestSizeLimit(65536)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> PublishState([FromBody] JsonElement state, CancellationToken cancellationToken)
    {
        if (!mqtt.IsValidState(state))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Expected a JSON object within Mqtt:MaxPayloadBytes.");
        }

        if (!await mqtt.PublishStateAsync(state, cancellationToken))
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "MQTT broker is unavailable. Check /api/mqtt/status.");
        }

        return Accepted(new { topic = mqtt.GetStatus().StateTopic });
    }
}
