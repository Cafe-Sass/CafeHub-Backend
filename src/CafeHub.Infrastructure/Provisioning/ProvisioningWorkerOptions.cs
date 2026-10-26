namespace CafeHub.Infrastructure.Provisioning
{
    public sealed class ProvisioningWorkerOptions
    {
        public bool Enabled { get; set; } = false;

        public int PollIntervalSeconds { get; set; } = 5;

        public int LeaseSeconds { get; set; } = 300;

        public string WorkerId { get; set; } = string.Empty;

        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();

            if (PollIntervalSeconds < 1 || PollIntervalSeconds > 300)
            {
                problems.Add("Provisioning:Worker:PollIntervalSeconds must be between 1 and 300.");
            }

            if (LeaseSeconds < 30 || LeaseSeconds > 3600)
            {
                problems.Add("Provisioning:Worker:LeaseSeconds must be between 30 and 3600.");
            }

            if (LeaseSeconds <= PollIntervalSeconds)
            {
                problems.Add("Provisioning:Worker:LeaseSeconds must exceed Provisioning:Worker:PollIntervalSeconds.");
            }

            return problems;
        }
    }
}

