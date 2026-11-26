namespace StopWatchTest
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using StopWatch;


    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public class WindowsSecretStoreTest
    {
        private string blob;
        private WindowsSecretStore store;

        [SetUp]
        public void SetUp()
        {
            blob = "";
            store = new WindowsSecretStore(() => blob, b => blob = b);
        }


        // The token a previous version saved: DPAPI.Encrypt(token), base64 in user.config.
        [Test]
        public async Task GetAsync_DecryptsBlobProducedByTheOldDpapiCode()
        {
            blob = LegacyDpapi.Encrypt("tökén-123");

            Assert.That(await store.GetAsync(SecretKeys.JiraApiToken), Is.EqualTo("tökén-123"));
        }


        // And the other way round, so rolling back to an older version still reads the token.
        [Test]
        public async Task SetAsync_ProducesBlobTheOldDpapiCodeDecrypts()
        {
            await store.SetAsync(SecretKeys.JiraApiToken, "tökén-123");

            Assert.That(blob, Is.Not.Empty);
            Assert.That(blob, Does.Not.Contain("123"));
            Assert.That(LegacyDpapi.Decrypt(blob), Is.EqualTo("tökén-123"));
        }


        [Test]
        public async Task GetAsync_NoBlob_ReturnsNull()
        {
            Assert.That(await store.GetAsync(SecretKeys.JiraApiToken), Is.Null);
        }


        [Test]
        public async Task DeleteAsync_ClearsTheBlob()
        {
            await store.SetAsync(SecretKeys.JiraApiToken, "abc");

            await store.DeleteAsync(SecretKeys.JiraApiToken);

            Assert.That(blob, Is.Empty);
            Assert.That(await store.GetAsync(SecretKeys.JiraApiToken), Is.Null);
        }


        [Test]
        public void GetAsync_BlobNotDecryptable_ThrowsSecretStoreException()
        {
            blob = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });

            Assert.ThrowsAsync<SecretStoreException>(() => store.GetAsync(SecretKeys.JiraApiToken));
        }
    }


    [TestFixture]
    [Platform("Linux")]
    [SupportedOSPlatform("linux")]
    public class LinuxSecretStoreTest
    {
        // Its own key: never touches a real token saved by the app.
        private const string TestKey = "jira-api-token-test";

        [DllImport("libc", SetLastError = true)] private static extern int setenv(string name, string value, int overwrite);
        [DllImport("libc", SetLastError = true)] private static extern int unsetenv(string name);


        // Runs first, before anything has connected to the session bus (libsecret keeps the
        // connection once it has one). Pointing the bus at a socket that does not exist is what
        // "no keyring service" looks like to libsecret.
        [Test, Order(1)]
        public void KeyringUnavailable_FailsWithASecretStoreException_AndNothingElse()
        {
            const string Variable = "DBUS_SESSION_BUS_ADDRESS";
            string original = Environment.GetEnvironmentVariable(Variable);
            setenv(Variable, "unix:path=/nonexistent/jirastopwatch-test-bus", 1);
            try
            {
                var store = new LinuxSecretStore(TimeSpan.FromSeconds(15)); // a locked keyring with no prompter never answers

                var ex = Assert.ThrowsAsync<SecretStoreException>(() => store.SetAsync(TestKey, "s3cret"));
                Assert.That(ex.Message, Does.Contain("keyring"));
                Assert.ThrowsAsync<SecretStoreException>(() => store.GetAsync(TestKey));
            }
            finally
            {
                if (original == null)
                    unsetenv(Variable);
                else
                    setenv(Variable, original, 1);
            }
        }


        [Test, Order(2)]
        public async Task StoreReadAndDelete()
        {
            var store = new LinuxSecretStore(TimeSpan.FromSeconds(15)); // a locked keyring with no prompter never answers
            try
            {
                await store.SetAsync(TestKey, "probe"); // a locked keyring still answers lookups (with nothing); only a write needs it open
            }
            catch (SecretStoreException ex)
            {
                Assert.Ignore("No usable keyring in this session: " + ex.Message);
            }

            try
            {
                await store.SetAsync(TestKey, "tökén-123");
                Assert.That(await store.GetAsync(TestKey), Is.EqualTo("tökén-123"));

                await store.SetAsync(TestKey, "replaced");
                Assert.That(await store.GetAsync(TestKey), Is.EqualTo("replaced"));

                await store.DeleteAsync(TestKey);
                Assert.That(await store.GetAsync(TestKey), Is.Null);

                // Deleting what is not there is not an error.
                await store.DeleteAsync(TestKey);
            }
            finally
            {
                await store.DeleteAsync(TestKey);
            }
        }
    }


    // Settings <-> ISecretStore, with a fake store (runs everywhere).
    [TestFixture]
    public class SettingsSecretTest
    {
        private class FakeSecretStore : ISecretStore
        {
            public Dictionary<string, string> Items = new Dictionary<string, string>();
            public Exception Failure;

            public Task<string> GetAsync(string key, CancellationToken ct = default) =>
                Failure != null ? Task.FromException<string>(Failure) : Task.FromResult(Items.TryGetValue(key, out string v) ? v : null);

            public Task SetAsync(string key, string secret, CancellationToken ct = default)
            {
                if (Failure != null)
                    return Task.FromException(Failure);
                Items[key] = secret;
                return Task.CompletedTask;
            }

            public Task DeleteAsync(string key, CancellationToken ct = default)
            {
                Items.Remove(key);
                return Task.CompletedTask;
            }
        }

        private ISecretStore original;
        private FakeSecretStore store;

        [SetUp]
        public void SetUp()
        {
            original = Settings.SecretStore;
            store = new FakeSecretStore();
            Settings.SecretStore = store;
        }

        [TearDown]
        public void TearDown() => Settings.SecretStore = original;


        [Test]
        public async Task LoadApiTokenAsync_ReadsTheStoredToken()
        {
            store.Items[SecretKeys.JiraApiToken] = "abc";
            var settings = new Settings();

            await settings.LoadApiTokenAsync();

            Assert.That(settings.ApiToken, Is.EqualTo("abc"));
        }


        [Test]
        public async Task LoadApiTokenAsync_NoStoredToken_IsEmpty()
        {
            var settings = new Settings { ApiToken = "stale" };

            await settings.LoadApiTokenAsync();

            Assert.That(settings.ApiToken, Is.EqualTo(""));
        }


        [Test]
        public void LoadApiTokenAsync_StoreFails_Throws_AndTokenStaysEmpty()
        {
            store.Failure = new SecretStoreException("locked");
            var settings = new Settings { ApiToken = "stale" };

            Assert.ThrowsAsync<SecretStoreException>(() => settings.LoadApiTokenAsync());
            Assert.That(settings.ApiToken, Is.EqualTo(""));
        }


        [Test]
        public async Task SaveApiTokenAsync_StoresTheToken_AndRemovesItWhenEmpty()
        {
            var settings = new Settings { ApiToken = "abc" };

            await settings.SaveApiTokenAsync();
            Assert.That(store.Items[SecretKeys.JiraApiToken], Is.EqualTo("abc"));

            settings.ApiToken = "";
            await settings.SaveApiTokenAsync();
            Assert.That(store.Items, Is.Empty);
        }
    }
}
