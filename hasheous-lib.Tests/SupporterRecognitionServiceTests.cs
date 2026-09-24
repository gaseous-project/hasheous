using Classes.Supporters;

namespace hasheous_lib.Tests;

public class SupporterRecognitionServiceTests
{
    [Fact]
    public void ContributionRemainsActiveWithinThirtyDays()
    {
        DateTime utcNow = new DateTime(2026, 8, 22, 8, 0, 0, DateTimeKind.Utc);
        DateTime lastPaymentUtc = utcNow.AddDays(-29).AddHours(-23);

        bool isActive = SupporterRecognitionService.IsSupporterActive(lastPaymentUtc, utcNow);

        Assert.True(isActive);
    }

    [Fact]
    public void ContributionExpiresAfterThirtyDays()
    {
        DateTime utcNow = new DateTime(2026, 8, 22, 8, 0, 1, DateTimeKind.Utc);
        DateTime lastPaymentUtc = utcNow.AddDays(-30).AddSeconds(-1);

        bool isActive = SupporterRecognitionService.IsSupporterActive(lastPaymentUtc, utcNow);

        Assert.False(isActive);
    }

    [Fact]
    public void ActiveUntilIsThirtyDaysAfterPayment()
    {
        DateTime lastPaymentUtc = new DateTime(2026, 8, 1, 12, 30, 0, DateTimeKind.Utc);

        DateTime? activeUntilUtc = SupporterRecognitionService.GetActiveUntilUtc(lastPaymentUtc);

        Assert.Equal(new DateTime(2026, 8, 31, 12, 30, 0, DateTimeKind.Utc), activeUntilUtc);
    }

    [Fact]
    public void NullPaymentIsInactiveAndHasNoExpiry()
    {
        bool isActive = SupporterRecognitionService.IsSupporterActive(null, DateTime.UtcNow);
        DateTime? activeUntilUtc = SupporterRecognitionService.GetActiveUntilUtc(null);

        Assert.False(isActive);
        Assert.Null(activeUntilUtc);
    }
}
