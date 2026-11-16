using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Tables.DTOs;

namespace CafeHub.Application.Tables.Queries
{
    /// <summary>
    /// Reads one table within the caller's branch scope. Returns the uniform not-found result for a
    /// foreign or missing id.
    /// </summary>
    public sealed record GetTableQuery(Guid TableId) : IQuery<TableResponse>;
}
