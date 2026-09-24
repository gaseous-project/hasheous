using System.Data;
using Authentication;
using Microsoft.AspNetCore.Identity;

namespace Classes.Supporters
{
    /// <summary>
    /// Coordinates supporter link persistence, provider synchronization, and Hasheous supporter role assignment.
    /// </summary>
    public class SupporterRecognitionService
    {
        private readonly Database _database;
        private readonly UserStore _userStore;
        private readonly List<ISupporterProvider> _providers;

        /// <summary>
        /// Initializes a new instance of the <see cref="SupporterRecognitionService"/> class.
        /// </summary>
        public SupporterRecognitionService()
        {
            _database = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            _userStore = new UserStore(_database);
            _providers = new List<ISupporterProvider>
            {
                new OpenCollectiveSupporterProvider()
            };
        }

        /// <summary>
        /// Determines whether a payment date currently qualifies for active supporter recognition.
        /// </summary>
        /// <param name="lastPaymentUtc">The UTC timestamp of the most recent payment.</param>
        /// <param name="utcNow">The current UTC time.</param>
        /// <returns><c>true</c> when the contribution is still active; otherwise, <c>false</c>.</returns>
        public static bool IsSupporterActive(DateTime? lastPaymentUtc, DateTime utcNow)
        {
            if (lastPaymentUtc == null)
            {
                return false;
            }

            DateTime normalizedLastPayment = DateTime.SpecifyKind(lastPaymentUtc.Value, DateTimeKind.Utc);
            return utcNow <= normalizedLastPayment.AddDays(GetActiveContributionDays());
        }

        /// <summary>
        /// Calculates the UTC timestamp at which supporter recognition should expire.
        /// </summary>
        /// <param name="lastPaymentUtc">The UTC timestamp of the most recent payment.</param>
        /// <returns>The UTC expiration timestamp when a payment exists; otherwise, <c>null</c>.</returns>
        public static DateTime? GetActiveUntilUtc(DateTime? lastPaymentUtc)
        {
            if (lastPaymentUtc == null)
            {
                return null;
            }

            DateTime normalizedLastPayment = DateTime.SpecifyKind(lastPaymentUtc.Value, DateTimeKind.Utc);
            return normalizedLastPayment.AddDays(GetActiveContributionDays());
        }

        /// <summary>
        /// Persists or updates a supporter account link for a Hasheous user.
        /// </summary>
        /// <param name="userId">The Hasheous user identifier.</param>
        /// <param name="provider">The supporter provider name.</param>
        /// <param name="providerAccountId">The provider-specific account identifier.</param>
        /// <param name="providerAccountSlug">The provider-specific account slug or handle.</param>
        /// <param name="providerDisplayName">The provider-specific display name.</param>
        public async Task UpsertUserSupporterLinkAsync(string userId, string provider, string providerAccountId, string? providerAccountSlug, string? providerDisplayName)
        {
            DateTime utcNow = DateTime.UtcNow;
            string sql = """
                INSERT INTO UserSupporterLinks
                (
                    UserId,
                    Provider,
                    ProviderAccountId,
                    ProviderAccountSlug,
                    ProviderDisplayName,
                    LinkedAtUtc,
                    LastSyncedUtc,
                    IsActive
                )
                VALUES
                (
                    @userId,
                    @provider,
                    @providerAccountId,
                    @providerAccountSlug,
                    @providerDisplayName,
                    @linkedAtUtc,
                    @lastSyncedUtc,
                    @isActive
                )
                ON DUPLICATE KEY UPDATE
                    ProviderAccountId = @providerAccountId,
                    ProviderAccountSlug = @providerAccountSlug,
                    ProviderDisplayName = @providerDisplayName
                """;

            await _database.ExecuteTransactionCMDAsync(new List<Database.SQLTransactionItem>
            {
                new Database.SQLTransactionItem(sql, new Dictionary<string, object>
                {
                    { "userId", userId },
                    { "provider", provider },
                    { "providerAccountId", providerAccountId },
                    { "providerAccountSlug", providerAccountSlug ?? (object)DBNull.Value },
                    { "providerDisplayName", providerDisplayName ?? (object)DBNull.Value },
                    { "linkedAtUtc", utcNow },
                    { "lastSyncedUtc", utcNow },
                    { "isActive", false }
                })
            });
        }

        /// <summary>
        /// Deletes a supporter account link and refreshes the user's supporter role.
        /// </summary>
        /// <param name="userId">The Hasheous user identifier.</param>
        /// <param name="provider">The supporter provider name.</param>
        public async Task DeleteUserSupporterLinkAsync(string userId, string provider)
        {
            await _database.ExecuteTransactionCMDAsync(new List<Database.SQLTransactionItem>
            {
                new Database.SQLTransactionItem(
                    "DELETE FROM UserSupporterLinks WHERE UserId=@userId AND Provider=@provider;",
                    new Dictionary<string, object>
                    {
                        { "userId", userId },
                        { "provider", provider }
                    })
            });

            await RefreshSupporterRoleAsync(userId);
        }

        /// <summary>
        /// Returns the supporter provider status items visible to a specific user.
        /// </summary>
        /// <param name="userId">The Hasheous user identifier.</param>
        /// <returns>The supporter status rows for configured or linked providers.</returns>
        public async Task<List<SupporterProviderStatusItem>> GetUserSupporterStatusesAsync(string userId)
        {
            List<UserSupporterLinkItem> links = await GetUserSupporterLinksAsync(userId);
            Dictionary<string, UserSupporterLinkItem> linkLookup = links.ToDictionary(link => link.Provider, StringComparer.OrdinalIgnoreCase);
            List<SupporterProviderStatusItem> statuses = new List<SupporterProviderStatusItem>();

            foreach (ISupporterProvider provider in _providers)
            {
                bool includeProvider = provider.IsLinkingEnabled || provider.IsSyncEnabled || linkLookup.ContainsKey(provider.ProviderName);
                if (!includeProvider)
                {
                    continue;
                }

                linkLookup.TryGetValue(provider.ProviderName, out UserSupporterLinkItem? link);
                statuses.Add(new SupporterProviderStatusItem
                {
                    Provider = provider.ProviderName,
                    IsLinkingEnabled = provider.IsLinkingEnabled,
                    IsSyncEnabled = provider.IsSyncEnabled,
                    IsLinked = link != null,
                    ProviderAccountSlug = link?.ProviderAccountSlug,
                    ProviderDisplayName = link?.ProviderDisplayName,
                    LastPaymentUtc = link?.LastPaymentUtc,
                    ActiveUntilUtc = link?.ActiveUntilUtc,
                    LastSyncedUtc = link?.LastSyncedUtc,
                    IsActive = link?.IsActive ?? false
                });
            }

            return statuses.OrderBy(status => status.Provider, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Synchronizes all configured supporter providers.
        /// </summary>
        public async Task SyncAllAsync()
        {
            DateTime utcNow = DateTime.UtcNow;
            foreach (ISupporterProvider provider in _providers)
            {
                if (!provider.IsSyncEnabled)
                {
                    continue;
                }

                try
                {
                    await SyncProviderAsync(provider, utcNow, null);
                }
                catch (Exception ex)
                {
                    Logging.Log(Logging.LogType.Warning, "Supporter Recognition", $"Unable to synchronize provider {provider.ProviderName}.", ex);
                }
            }
        }

        /// <summary>
        /// Synchronizes supporter data for a single user and provider.
        /// </summary>
        /// <param name="userId">The Hasheous user identifier.</param>
        /// <param name="providerName">The supporter provider name.</param>
        public async Task SyncUserAsync(string userId, string providerName)
        {
            ISupporterProvider? provider = GetProvider(providerName);
            if (provider == null || !provider.IsSyncEnabled)
            {
                await RefreshSupporterRoleAsync(userId);
                return;
            }

            try
            {
                await SyncProviderAsync(provider, DateTime.UtcNow, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { userId });
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Warning, "Supporter Recognition", $"Unable to synchronize supporter data for user {userId} and provider {providerName}.", ex);
            }
        }

        /// <summary>
        /// Recalculates and applies the Hasheous supporter role for a specific user.
        /// </summary>
        /// <param name="userId">The Hasheous user identifier.</param>
        public async Task RefreshSupporterRoleAsync(string userId)
        {
            ApplicationUser? user = await _userStore.FindByIdAsync(userId, CancellationToken.None);
            if (user == null)
            {
                return;
            }

            bool hasActiveSupport = await UserHasActiveSupporterLinkAsync(userId);
            bool alreadyHasRole = await _userStore.IsInRoleAsync(user, SupporterConstants.SupporterRoleName, CancellationToken.None);

            if (hasActiveSupport && !alreadyHasRole)
            {
                await _userStore.AddToRoleAsync(user, SupporterConstants.SupporterRoleName, CancellationToken.None);
            }
            else if (!hasActiveSupport && alreadyHasRole)
            {
                await _userStore.RemoveFromRoleAsync(user, SupporterConstants.SupporterRoleName, CancellationToken.None);
            }
        }

        /// <summary>
        /// Synchronizes a single provider and optionally limits the work to specific user identifiers.
        /// </summary>
        /// <param name="provider">The provider to synchronize.</param>
        /// <param name="utcNow">The current UTC timestamp.</param>
        /// <param name="userIds">An optional set of user identifiers to limit the synchronization to.</param>
        private async Task SyncProviderAsync(ISupporterProvider provider, DateTime utcNow, HashSet<string>? userIds)
        {
            List<SupporterContributionRecord> contributions = await provider.GetRecentContributionsAsync(utcNow);
            Dictionary<string, SupporterContributionRecord> contributionLookup = BuildLatestContributionLookup(contributions);
            List<UserSupporterLinkItem> links = await GetUserSupporterLinksAsync(provider.ProviderName, userIds);
            List<Database.SQLTransactionItem> transactionItems = new List<Database.SQLTransactionItem>();
            HashSet<string> usersToRefresh = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (UserSupporterLinkItem link in links)
            {
                SupporterContributionRecord? contribution = FindContributionForLink(link, contributionLookup);
                DateTime? lastPaymentUtc = contribution?.LastPaymentUtc;
                DateTime? activeUntilUtc = GetActiveUntilUtc(lastPaymentUtc);
                bool isActive = IsSupporterActive(lastPaymentUtc, utcNow);

                transactionItems.Add(new Database.SQLTransactionItem(
                    """
                    UPDATE UserSupporterLinks
                    SET
                        ProviderAccountSlug=@providerAccountSlug,
                        ProviderDisplayName=@providerDisplayName,
                        LastPaymentUtc=@lastPaymentUtc,
                        ActiveUntilUtc=@activeUntilUtc,
                        LastSyncedUtc=@lastSyncedUtc,
                        IsActive=@isActive
                    WHERE Id=@id;
                    """,
                    new Dictionary<string, object>
                    {
                        { "id", link.Id },
                        { "providerAccountSlug", contribution?.AccountSlug ?? link.ProviderAccountSlug ?? (object)DBNull.Value },
                        { "providerDisplayName", contribution?.DisplayName ?? link.ProviderDisplayName ?? (object)DBNull.Value },
                        { "lastPaymentUtc", lastPaymentUtc ?? (object)DBNull.Value },
                        { "activeUntilUtc", activeUntilUtc ?? (object)DBNull.Value },
                        { "lastSyncedUtc", utcNow },
                        { "isActive", isActive }
                    }));

                usersToRefresh.Add(link.UserId);
            }

            if (transactionItems.Count > 0)
            {
                await _database.ExecuteTransactionCMDAsync(transactionItems);
            }

            foreach (string userId in usersToRefresh)
            {
                await RefreshSupporterRoleAsync(userId);
            }
        }

        /// <summary>
        /// Builds a lookup containing the latest contribution per provider account identifier.
        /// </summary>
        /// <param name="contributions">The normalized provider contributions.</param>
        /// <returns>The latest contribution keyed by provider account identifier.</returns>
        private static Dictionary<string, SupporterContributionRecord> BuildLatestContributionLookup(IEnumerable<SupporterContributionRecord> contributions)
        {
            Dictionary<string, SupporterContributionRecord> lookup = new Dictionary<string, SupporterContributionRecord>(StringComparer.OrdinalIgnoreCase);

            foreach (SupporterContributionRecord contribution in contributions)
            {
                if (string.IsNullOrWhiteSpace(contribution.AccountId))
                {
                    continue;
                }

                if (!lookup.TryGetValue(contribution.AccountId, out SupporterContributionRecord? existingContribution)
                    || contribution.LastPaymentUtc > existingContribution.LastPaymentUtc)
                {
                    lookup[contribution.AccountId] = contribution;
                }
            }

            return lookup;
        }

        /// <summary>
        /// Resolves the contribution that best matches a persisted supporter link.
        /// </summary>
        /// <param name="link">The persisted supporter link.</param>
        /// <param name="contributionLookup">The contribution lookup keyed by account identifier.</param>
        /// <returns>The matching contribution when found; otherwise, <c>null</c>.</returns>
        private static SupporterContributionRecord? FindContributionForLink(UserSupporterLinkItem link, Dictionary<string, SupporterContributionRecord> contributionLookup)
        {
            if (contributionLookup.TryGetValue(link.ProviderAccountId, out SupporterContributionRecord? contribution))
            {
                return contribution;
            }

            if (!string.IsNullOrWhiteSpace(link.ProviderAccountSlug))
            {
                return contributionLookup.Values.FirstOrDefault(candidate =>
                    string.Equals(candidate.AccountSlug, link.ProviderAccountSlug, StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        /// <summary>
        /// Retrieves persisted supporter links for a user.
        /// </summary>
        /// <param name="userId">The Hasheous user identifier.</param>
        /// <returns>The user's supporter links.</returns>
        private async Task<List<UserSupporterLinkItem>> GetUserSupporterLinksAsync(string userId)
        {
            List<Dictionary<string, object>> rows = await _database.ExecuteCMDDictAsync(
                "SELECT * FROM UserSupporterLinks WHERE UserId=@userId ORDER BY Provider;",
                new Dictionary<string, object>
                {
                    { "userId", userId }
                });

            return rows.Select(MapSupporterLink).ToList();
        }

        /// <summary>
        /// Retrieves persisted supporter links for a provider, optionally filtered to specific users.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="userIds">An optional set of Hasheous user identifiers.</param>
        /// <returns>The supporter links matching the requested provider and users.</returns>
        private async Task<List<UserSupporterLinkItem>> GetUserSupporterLinksAsync(string provider, HashSet<string>? userIds)
        {
            string sql = "SELECT * FROM UserSupporterLinks WHERE Provider=@provider";
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "provider", provider }
            };

            if (userIds != null && userIds.Count > 0)
            {
                List<string> parameterNames = new List<string>();
                int index = 0;
                foreach (string userId in userIds)
                {
                    string parameterName = $"userId{index}";
                    parameterNames.Add($"@{parameterName}");
                    parameters.Add(parameterName, userId);
                    index++;
                }

                sql += $" AND UserId IN ({string.Join(", ", parameterNames)})";
            }

            sql += " ORDER BY UserId;";

            List<Dictionary<string, object>> rows = await _database.ExecuteCMDDictAsync(sql, parameters);
            return rows.Select(MapSupporterLink).ToList();
        }

        /// <summary>
        /// Determines whether a user currently has at least one active supporter link.
        /// </summary>
        /// <param name="userId">The Hasheous user identifier.</param>
        /// <returns><c>true</c> when an active supporter link exists; otherwise, <c>false</c>.</returns>
        private async Task<bool> UserHasActiveSupporterLinkAsync(string userId)
        {
            DataTable table = await _database.ExecuteCMDAsync(
                "SELECT 1 FROM UserSupporterLinks WHERE UserId=@userId AND IsActive=1 LIMIT 1;",
                new Dictionary<string, object>
                {
                    { "userId", userId }
                });

            return table.Rows.Count > 0;
        }

        /// <summary>
        /// Maps a supporter link database row to a strongly typed model.
        /// </summary>
        /// <param name="row">The raw database row.</param>
        /// <returns>The mapped supporter link model.</returns>
        private static UserSupporterLinkItem MapSupporterLink(Dictionary<string, object> row)
        {
            return new UserSupporterLinkItem
            {
                Id = Convert.ToInt64(row["Id"]),
                UserId = Convert.ToString(row["UserId"]) ?? string.Empty,
                Provider = Convert.ToString(row["Provider"]) ?? string.Empty,
                ProviderAccountId = Convert.ToString(row["ProviderAccountId"]) ?? string.Empty,
                ProviderAccountSlug = row["ProviderAccountSlug"] == DBNull.Value ? null : Convert.ToString(row["ProviderAccountSlug"]),
                ProviderDisplayName = row["ProviderDisplayName"] == DBNull.Value ? null : Convert.ToString(row["ProviderDisplayName"]),
                LinkedAtUtc = DateTime.SpecifyKind(Convert.ToDateTime(row["LinkedAtUtc"]), DateTimeKind.Utc),
                LastPaymentUtc = row["LastPaymentUtc"] == DBNull.Value ? null : DateTime.SpecifyKind(Convert.ToDateTime(row["LastPaymentUtc"]), DateTimeKind.Utc),
                ActiveUntilUtc = row["ActiveUntilUtc"] == DBNull.Value ? null : DateTime.SpecifyKind(Convert.ToDateTime(row["ActiveUntilUtc"]), DateTimeKind.Utc),
                LastSyncedUtc = row["LastSyncedUtc"] == DBNull.Value ? null : DateTime.SpecifyKind(Convert.ToDateTime(row["LastSyncedUtc"]), DateTimeKind.Utc),
                IsActive = Convert.ToBoolean(row["IsActive"]),
                MetadataJson = row["MetadataJson"] == DBNull.Value ? null : Convert.ToString(row["MetadataJson"])
            };
        }

        /// <summary>
        /// Returns the provider instance that matches the supplied provider name.
        /// </summary>
        /// <param name="providerName">The provider name to search for.</param>
        /// <returns>The matching provider when found; otherwise, <c>null</c>.</returns>
        private ISupporterProvider? GetProvider(string providerName)
        {
            return _providers.FirstOrDefault(provider =>
                string.Equals(provider.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns the configured number of active contribution days, falling back to the default when configuration is unavailable.
        /// </summary>
        /// <returns>The number of days a contribution remains active.</returns>
        private static int GetActiveContributionDays()
        {
            return Math.Max(1, Config.SupporterRecognitionConfiguration.ActiveContributionDays);
        }
    }
}
