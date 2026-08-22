namespace Classes.Supporters
{
    /// <summary>
    /// Defines shared constant values used by supporter recognition features.
    /// </summary>
    public static class SupporterConstants
    {
        /// <summary>
        /// The role automatically granted to users with any active supporter entitlement.
        /// </summary>
        public const string SupporterRoleName = "Supporter";

        /// <summary>
        /// The provider name used for OpenCollective account links and sync records.
        /// </summary>
        public const string OpenCollectiveProviderName = "OpenCollective";

        /// <summary>
        /// The custom claim type used to store the linked OpenCollective account slug.
        /// </summary>
        public const string OpenCollectiveSlugClaimType = "urn:hasheous:opencollective:slug";
    }
}
