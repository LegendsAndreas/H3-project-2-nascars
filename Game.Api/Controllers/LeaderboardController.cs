using Game.Api.Racing;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers;

[ApiController]
[Route("api/leaderboard")]
public sealed class LeaderboardController(GameService game) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string trackId = "main", [FromQuery] int limit = 10, CancellationToken token = default) =>
        Ok(await game.LeaderboardAsync(trackId, limit, token));
}
