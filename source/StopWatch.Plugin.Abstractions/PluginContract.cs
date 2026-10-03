using System;

namespace StopWatch.Plugin
{
    /// <summary>
    /// The version of the plugin contract. Independent of the version of the
    /// StopWatch executable: it changes only when this assembly's public
    /// surface does.
    ///
    /// A minor bump is additive (new members, new optional capabilities); a
    /// major bump breaks existing plugins.
    /// </summary>
    public static class PluginContract
    {
        /// <summary>The contract version this assembly describes.</summary>
        public static readonly Version ContractVersion = new Version(1, 0);


        /// <summary>
        /// Whether a plugin built against <paramref name="pluginVersion"/> can
        /// run on a host implementing <paramref name="hostVersion"/>: same
        /// major, and a minor no newer than the host's.
        /// </summary>
        public static bool IsCompatible(Version hostVersion, Version pluginVersion)
        {
            if (hostVersion == null)
                throw new ArgumentNullException(nameof(hostVersion));
            if (pluginVersion == null)
                throw new ArgumentNullException(nameof(pluginVersion));

            return pluginVersion.Major == hostVersion.Major
                && pluginVersion.Minor <= hostVersion.Minor;
        }
    }
}
