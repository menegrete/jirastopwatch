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
using System.Security.Cryptography;

namespace StopWatch.Update
{
    /// <summary>
    /// Verifies a downloaded asset against the "&lt;hex digest&gt;  &lt;filename&gt;"
    /// .sha256 file scripts/publish-release-artifacts.js publishes next to it
    /// (the same format sha256sum produces).
    /// </summary>
    internal static class ChecksumVerifier
    {
        public static bool Verify(byte[] content, string checksumFileContent)
        {
            string expected = ParseDigest(checksumFileContent);
            if (expected == null)
                return false;

            string actual = Convert.ToHexString(SHA256.HashData(content));
            return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }


        /// <summary>Internal rather than private so it's directly testable.</summary>
        internal static string ParseDigest(string checksumFileContent)
        {
            if (string.IsNullOrWhiteSpace(checksumFileContent))
                return null;

            string firstToken = checksumFileContent
                .Trim()
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[0];

            return firstToken.Length == 64 ? firstToken : null;
        }
    }
}
