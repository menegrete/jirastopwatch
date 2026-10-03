using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace StopWatch.Plugins
{
    /// <summary>
    /// Finds a plugin's own dependencies in its folder: managed assemblies
    /// (when the default load context fails to find them) and native
    /// libraries (for the plugin assembly and every assembly it brings).
    ///
    /// Plugins are loose files on disk, so none of this is affected by the
    /// host being published as a single file.
    /// </summary>
    internal sealed class PluginDependencyResolver
    {
        private static readonly object sync = new object();
        private static readonly List<PluginDependencyResolver> registered = new List<PluginDependencyResolver>();
        private static bool hooked;

        private readonly string folder;
        private readonly AssemblyDependencyResolver managed;

        private PluginDependencyResolver(string pluginAssemblyPath)
        {
            folder = Path.GetDirectoryName(Path.GetFullPath(pluginAssemblyPath));
            managed = new AssemblyDependencyResolver(pluginAssemblyPath);
        }


        /// <summary>
        /// Starts resolving dependencies for the plugin assembly at
        /// <paramref name="pluginAssemblyPath"/>. Must be called before the
        /// assembly is loaded.
        /// </summary>
        public static PluginDependencyResolver Register(string pluginAssemblyPath)
        {
            var resolver = new PluginDependencyResolver(pluginAssemblyPath);

            lock (sync)
            {
                registered.Add(resolver);

                if (!hooked)
                {
                    AssemblyLoadContext.Default.Resolving += DefaultContext_Resolving;
                    hooked = true;
                }
            }

            return resolver;
        }


        /// <summary>Loads the plugin assembly and arranges for its native lookups to use this resolver.</summary>
        public Assembly LoadPlugin(string pluginAssemblyPath)
        {
            Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(pluginAssemblyPath));
            RegisterNativeResolver(assembly);
            return assembly;
        }


        private static Assembly DefaultContext_Resolving(AssemblyLoadContext context, AssemblyName name)
        {
            PluginDependencyResolver[] snapshot;
            lock (sync)
                snapshot = registered.ToArray();

            foreach (PluginDependencyResolver resolver in snapshot)
            {
                Assembly assembly = resolver.TryLoadManaged(context, name);
                if (assembly != null)
                    return assembly;
            }

            return null;
        }


        private Assembly TryLoadManaged(AssemblyLoadContext context, AssemblyName name)
        {
            string path = managed.ResolveAssemblyToPath(name);

            if (path == null && name.Name != null)
            {
                string candidate = Path.Combine(folder, name.Name + ".dll");
                if (File.Exists(candidate))
                    path = candidate;
            }

            if (path == null)
                return null;

            Assembly assembly = context.LoadFromAssemblyPath(path);
            RegisterNativeResolver(assembly);
            return assembly;
        }


        private void RegisterNativeResolver(Assembly assembly)
        {
            try
            {
                NativeLibrary.SetDllImportResolver(assembly, ResolveNative);
            }
            catch (InvalidOperationException)
            {
                // A resolver is already set for this assembly: leave it.
            }
        }


        private IntPtr ResolveNative(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            foreach (string candidate in NativeCandidates(libraryName))
            {
                if (candidate != null && File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out IntPtr handle))
                    return handle;
            }

            // Not ours: let the default probing carry on.
            return IntPtr.Zero;
        }


        private IEnumerable<string> NativeCandidates(string libraryName)
        {
            yield return managed.ResolveUnmanagedDllToPath(libraryName);

            string fileName = libraryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? libraryName : libraryName + ".dll";
            string arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

            yield return Path.Combine(folder, fileName);
            yield return Path.Combine(folder, "runtimes", "win-" + arch, "native", fileName);
        }
    }
}
