using System.Runtime.InteropServices;

namespace AvaloniaSpike;

/// <summary>Direct P/Invoke to libsecret using the non-variadic "v" functions. Linux only.</summary>
internal static class LibSecret
{
    private const string Secret = "libsecret-1.so.0";
    private const string GLib = "libglib-2.0.so.0";
    private const string SchemaName = "com.jirastopwatch.spike";
    private const string AttrName = "account";
    private const string AttrValue = "spike";

    [DllImport(Secret)] private static extern IntPtr secret_schema_newv(string name, int flags, IntPtr attrs);
    [DllImport(Secret)] private static extern void secret_schema_unref(IntPtr schema);
    [DllImport(Secret)] private static extern int secret_password_storev_sync(IntPtr schema, IntPtr attrs, string? collection, string label, string password, IntPtr cancellable, out IntPtr error);
    [DllImport(Secret)] private static extern IntPtr secret_password_lookupv_sync(IntPtr schema, IntPtr attrs, IntPtr cancellable, out IntPtr error);
    [DllImport(Secret)] private static extern int secret_password_clearv_sync(IntPtr schema, IntPtr attrs, IntPtr cancellable, out IntPtr error);
    [DllImport(Secret)] private static extern void secret_password_free(IntPtr password);

    [DllImport(GLib)] private static extern IntPtr g_hash_table_new(IntPtr hash, IntPtr equal);
    [DllImport(GLib)] private static extern void g_hash_table_insert(IntPtr table, IntPtr key, IntPtr value);
    [DllImport(GLib)] private static extern void g_hash_table_unref(IntPtr table);
    [DllImport(GLib)] private static extern void g_error_free(IntPtr error);

    public static bool IsSupported => OperatingSystem.IsLinux();

    // GError { guint32 domain; gint code; gchar* message; } -> message at offset 8 on 64-bit
    private static string ReadError(IntPtr err)
    {
        if (err == IntPtr.Zero) return "(no GError)";
        var msg = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(err, 8));
        var code = Marshal.ReadInt32(err, 4);
        g_error_free(err);
        return $"{msg} (code {code})";
    }

    private static IntPtr NewTable(out IntPtr hashFn, out IntPtr eqFn)
    {
        var lib = NativeLibrary.Load(GLib);
        hashFn = NativeLibrary.GetExport(lib, "g_str_hash");
        eqFn = NativeLibrary.GetExport(lib, "g_str_equal");
        return g_hash_table_new(hashFn, eqFn);
    }

    private static IntPtr NewSchema()
    {
        var types = NewTable(out _, out _);
        var k = Marshal.StringToCoTaskMemUTF8(AttrName);
        try
        {
            g_hash_table_insert(types, k, IntPtr.Zero); // SECRET_SCHEMA_ATTRIBUTE_STRING = 0
            return secret_schema_newv(SchemaName, 0 /* SECRET_SCHEMA_NONE */, types);
        }
        finally
        {
            g_hash_table_unref(types);
            Marshal.FreeCoTaskMem(k);
        }
    }

    // Runs action with schema + attributes table; always frees native resources.
    private static T With<T>(Func<IntPtr, IntPtr, T> action)
    {
        var schema = NewSchema();
        var attrs = NewTable(out _, out _);
        var k = Marshal.StringToCoTaskMemUTF8(AttrName);
        var v = Marshal.StringToCoTaskMemUTF8(AttrValue);
        try
        {
            g_hash_table_insert(attrs, k, v);
            return action(schema, attrs);
        }
        finally
        {
            g_hash_table_unref(attrs);
            Marshal.FreeCoTaskMem(k);
            Marshal.FreeCoTaskMem(v);
            if (schema != IntPtr.Zero) secret_schema_unref(schema);
        }
    }

    public static (bool Ok, string Error) Store(string secret) => With((schema, attrs) =>
    {
        var ok = secret_password_storev_sync(schema, attrs, null, "Avalonia spike secret", secret, IntPtr.Zero, out var err) != 0;
        return ok ? (true, "") : (false, ReadError(err));
    });

    public static (bool Ok, string? Value, string Error) Lookup() => With((schema, attrs) =>
    {
        var p = secret_password_lookupv_sync(schema, attrs, IntPtr.Zero, out var err);
        if (err != IntPtr.Zero) return (false, (string?)null, ReadError(err));
        if (p == IntPtr.Zero) return (true, (string?)null, "");
        var s = Marshal.PtrToStringUTF8(p);
        secret_password_free(p);
        return (true, s, "");
    });

    public static (bool Ok, bool Removed, string Error) Clear() => With((schema, attrs) =>
    {
        var removed = secret_password_clearv_sync(schema, attrs, IntPtr.Zero, out var err) != 0;
        return err != IntPtr.Zero ? (false, false, ReadError(err)) : (true, removed, "");
    });
}
