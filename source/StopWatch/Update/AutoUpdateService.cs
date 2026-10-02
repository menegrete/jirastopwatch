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
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using StopWatch.Logging;

namespace StopWatch.Update
{
    /// <summary>
    /// Checks GitHub for a newer release, and if one exists and matches the
    /// running variant, downloads it, verifies it against its published
    /// SHA256 checksum, and stages it under <c>stagingBaseDir</c> ready to
    /// apply. Never touches the running install directory - see the
    /// auto-update design, "Download staging".
    ///
    /// Every failure (network, missing asset, bad checksum) is caught and
    /// logged rather than thrown: a failed check must never stop the app
    /// from starting normally, per the auto-update spec, "El chequeo falla".
    /// </summary>
    internal sealed class AutoUpdateService
    {
        public AutoUpdateService(IReleaseSource releaseSource)
        {
            this.releaseSource = releaseSource ?? throw new ArgumentNullException(nameof(releaseSource));
        }


        /// <summary>
        /// Returns the staged update, or null if there is none - checking is
        /// turned off, there's no newer release, the matching asset/checksum
        /// wasn't published, the download failed, or the checksum didn't
        /// match. Takes the enabled flag itself (rather than leaving the
        /// caller to gate the call) so the "off means no network activity at
        /// all" behaviour is covered by a unit test against this service
        /// rather than needing to stand up the WPF window that calls it.
        ///
        /// Of the releases the source offers, the one with the highest
        /// semantic version wins (pre-releases count only with
        /// <paramref name="includePrereleases"/>), and it's offered only if
        /// strictly newer than <paramref name="currentVersion"/> - so there
        /// is no downgrade and no special case for leaving the beta channel.
        /// </summary>
        public async Task<PendingUpdate> CheckAndStageAsync(bool checkForUpdatesEnabled, bool includePrereleases, string currentVersion, bool isSelfContained, string stagingBaseDir)
        {
            if (!checkForUpdatesEnabled)
                return null;

            try
            {
                IReadOnlyList<ReleaseInfo> releases = await releaseSource.GetReleasesAsync(includePrereleases);

                if (!UpdateVersion.TryParse(currentVersion, out SemanticVersion current))
                    return null;

                (ReleaseInfo release, SemanticVersion version) = PickHighest(releases, includePrereleases);
                if (release == null || !UpdateVersion.IsNewer(version, current))
                    return null;

                // The canonical text keeps a prerelease suffix ("3.8.0-rc.1"),
                // which the asset names and the staging folder both carry.
                return await DownloadAndStageAsync(version.ToString(), version.IsPrerelease || release.IsPrerelease, isSelfContained, stagingBaseDir, release);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Auto-update check failed", ex);
                return null;
            }
        }


        private static (ReleaseInfo release, SemanticVersion version) PickHighest(IReadOnlyList<ReleaseInfo> releases, bool includePrereleases)
        {
            ReleaseInfo best = null;
            SemanticVersion bestVersion = null;

            foreach (ReleaseInfo release in releases ?? Array.Empty<ReleaseInfo>())
            {
                if (release == null || !UpdateVersion.TryParse(release.TagName, out SemanticVersion version))
                    continue;

                // A stable-only check never offers a pre-release, whatever
                // the source returned (and a tag with a suffix is one too).
                if (!includePrereleases && (release.IsPrerelease || version.IsPrerelease))
                    continue;

                if (bestVersion == null || version.CompareTo(bestVersion) > 0)
                {
                    best = release;
                    bestVersion = version;
                }
            }

            return (best, bestVersion);
        }


        private async Task<PendingUpdate> DownloadAndStageAsync(string versionText, bool isPrerelease, bool isSelfContained, string stagingBaseDir, ReleaseInfo release)
        {
            string assetName = ReleaseAssetSelector.AssetFileName(versionText, isSelfContained);
            string checksumName = ReleaseAssetSelector.ChecksumFileName(versionText, isSelfContained);

            ReleaseAsset asset = ReleaseAssetSelector.FindAsset(release, assetName);
            ReleaseAsset checksumAsset = ReleaseAssetSelector.FindAsset(release, checksumName);
            if (asset == null || checksumAsset == null)
            {
                Logger.Instance.Log($"Auto-update: release {versionText} has no {assetName} asset, skipping");
                return null;
            }

            byte[] checksumBytes = await releaseSource.DownloadAssetAsync(checksumAsset.DownloadUrl);
            byte[] assetBytes = await releaseSource.DownloadAssetAsync(asset.DownloadUrl);

            if (!ChecksumVerifier.Verify(assetBytes, Encoding.UTF8.GetString(checksumBytes)))
            {
                Logger.Instance.Log($"Auto-update: checksum mismatch for {assetName}, discarding download");
                return null;
            }

            string versionDir = Path.Combine(stagingBaseDir, versionText);
            try
            {
                // A clean slate every attempt - a prior crashed/killed attempt
                // at the same version may have left a partial staging dir.
                if (Directory.Exists(versionDir))
                    Directory.Delete(versionDir, recursive: true);
                Directory.CreateDirectory(versionDir);

                if (isSelfContained)
                {
                    string stagedExePath = Path.Combine(versionDir, "StopWatch.exe");
                    File.WriteAllBytes(stagedExePath, assetBytes);
                    return new PendingUpdate(versionText, stagedExePath, isSelfContained: true, release.HtmlUrl, isPrerelease: isPrerelease);
                }

                string zipPath = Path.Combine(versionDir, assetName);
                File.WriteAllBytes(zipPath, assetBytes);

                string extractedDir = Path.Combine(versionDir, "extracted");
                ZipFile.ExtractToDirectory(zipPath, extractedDir);

                return new PendingUpdate(versionText, extractedDir, isSelfContained: false, release.HtmlUrl, isPrerelease: isPrerelease);
            }
            catch
            {
                // Staging failed partway through (disk full, extraction
                // error, ...) - leave no half-written staged build behind.
                try { Directory.Delete(versionDir, recursive: true); } catch { /* best-effort cleanup */ }
                throw;
            }
        }


        private readonly IReleaseSource releaseSource;
    }
}
