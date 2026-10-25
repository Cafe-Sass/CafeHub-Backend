namespace CafeHub.Application.Idempotency
{
    public interface IIdempotentCommand
    {
        string? IdempotencyKey { get; set; }
    }
}
