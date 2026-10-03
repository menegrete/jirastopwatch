using System;
using System.Text.Json;

namespace StopWatch.Plugins
{
    /// <summary>
    /// The contents of a plugin's plugin.json: who it is and which version of
    /// the plugin contract it was built against.
    /// </summary>
    internal class PluginManifest
    {
        public string Id { get; private set; }

        public string Name { get; private set; }

        public string Version { get; private set; }

        public Version ContractVersion { get; private set; }


        /// <summary>
        /// Parses and validates a manifest. Throws <see cref="PluginLoadException"/>
        /// with a message fit to show the user when it is malformed or
        /// incomplete.
        /// </summary>
        public static PluginManifest Parse(string json)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            }
            catch (JsonException ex)
            {
                throw new PluginLoadException("plugin.json is not valid JSON: " + ex.Message);
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    throw new PluginLoadException("plugin.json must contain a JSON object.");

                string id = RequiredString(document.RootElement, "id");
                string name = RequiredString(document.RootElement, "name");
                string version = RequiredString(document.RootElement, "version");
                string contract = RequiredString(document.RootElement, "contractVersion");

                return new PluginManifest
                {
                    Id = id,
                    Name = name,
                    Version = version,
                    ContractVersion = ParseContractVersion(contract),
                };
            }
        }


        private static string RequiredString(JsonElement root, string property)
        {
            foreach (JsonProperty p in root.EnumerateObject())
            {
                if (!string.Equals(p.Name, property, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (p.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.Value.GetString()))
                    return p.Value.GetString().Trim();

                break;
            }

            throw new PluginLoadException($"plugin.json is missing the required \"{property}\" text field.");
        }


        private static Version ParseContractVersion(string text)
        {
            // "1" is shorthand for "1.0"; System.Version insists on two parts.
            string normalized = text.IndexOf('.') < 0 ? text + ".0" : text;

            if (!System.Version.TryParse(normalized, out Version version))
                throw new PluginLoadException($"plugin.json has an invalid contractVersion \"{text}\" (expected e.g. \"1.0\").");

            return version;
        }
    }


    /// <summary>A plugin could not be loaded; the message is shown to the user.</summary>
    internal class PluginLoadException : Exception
    {
        public PluginLoadException(string message) : base(message)
        {
        }
    }
}
