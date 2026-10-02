/**
 * Copyright 2023 Y. Meyer-Norwood
 * Copyright 2020 Dan Tulloh
 * Copyright 2016 Carsten Gehling
 *
 * For a full list of contributing authors, see:
 *
 *     https://jirastopwatch.com/contributors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at:
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace StopWatch.Update
{
    /// <summary>
    /// A semantic version, <c>MAJOR.MINOR.PATCH[-prerelease][+build]</c>, with
    /// the ordering semver defines: a version with a prerelease suffix is
    /// lower than the same version without one, and prerelease identifiers
    /// are compared one by one (numeric ones as numbers, and lower than
    /// alphanumeric ones). Build metadata is ignored. Needed because
    /// <see cref="Version"/> can't parse a suffix like "3.8.0-rc.1".
    /// </summary>
    internal sealed class SemanticVersion : IComparable<SemanticVersion>
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }

        /// <summary>The dot-separated prerelease identifiers; empty for a stable version.</summary>
        public IReadOnlyList<string> Prerelease { get; }

        public bool IsPrerelease => Prerelease.Count > 0;


        private SemanticVersion(int major, int minor, int patch, IReadOnlyList<string> prerelease)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Prerelease = prerelease;
        }


        /// <summary>
        /// Parses a version with an optional leading "v" (as in a git tag)
        /// and surrounding whitespace. Returns false for anything that isn't
        /// <c>MAJOR.MINOR.PATCH</c>, optionally followed by
        /// <c>-identifier[.identifier...]</c> and/or <c>+build</c>.
        /// </summary>
        public static bool TryParse(string text, out SemanticVersion version)
        {
            version = null;

            string trimmed = text?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return false;

            if (trimmed[0] == 'v' || trimmed[0] == 'V')
                trimmed = trimmed.Substring(1);

            int plus = trimmed.IndexOf('+');
            if (plus >= 0)
                trimmed = trimmed.Substring(0, plus);

            string core = trimmed;
            string[] prerelease = Array.Empty<string>();

            int dash = trimmed.IndexOf('-');
            if (dash >= 0)
            {
                core = trimmed.Substring(0, dash);
                prerelease = trimmed.Substring(dash + 1).Split('.');
                if (prerelease.Any(identifier => !IsValidIdentifier(identifier)))
                    return false;
            }

            string[] parts = core.Split('.');
            if (parts.Length != 3 ||
                !TryParseNumber(parts[0], out int major) ||
                !TryParseNumber(parts[1], out int minor) ||
                !TryParseNumber(parts[2], out int patch))
            {
                return false;
            }

            version = new SemanticVersion(major, minor, patch, prerelease);
            return true;
        }


        public int CompareTo(SemanticVersion other)
        {
            if (other is null)
                return 1;

            int result = Major.CompareTo(other.Major);
            if (result != 0) return result;
            result = Minor.CompareTo(other.Minor);
            if (result != 0) return result;
            result = Patch.CompareTo(other.Patch);
            if (result != 0) return result;

            // Same X.Y.Z: the one without a prerelease suffix is higher.
            if (!IsPrerelease && !other.IsPrerelease) return 0;
            if (!IsPrerelease) return 1;
            if (!other.IsPrerelease) return -1;

            int shared = Math.Min(Prerelease.Count, other.Prerelease.Count);
            for (int i = 0; i < shared; i++)
            {
                result = CompareIdentifiers(Prerelease[i], other.Prerelease[i]);
                if (result != 0) return result;
            }

            // All shared identifiers equal: the longer list is higher.
            return Prerelease.Count.CompareTo(other.Prerelease.Count);
        }


        /// <summary>The canonical form: no leading "v" and no build metadata, e.g. "3.8.0-rc.1".</summary>
        public override string ToString()
        {
            string core = $"{Major}.{Minor}.{Patch}";
            return IsPrerelease ? $"{core}-{string.Join(".", Prerelease)}" : core;
        }


        private static int CompareIdentifiers(string a, string b)
        {
            bool aNumeric = IsNumeric(a);
            bool bNumeric = IsNumeric(b);

            if (aNumeric && bNumeric)
                return CompareNumeric(a, b);

            // Numeric identifiers are lower than alphanumeric ones.
            if (aNumeric) return -1;
            if (bNumeric) return 1;

            return string.CompareOrdinal(a, b);
        }


        /// <summary>
        /// Compares digit strings as numbers without parsing them, so an
        /// identifier longer than an int can't overflow.
        /// </summary>
        private static int CompareNumeric(string a, string b)
        {
            a = a.TrimStart('0');
            b = b.TrimStart('0');
            if (a.Length != b.Length)
                return a.Length.CompareTo(b.Length);
            return string.CompareOrdinal(a, b);
        }


        private static bool IsNumeric(string identifier) => identifier.All(c => c >= '0' && c <= '9');


        private static bool IsValidIdentifier(string identifier)
        {
            return identifier.Length > 0 &&
                   identifier.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '-');
        }


        private static bool TryParseNumber(string text, out int number)
        {
            number = 0;
            return text.Length > 0 &&
                   IsNumeric(text) &&
                   int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);
        }
    }
}
