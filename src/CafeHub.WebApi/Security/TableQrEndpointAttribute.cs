namespace CafeHub.WebApi.Security
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TableQrEndpointAttribute : Attribute
    {
        public bool IsGuest { get; set; }
    }
}
