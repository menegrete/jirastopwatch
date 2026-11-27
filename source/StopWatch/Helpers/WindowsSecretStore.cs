using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StopWatch
{
    /// <summary>
    /// <see cref="ISecretStore"/> on Windows: DPAPI, current-user scope, no
    /// entropy, UTF-8, kept as base64 in a single string. That is exactly what
    /// the old hand-written DPAPI wrapper produced and stored in user.config's
    /// ApiToken, so a token saved by an earlier version still decrypts.
    ///
    /// There is one blob, so <c>key</c> is not used to tell secrets apart; the
    /// only secret the app keeps is the Jira API token.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class WindowsSecretStore : ISecretStore
    {
        private readonly Func<string> readBlob;
        private readonly Action<string> writeBlob;

        /// <param name="readBlob">Returns the stored base64 blob, or "" if none.</param>
        /// <param name="writeBlob">Stores the base64 blob ("" to clear).</param>
        public WindowsSecretStore(Func<string> readBlob, Action<string> writeBlob)
        {
            this.readBlob = readBlob ?? throw new ArgumentNullException(nameof(readBlob));
            this.writeBlob = writeBlob ?? throw new ArgumentNullException(nameof(writeBlob));
        }


        public Task<string> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.Run(() =>
            {
                string blob = readBlob();
                if (string.IsNullOrEmpty(blob))
                    return (string)null;

                try
                {
                    byte[] plain = ProtectedData.Unprotect(Convert.FromBase64String(blob), null, DataProtectionScope.CurrentUser);
                    return Encoding.UTF8.GetString(plain);
                }
                catch (Exception ex) when (ex is CryptographicException || ex is FormatException)
                {
                    throw new SecretStoreException("The saved Jira API token could not be decrypted (it was saved by another Windows user or machine). Please enter it again in Settings.", ex);
                }
            }, cancellationToken);


        public Task SetAsync(string key, string secret, CancellationToken cancellationToken = default) =>
            Task.Run(() =>
            {
                try
                {
                    byte[] cipher = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), null, DataProtectionScope.CurrentUser);
                    writeBlob(Convert.ToBase64String(cipher));
                }
                catch (CryptographicException ex)
                {
                    throw new SecretStoreException("The Jira API token could not be encrypted, so it was not saved.", ex);
                }
            }, cancellationToken);


        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
            Task.Run(() => writeBlob(""), cancellationToken);
    }
}
