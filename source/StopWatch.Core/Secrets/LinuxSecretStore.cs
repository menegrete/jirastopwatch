using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace StopWatch
{
    /// <summary>
    /// <see cref="ISecretStore"/> on the user's Secret Service keyring
    /// (gnome-keyring, KWallet, ...) through libsecret.
    ///
    /// Items use the schema <c>com.jirastopwatch.app</c> with one string
    /// attribute, <c>account</c>, whose value is the key passed in (for the
    /// API token, <see cref="SecretKeys.JiraApiToken"/>). The key is fixed, not
    /// derived from the Jira URL or user name, so editing either never orphans
    /// the stored token. The item's label is "Jira StopWatch " + key, and it is
    /// stored in the default collection.
    ///
    /// Plain P/Invoke on the non-variadic "v" functions (validated on Linux
    /// Mint 22.3 in the spikes/avalonia-linux project). The blocking calls run
    /// on the thread pool. Nothing here ever writes a secret anywhere else.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public sealed class LinuxSecretStore : ISecretStore
    {
        public const string SchemaName = "com.jirastopwatch.app";
        public const string AttributeName = "account";

        private const string Secret = "libsecret-1.so.0";
        private const string GLib = "libglib-2.0.so.0";
        private const string DefaultCollection = "default";

        #region native
        [DllImport(Secret)] private static extern IntPtr secret_schema_newv(string name, int flags, IntPtr attrs);
        [DllImport(Secret)] private static extern void secret_schema_unref(IntPtr schema);
        [DllImport(Secret)] private static extern int secret_password_storev_sync(IntPtr schema, IntPtr attrs, string collection, string label, string password, IntPtr cancellable, out IntPtr error);
        [DllImport(Secret)] private static extern IntPtr secret_password_lookupv_sync(IntPtr schema, IntPtr attrs, IntPtr cancellable, out IntPtr error);
        [DllImport(Secret)] private static extern int secret_password_clearv_sync(IntPtr schema, IntPtr attrs, IntPtr cancellable, out IntPtr error);
        [DllImport(Secret)] private static extern void secret_password_free(IntPtr password);

        [DllImport(GLib)] private static extern IntPtr g_hash_table_new(IntPtr hash, IntPtr equal);
        [DllImport(GLib)] private static extern void g_hash_table_insert(IntPtr table, IntPtr key, IntPtr value);
        [DllImport(GLib)] private static extern void g_hash_table_unref(IntPtr table);
        [DllImport(GLib)] private static extern void g_error_free(IntPtr error);
        #endregion


        // Time the keyring gets to answer, unlock prompt included. A locked keyring with no
        // prompter to unlock it never answers at all; the app must say so rather than hang.
        private readonly TimeSpan timeout;

        public LinuxSecretStore() : this(TimeSpan.FromSeconds(60))
        {
        }

        public LinuxSecretStore(TimeSpan timeout)
        {
            this.timeout = timeout;
        }


        #region ISecretStore
        public Task<string> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Run(() => Lookup(key), cancellationToken);

        public Task SetAsync(string key, string secret, CancellationToken cancellationToken = default) =>
            Run(() => { Store(key, secret); return (string)null; }, cancellationToken);

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
            Run(() => { Clear(key); return (string)null; }, cancellationToken);
        #endregion


        #region private methods
        private const string Unavailable =
            "The system keyring is not available, so the Jira API token could not be read or saved. " +
            "Install and unlock a keyring service (for example gnome-keyring) and try again.";

        private async Task<string> Run(Func<string> work, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Task<string> call = Task.Run(() =>
            {
                try
                {
                    return work();
                }
                catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException || ex is BadImageFormatException)
                {
                    throw new SecretStoreException(Unavailable + " (libsecret was not found.)", ex);
                }
            });

            // The native call cannot be cancelled: on timeout it is abandoned on its pool thread.
            Task first = await Task.WhenAny(call, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (first != call)
                throw new SecretStoreException(Unavailable + " (the keyring did not answer in time; it may be locked.)");

            return await call.ConfigureAwait(false);
        }


        private static void Store(string key, string secret)
        {
            With(key, (schema, attrs) =>
            {
                bool ok = secret_password_storev_sync(schema, attrs, DefaultCollection, "Jira StopWatch " + key, secret, IntPtr.Zero, out IntPtr err) != 0;
                if (!ok || err != IntPtr.Zero)
                    throw Failure(err);
                return 0;
            });
        }


        private static string Lookup(string key) =>
            With(key, (schema, attrs) =>
            {
                IntPtr p = secret_password_lookupv_sync(schema, attrs, IntPtr.Zero, out IntPtr err);
                if (err != IntPtr.Zero)
                    throw Failure(err);
                if (p == IntPtr.Zero)
                    return null;

                string s = Marshal.PtrToStringUTF8(p);
                secret_password_free(p);
                return s;
            });


        private static void Clear(string key)
        {
            With(key, (schema, attrs) =>
            {
                secret_password_clearv_sync(schema, attrs, IntPtr.Zero, out IntPtr err);
                if (err != IntPtr.Zero)
                    throw Failure(err);
                return 0;
            });
        }


        // GError { guint32 domain; gint code; gchar* message; }: message at offset 8 on 64-bit.
        private static SecretStoreException Failure(IntPtr err)
        {
            string message = "no detail";
            if (err != IntPtr.Zero)
            {
                message = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(err, 8)) ?? message;
                g_error_free(err);
            }
            return new SecretStoreException($"{Unavailable} ({message})");
        }


        private static IntPtr NewTable()
        {
            IntPtr lib = NativeLibrary.Load(GLib);
            return g_hash_table_new(NativeLibrary.GetExport(lib, "g_str_hash"), NativeLibrary.GetExport(lib, "g_str_equal"));
        }


        // Runs the action with the schema and the attribute table, always freeing the native memory.
        private static T With<T>(string key, Func<IntPtr, IntPtr, T> action)
        {
            IntPtr types = NewTable();
            IntPtr attrs = NewTable();
            IntPtr name1 = Marshal.StringToCoTaskMemUTF8(AttributeName);
            IntPtr name2 = Marshal.StringToCoTaskMemUTF8(AttributeName);
            IntPtr value = Marshal.StringToCoTaskMemUTF8(key);
            IntPtr schema = IntPtr.Zero;
            try
            {
                g_hash_table_insert(types, name1, IntPtr.Zero); // SECRET_SCHEMA_ATTRIBUTE_STRING = 0
                schema = secret_schema_newv(SchemaName, 0 /* SECRET_SCHEMA_NONE */, types);
                g_hash_table_insert(attrs, name2, value);
                return action(schema, attrs);
            }
            finally
            {
                g_hash_table_unref(attrs);
                g_hash_table_unref(types);
                if (schema != IntPtr.Zero)
                    secret_schema_unref(schema);
                Marshal.FreeCoTaskMem(name1);
                Marshal.FreeCoTaskMem(name2);
                Marshal.FreeCoTaskMem(value);
            }
        }
        #endregion
    }
}
