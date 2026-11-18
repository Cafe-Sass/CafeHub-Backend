namespace CafeHub.UnitTests.Menu.Persistence
{
    public sealed class Task47PostgreSqlFactAttribute : FactAttribute
    {
        internal const string ConnectionVariable = "CAFEHUB_TASK47_DISPOSABLE_POSTGRES";

        public Task47PostgreSqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
            {
                Skip = "Requires an explicitly designated disposable loopback PostgreSQL cluster with database and role creation privileges.";
            }
        }
    }
}
