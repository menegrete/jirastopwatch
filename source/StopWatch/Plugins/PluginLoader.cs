using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using StopWatch.Plugin;

namespace StopWatch.Plugins
{
    /// <summary>
    /// Discovers and loads the plugins under one or more root folders, one
    /// <c>&lt;Id&gt;/</c> subfolder each.
    ///
    /// Every plugin is loaded, initialized and asked for its commands inside
    /// its own try/catch, so a plugin that fails is reported as not loaded
    /// (and contributes nothing) without stopping the others.
    /// </summary>
    internal class PluginLoader
    {
        public const string ManifestFileName = "plugin.json";

        private readonly IReadOnlyList<string> roots;
        private readonly Version hostContractVersion;
        private readonly Func<string, IPluginHost> hostFactory;
        private readonly Action<string, Exception> log;

        /// <param name="roots">The folders holding one subfolder per plugin, in priority order: when the same id is in more than one, the first wins.</param>
        /// <param name="hostContractVersion">The contract version the host implements.</param>
        /// <param name="hostFactory">Builds the <see cref="IPluginHost"/> for a plugin id.</param>
        /// <param name="log">Where failures are reported.</param>
        public PluginLoader(IReadOnlyList<string> roots, Version hostContractVersion, Func<string, IPluginHost> hostFactory, Action<string, Exception> log)
        {
            this.roots = roots ?? new string[0];
            this.hostContractVersion = hostContractVersion;
            this.hostFactory = hostFactory;
            this.log = log ?? ((m, e) => { });
        }


        /// <summary>
        /// Loads every plugin found, in order of id (ordinal, ignoring case).
        /// Roots that do not exist are skipped. An id found in a second root
        /// is reported as not loaded (duplicate) rather than loaded twice.
        /// </summary>
        public IReadOnlyList<PluginInfo> LoadAll()
        {
            // (id, folder) in root priority order, so the first of a duplicate is the winner.
            var found = new List<KeyValuePair<string, string>>();

            foreach (string root in roots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                    continue;

                try
                {
                    foreach (string folder in Directory.GetDirectories(root))
                        found.Add(new KeyValuePair<string, string>(Path.GetFileName(folder), folder));
                }
                catch (Exception ex)
                {
                    log($"Plugins: could not list '{root}'", ex);
                }
            }

            var result = new List<PluginInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // OrderBy is stable: equal ids keep their root priority order.
            foreach (var entry in found.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (seen.Add(entry.Key))
                {
                    result.Add(LoadOne(entry.Value));
                    continue;
                }

                var duplicate = new PluginInfo(entry.Key);
                Fail(duplicate, $"Duplicate plugin id: another folder with the same id was loaded instead ('{entry.Value}' ignored).", null);
                result.Add(duplicate);
            }

            return result;
        }


        private PluginInfo LoadOne(string folder)
        {
            var info = new PluginInfo(Path.GetFileName(folder));

            try
            {
                Load(folder, info);
            }
            catch (PluginLoadException ex)
            {
                Fail(info, ex.Message, null);
            }
            catch (Exception ex)
            {
                Fail(info, Describe(ex), ex);
            }

            return info;
        }


        private void Load(string folder, PluginInfo info)
        {
            string manifestPath = Path.Combine(folder, ManifestFileName);
            if (!File.Exists(manifestPath))
                throw new PluginLoadException(ManifestFileName + " not found.");

            PluginManifest manifest = PluginManifest.Parse(File.ReadAllText(manifestPath));
            info.Name = manifest.Name;
            info.Version = manifest.Version;

            if (!string.Equals(manifest.Id, info.Id, StringComparison.OrdinalIgnoreCase))
                throw new PluginLoadException($"The manifest id \"{manifest.Id}\" does not match the folder name \"{info.Id}\".");

            if (!PluginContract.IsCompatible(hostContractVersion, manifest.ContractVersion))
                throw new PluginLoadException($"Incompatible plugin contract: the plugin targets {manifest.ContractVersion}, this application supports {hostContractVersion}.");

            string assemblyPath = Path.Combine(folder, manifest.Id + ".dll");
            if (!File.Exists(assemblyPath))
                throw new PluginLoadException($"{manifest.Id}.dll not found.");

            PluginDependencyResolver resolver = PluginDependencyResolver.Register(assemblyPath);
            Assembly assembly = resolver.LoadPlugin(assemblyPath);

            IPlugin plugin = CreatePlugin(assembly);
            plugin.Initialize(hostFactory(info.Id));

            IEnumerable<PluginCommand> commands = plugin.GetCommands() ?? Enumerable.Empty<PluginCommand>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PluginCommand command in commands)
            {
                if (command == null)
                    continue;

                if (!seen.Add(command.Id))
                {
                    log($"Plugin '{info.Id}': duplicate command id '{command.Id}' ignored", null);
                    continue;
                }

                info.Commands.Add(new PluginCommandItem(info, command, log));
            }

            info.State = PluginState.Loaded;
            info.Reason = "";
        }


        private static IPlugin CreatePlugin(Assembly assembly)
        {
            List<Type> candidates = assembly.GetTypes()
                .Where(t => t.IsPublic && !t.IsAbstract && typeof(IPlugin).IsAssignableFrom(t))
                .ToList();

            if (candidates.Count == 0)
                throw new PluginLoadException("The assembly has no public class implementing IPlugin.");

            if (candidates.Count > 1)
                throw new PluginLoadException("The assembly has more than one public class implementing IPlugin.");

            return (IPlugin)Activator.CreateInstance(candidates[0]);
        }


        private void Fail(PluginInfo info, string reason, Exception ex)
        {
            info.State = PluginState.Failed;
            info.Reason = reason;
            info.Commands.Clear();

            log($"Plugin '{info.Id}' not loaded: {reason}", ex);
        }


        private static string Describe(Exception ex)
        {
            if (ex is TargetInvocationException tie && tie.InnerException != null)
                ex = tie.InnerException;

            if (ex is ReflectionTypeLoadException rtle && rtle.LoaderExceptions.Length > 0)
                return rtle.LoaderExceptions[0].Message;

            return $"{ex.GetType().Name}: {ex.Message}";
        }
    }
}
