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

namespace StopWatchTest
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Moq;
    using NUnit.Framework;
    using StopWatch.Update;


    [TestFixture]
    public class AutoUpdateServiceTest
    {
        private Mock<IReleaseSource> releaseSource;
        private AutoUpdateService service;
        private string stagingBaseDir;

        [SetUp]
        public void Setup()
        {
            releaseSource = new Mock<IReleaseSource>(MockBehavior.Strict);
            service = new AutoUpdateService(releaseSource.Object);
            stagingBaseDir = Path.Combine(Path.GetTempPath(), $"StopWatchTest-{Guid.NewGuid():N}");
        }


        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(stagingBaseDir))
                Directory.Delete(stagingBaseDir, recursive: true);
        }


        [TestCase(false)]
        [TestCase(true)]
        public async Task CheckAndStageAsync_ReturnsNullAndTouchesNoNetworkWhenDisabled(bool includePrereleases)
        {
            // MockBehavior.Strict means any unexpected call (e.g. to
            // GetReleasesAsync) throws - this is itself the assertion that a
            // disabled check makes no network call at all, beta or not.
            PendingUpdate result = await service.CheckAndStageAsync(
                checkForUpdatesEnabled: false,
                includePrereleases: includePrereleases,
                currentVersion: "3.0.0",
                isSelfContained: true,
                stagingBaseDir: stagingBaseDir);

            Assert.That(result, Is.Null);
        }


        [Test]
        public async Task CheckAndStageAsync_ReturnsNullWhenNoNewerReleaseExists()
        {
            SetupReleases(false, new ReleaseInfo { TagName = "v3.0.0", Assets = Array.Empty<ReleaseAsset>() });

            Assert.That(await Check("3.0.0", includePrereleases: false), Is.Null);
        }


        [Test]
        public async Task CheckAndStageAsync_ReturnsNullWhenTheMatchingAssetIsMissing()
        {
            SetupReleases(false, new ReleaseInfo { TagName = "v3.1.0", Assets = Array.Empty<ReleaseAsset>() });

            Assert.That(await Check("3.0.0", includePrereleases: false), Is.Null);
        }


        [Test]
        public async Task CheckAndStageAsync_ReturnsNullWhenTheCurrentVersionCannotBeParsed()
        {
            SetupReleases(false, StageableRelease("v3.1.0"));

            Assert.That(await Check("not-a-version", includePrereleases: false), Is.Null);
        }


        [Test]
        public async Task CheckAndStageAsync_CarriesTheReleaseUrlIntoTheStagedUpdate()
        {
            SetupReleases(false, StageableRelease("v3.1.0"));

            PendingUpdate result = await Check("3.0.0", includePrereleases: false);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ReleaseUrl, Is.EqualTo("https://github.com/menegrete/jirastopwatch/releases/tag/v3.1.0"));
            Assert.That(result.Version, Is.EqualTo("3.1.0"));
            Assert.That(result.IsPrerelease, Is.False);
        }


        [Test]
        public async Task CheckAndStageAsync_StableOnlyIgnoresPrereleasesEvenIfTheSourceReturnsThem()
        {
            SetupReleases(false, StageableRelease("v3.8.0-rc.1", isPrerelease: true));

            Assert.That(await Check("3.7.0", includePrereleases: false), Is.Null);
        }


        [Test]
        public async Task CheckAndStageAsync_OffersAHigherPrereleaseWhenSubscribed()
        {
            SetupReleases(true, StageableRelease("v3.7.0"), StageableRelease("v3.8.0-rc.1", isPrerelease: true));

            PendingUpdate result = await Check("3.7.0", includePrereleases: true);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Version, Is.EqualTo("3.8.0-rc.1"));
            Assert.That(result.IsPrerelease, Is.True);
            Assert.That(result.StagedPath, Does.Contain("3.8.0-rc.1"));
            Assert.That(result.ReleaseUrl, Does.EndWith("/v3.8.0-rc.1"));
        }


        [Test]
        public async Task CheckAndStageAsync_ComparesPrereleaseNumbersAsNumbers()
        {
            SetupReleases(true,
                StageableRelease("v3.8.0-rc.2", isPrerelease: true),
                StageableRelease("v3.8.0-rc.10", isPrerelease: true),
                StageableRelease("v3.8.0-rc.9", isPrerelease: true));

            PendingUpdate result = await Check("3.7.0", includePrereleases: true);

            Assert.That(result.Version, Is.EqualTo("3.8.0-rc.10"));
        }


        [Test]
        public async Task CheckAndStageAsync_PrefersTheStableReleaseOverItsPrereleases()
        {
            SetupReleases(true,
                StageableRelease("v3.8.0-rc.3", isPrerelease: true),
                StageableRelease("v3.8.0"),
                StageableRelease("v3.8.0-rc.2", isPrerelease: true));

            PendingUpdate result = await Check("3.7.0", includePrereleases: true);

            Assert.That(result.Version, Is.EqualTo("3.8.0"));
            Assert.That(result.IsPrerelease, Is.False);
        }


        [Test]
        public async Task CheckAndStageAsync_DoesNotDowngradeFromABetaToTheLatestStable()
        {
            SetupReleases(false, StageableRelease("v3.7.0"));

            Assert.That(await Check("3.8.0-rc.1", includePrereleases: false), Is.Null);
        }


        [Test]
        public async Task CheckAndStageAsync_OffersTheNextStableToAUserWhoLeftTheBetaChannel()
        {
            SetupReleases(false, StageableRelease("v3.8.0"));

            PendingUpdate result = await Check("3.8.0-rc.1", includePrereleases: false);

            Assert.That(result.Version, Is.EqualTo("3.8.0"));
        }


        [Test]
        public async Task CheckAndStageAsync_OffersNothingWhenTheBetaInstalledIsAlreadyTheNewest()
        {
            SetupReleases(true, StageableRelease("v3.7.0"), StageableRelease("v3.8.0-rc.1", isPrerelease: true));

            Assert.That(await Check("3.8.0-rc.1", includePrereleases: true), Is.Null);
        }


        [Test]
        public async Task CheckAndStageAsync_IgnoresBuildMetadataOnTheCurrentVersion()
        {
            SetupReleases(true, StageableRelease("v3.8.0-rc.1", isPrerelease: true));

            Assert.That(await Check("3.8.0-rc.1+abc123", includePrereleases: true), Is.Null);
        }


        [Test]
        public async Task CheckAndStageAsync_LeavesNothingPendingWhenABetaReleaseHasNoAssets()
        {
            SetupReleases(true, new ReleaseInfo
            {
                TagName = "v3.8.0-rc.1",
                IsPrerelease = true,
                Assets = Array.Empty<ReleaseAsset>(),
            });

            Assert.That(await Check("3.7.0", includePrereleases: true), Is.Null);
            Assert.That(Directory.Exists(stagingBaseDir), Is.False);
        }


        [Test]
        public async Task CheckAndStageAsync_DiscardsABetaWhoseChecksumDoesNotMatch()
        {
            const string assetName = "JiraStopWatch-v3.8.0-rc.1-self-contained.exe";
            const string checksumName = assetName + ".sha256";

            SetupReleases(true, new ReleaseInfo
            {
                TagName = "v3.8.0-rc.1",
                IsPrerelease = true,
                Assets = new[]
                {
                    new ReleaseAsset { Name = assetName, DownloadUrl = "https://example.com/" + assetName },
                    new ReleaseAsset { Name = checksumName, DownloadUrl = "https://example.com/" + checksumName },
                },
            });
            releaseSource
                .Setup(r => r.DownloadAssetAsync("https://example.com/" + checksumName))
                .ReturnsAsync(Encoding.UTF8.GetBytes($"{new string('0', 64)}  {assetName}"));
            releaseSource
                .Setup(r => r.DownloadAssetAsync("https://example.com/" + assetName))
                .ReturnsAsync(Encoding.UTF8.GetBytes("tampered"));

            Assert.That(await Check("3.7.0", includePrereleases: true), Is.Null);
        }


        private Task<PendingUpdate> Check(string currentVersion, bool includePrereleases)
        {
            return service.CheckAndStageAsync(
                checkForUpdatesEnabled: true,
                includePrereleases: includePrereleases,
                currentVersion: currentVersion,
                isSelfContained: true,
                stagingBaseDir: stagingBaseDir);
        }


        /// <summary>
        /// The source is expected to be asked with exactly this
        /// <paramref name="includePrereleases"/> flag - Strict mocking makes
        /// a call with the other value throw.
        /// </summary>
        private void SetupReleases(bool includePrereleases, params ReleaseInfo[] releases)
        {
            releaseSource
                .Setup(r => r.GetReleasesAsync(includePrereleases))
                .ReturnsAsync((IReadOnlyList<ReleaseInfo>)releases);
        }


        /// <summary>
        /// A release with a valid self-contained asset and checksum, and the
        /// download setups to match. Asset names carry the full version,
        /// suffix included, as scripts/publish-release-artifacts.js names them.
        /// </summary>
        private ReleaseInfo StageableRelease(string tag, bool isPrerelease = false)
        {
            string version = tag.TrimStart('v');
            string assetName = $"JiraStopWatch-v{version}-self-contained.exe";
            string checksumName = assetName + ".sha256";
            byte[] assetBytes = Encoding.UTF8.GetBytes("fake exe contents " + version);
            string digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(assetBytes));

            releaseSource
                .Setup(r => r.DownloadAssetAsync("https://example.com/" + checksumName))
                .ReturnsAsync(Encoding.UTF8.GetBytes($"{digest}  {assetName}"));
            releaseSource
                .Setup(r => r.DownloadAssetAsync("https://example.com/" + assetName))
                .ReturnsAsync(assetBytes);

            return new ReleaseInfo
            {
                TagName = tag,
                IsPrerelease = isPrerelease,
                HtmlUrl = "https://github.com/menegrete/jirastopwatch/releases/tag/" + tag,
                Assets = new[]
                {
                    new ReleaseAsset { Name = assetName, DownloadUrl = "https://example.com/" + assetName },
                    new ReleaseAsset { Name = checksumName, DownloadUrl = "https://example.com/" + checksumName },
                },
            };
        }
    }
}
