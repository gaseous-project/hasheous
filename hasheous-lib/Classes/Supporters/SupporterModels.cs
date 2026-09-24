namespace Classes.Supporters
{
    /// <summary>
    /// Represents a normalized supporter contribution returned by a payment provider.
    /// </summary>
    public class SupporterContributionRecord
    {
        /// <summary>
        /// Gets or sets the provider name that produced the contribution record.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the provider-specific account identifier.
        /// </summary>
        public string AccountId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the provider-specific account slug or handle.
        /// </summary>
        public string? AccountSlug { get; set; }

        /// <summary>
        /// Gets or sets the provider-specific display name for the account.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the most recent payment timestamp recorded for the account.
        /// </summary>
        public DateTime LastPaymentUtc { get; set; }
    }

    /// <summary>
    /// Represents a persisted supporter account link for a Hasheous user.
    /// </summary>
    public class UserSupporterLinkItem
    {
        /// <summary>
        /// Gets or sets the database identifier for the supporter link row.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the Hasheous user identifier.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the provider name for the linked account.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the provider-specific account identifier.
        /// </summary>
        public string ProviderAccountId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the provider-specific account slug or handle.
        /// </summary>
        public string? ProviderAccountSlug { get; set; }

        /// <summary>
        /// Gets or sets the provider-specific display name captured during link or sync.
        /// </summary>
        public string? ProviderDisplayName { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the account link was created.
        /// </summary>
        public DateTime LinkedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp of the most recent qualifying payment.
        /// </summary>
        public DateTime? LastPaymentUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp through which the user remains active.
        /// </summary>
        public DateTime? ActiveUntilUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp of the last synchronization attempt.
        /// </summary>
        public DateTime? LastSyncedUtc { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the supporter entitlement is currently active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets provider-specific metadata captured for debugging or future expansion.
        /// </summary>
        public string? MetadataJson { get; set; }
    }

    /// <summary>
    /// Represents supporter recognition status returned to the account management UI.
    /// </summary>
    public class SupporterProviderStatusItem
    {
        /// <summary>
        /// Gets or sets the provider name.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether account linking is configured for the provider.
        /// </summary>
        public bool IsLinkingEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether background synchronization is configured for the provider.
        /// </summary>
        public bool IsSyncEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the current user has linked the provider account.
        /// </summary>
        public bool IsLinked { get; set; }

        /// <summary>
        /// Gets or sets the provider-specific account slug or handle.
        /// </summary>
        public string? ProviderAccountSlug { get; set; }

        /// <summary>
        /// Gets or sets the provider-specific display name.
        /// </summary>
        public string? ProviderDisplayName { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp of the last qualifying payment.
        /// </summary>
        public DateTime? LastPaymentUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp through which the entitlement remains active.
        /// </summary>
        public DateTime? ActiveUntilUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp of the last successful synchronization.
        /// </summary>
        public DateTime? LastSyncedUtc { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the entitlement is currently active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets the Hasheous role name granted for active supporters.
        /// </summary>
        public string RoleName { get; set; } = SupporterConstants.SupporterRoleName;
    }
}
