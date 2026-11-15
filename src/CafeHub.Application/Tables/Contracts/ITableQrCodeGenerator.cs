namespace CafeHub.Application.Tables.Contracts
{
    /// <summary>
    /// Produces unguessable URL-safe codes for the branch table QR column; a code is never derived
    /// from the table number or the branch id.
    /// </summary>
    public interface ITableQrCodeGenerator
    {
        string Generate();
    }
}
