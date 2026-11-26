using System.Threading;
using System.Threading.Tasks;

namespace StopWatch
{
    /// <summary>
    /// Where the platform keeps secrets (today: the Jira API token). Async
    /// because a locked keyring can block for many seconds while the user is
    /// asked to unlock it: callers must never wait on it from the UI thread.
    ///
    /// Every failure (no keyring service, locked and not unlocked, a stored
    /// value that cannot be decrypted) is a <see cref="SecretStoreException"/>
    /// whose message is fit to show the user. An implementation never falls
    /// back to storing a secret in clear text.
    /// </summary>
    public interface ISecretStore
    {
        /// <summary>The secret stored under <paramref name="key"/>, or null if there is none.</summary>
        Task<string> GetAsync(string key, CancellationToken cancellationToken = default);

        Task SetAsync(string key, string secret, CancellationToken cancellationToken = default);

        /// <summary>Removes the secret. Not an error if there was none.</summary>
        Task DeleteAsync(string key, CancellationToken cancellationToken = default);
    }


    /// <summary>The keys the app stores secrets under.</summary>
    public static class SecretKeys
    {
        public const string JiraApiToken = "jira-api-token";
    }
}
