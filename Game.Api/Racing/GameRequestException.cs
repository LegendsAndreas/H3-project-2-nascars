namespace Game.Api.Racing;

public sealed class GameRequestException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
