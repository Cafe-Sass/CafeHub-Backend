using CafeHub.Infrastructure.Identity;

namespace CafeHub.UnitTests
{
    public sealed class IdentityLockoutOptionsTests
    {
        private const string AttemptsMessage = $"{IdentityConfigurationKeys.MaxFailedAccessAttemptsPath} must be between 3 and 20 but was ";

        private const string MinutesMessage = $"{IdentityConfigurationKeys.LockoutMinutesPath} must be between 1 and 1440 but was ";

        [Fact]
        public void FreshOptions_UseTheDocumentedDefaults_AndPassValidation()
        {
            var options = new IdentityLockoutOptions();

            Assert.Equal(5, options.MaxFailedAccessAttempts);
            Assert.Equal(15, options.LockoutMinutes);
            Assert.Empty(options.Validate());
        }

        [Theory]
        [InlineData(2)]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(21)]
        [InlineData(100)]
        public void Validate_RejectsAFailedAttemptLimitOutsideTheThreeToTwentyRange(int attempts)
        {
            var options = new IdentityLockoutOptions { MaxFailedAccessAttempts = attempts };

            var problem = Assert.Single(options.Validate());

            Assert.Equal($"{AttemptsMessage}{attempts}.", problem);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(20)]
        public void Validate_AcceptsAFailedAttemptLimitInsideTheThreeToTwentyRange(int attempts)
        {
            var options = new IdentityLockoutOptions { MaxFailedAccessAttempts = attempts };

            Assert.Empty(options.Validate());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(1441)]
        [InlineData(100000)]
        public void Validate_RejectsALockoutDurationOutsideTheOneToFourteenHundredFortyMinuteRange(int lockoutMinutes)
        {
            var options = new IdentityLockoutOptions { LockoutMinutes = lockoutMinutes };

            var problem = Assert.Single(options.Validate());

            Assert.Equal($"{MinutesMessage}{lockoutMinutes}.", problem);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(1440)]
        public void Validate_AcceptsALockoutDurationInsideTheOneToFourteenHundredFortyMinuteRange(int lockoutMinutes)
        {
            var options = new IdentityLockoutOptions { LockoutMinutes = lockoutMinutes };

            Assert.Empty(options.Validate());
        }

        [Fact]
        public void Validate_AcceptsTheWidestConfiguredThresholds()
        {
            var options = new IdentityLockoutOptions
            {
                MaxFailedAccessAttempts = 20,
                LockoutMinutes = 1440,
            };

            Assert.Empty(options.Validate());
        }

        [Fact]
        public void Validate_ReportsBothProblems_WhenBothThresholdsAreOutsideTheirRanges()
        {
            var options = new IdentityLockoutOptions
            {
                MaxFailedAccessAttempts = 2,
                LockoutMinutes = 0,
            };

            var problems = options.Validate();

            Assert.Equal(2, problems.Count);
            Assert.Contains(problems, problem => problem == $"{AttemptsMessage}2.");
            Assert.Contains(problems, problem => problem == $"{MinutesMessage}0.");
        }
    }
}
