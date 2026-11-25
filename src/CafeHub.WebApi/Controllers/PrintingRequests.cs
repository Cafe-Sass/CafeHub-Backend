namespace CafeHub.WebApi.Controllers
{
    public sealed record CreatePrintConnectorRequest(string? Name);

    public sealed record ConnectorFailureRequest(string? ErrorCode);
}
