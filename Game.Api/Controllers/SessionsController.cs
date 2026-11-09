using Game.Api.Contracts;
using Game.Api.Racing;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers;

// HTTP is retained for Swagger/manual checks; Unity reports arrive through MQTT.
[ApiController]
[Route("api/sessions")]
public sealed class SessionsController(GameService game) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateSessionRequest request, CancellationToken token)
    {
        var session = await game.CreateAsync(request, token);
        return CreatedAtAction(nameof(Get), new { id = session.Id }, session);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int limit = 20, CancellationToken token = default) =>
        Ok(await game.ListAsync(limit, token));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken token) => Ok(await game.GetAsync(id, token));

    [HttpPut("{id:guid}/results/{carId}")]
    public async Task<IActionResult> SaveResult(Guid id, string carId, SaveResultRequest request, CancellationToken token) =>
        Ok(await game.SaveResultAsync(id, carId, request, token));

    [HttpGet("{id:guid}/results")]
    public async Task<IActionResult> Results(Guid id, CancellationToken token) => Ok(await game.ResultsAsync(id, token));

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken token) => Ok(await game.CompleteAsync(id, token));
}
