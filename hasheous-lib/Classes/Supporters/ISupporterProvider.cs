namespace Classes.Supporters
{
    /// <summary>
    /// Defines the contract implemented by payment providers that can recognize Hasheous supporters.
    /// </summary>
    public interface ISupporterProvider
    {
        /// <summary>
        /// Gets the provider name used for persistence and display.
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Gets a value indicating whether user account linking is configured for this provider.
        /// </summary>
        bool IsLinkingEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether background supporter synchronization is configured for this provider.
        /// </summary>
        bool IsSyncEnabled { get; }

        /// <summary>
        /// Retrieves the latest contributions that should count toward active supporter recognition.
        /// </summary>
        /// <param name="utcNow">The current UTC time used when constructing provider queries.</param>
        /// <param name="cancellationToken">The cancellation token for the provider request.</param>
        /// <returns>The normalized contribution records for the provider.</returns>
        Task<List<SupporterContributionRecord>> GetRecentContributionsAsync(DateTime utcNow, CancellationToken cancellationToken = default);
    }
}
