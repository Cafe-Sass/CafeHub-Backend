namespace CafeHub.WebApi.Controllers
{
    public sealed record UpdateAttendanceSettingsRequest(string Mode, string ConcurrencyToken);
}
