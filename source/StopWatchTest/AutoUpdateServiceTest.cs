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
    using System.Formats.Tar;
    using System.IO;
    using System.IO.Compression;
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
                platform: UpdatePlatform.Windows,
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


        [TestCase(true, "v4.0.0", false)]
        [TestCase(false, "v4.0.0", false)]
        [TestCase(true, "v4.0.0-rc.1", true)]
        [TestCase(false, "v4.0.0-rc.1", true)]
        public async Task CheckAndStageAsync_OnLinuxStagesTheTarballWithItsExecutableMode(bool isSelfContained, string tag, bool isPrerelease)
        {
            string variant = isSelfContained ? "self-contained" : "framework-dependent";
            string assetName = $"JiraStopWatch-{tag}-linux-x64-{variant}.tar.gz";
            byte[] exeBytes = Encoding.UTF8.GetBytes("fake linux exe " + tag);
            // The Windows assets are not even in the release: only the Linux ones may be asked for.
            ReleaseInfo release = ReleaseWith(tag, isPrerelease, assetName, MakeTarGz(exeBytes, includeSibling: !isSelfContained));
            SetupReleases(true, release);

            PendingUpdate result = await Check("3.9.0", includePrereleases: true, isSelfContained, UpdatePlatform.Linux);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Version, Is.EqualTo(tag.TrimStart('v')));
            Assert.That(result.IsPrerelease, Is.EqualTo(isPrerelease));
            Assert.That(result.IsSelfContained, Is.EqualTo(isSelfContained));
            Assert.That(File.ReadAllBytes(Path.Combine(result.StagedPath, "StopWatch")), Is.EqualTo(exeBytes));
            if (!isSelfContained)
                Assert.That(File.Exists(Path.Combine(result.StagedPath, "libSkiaSharp.so")), Is.True);
            if (!OperatingSystem.IsWindows())
                Assert.That(File.GetUnixFileMode(Path.Combine(result.StagedPath, "StopWatch")),
                    Is.EqualTo((UnixFileMode)Convert.ToInt32("755", 8)));
        }


        [Test]
        public async Task CheckAndStageAsync_OnLinuxDiscardsATarballWhoseChecksumDoesNotMatch()
        {
            const string assetName = "JiraStopWatch-v4.0.0-linux-x64-self-contained.tar.gz";
            ReleaseInfo release = ReleaseWith("v4.0.0", false, assetName, MakeTarGz(Encoding.UTF8.GetBytes("x"), false));
            releaseSource
                .Setup(r => r.DownloadAssetAsync("https://example.com/" + assetName + ".sha256"))
                .ReturnsAsync(Encoding.UTF8.GetBytes($"{new string('0', 64)}  {assetName}"));
            SetupReleases(false, release);

            Assert.That(await Check("3.9.0", includePrereleases: false, isSelfContained: true, UpdatePlatform.Linux), Is.Null);
            Assert.That(Directory.Exists(stagingBaseDir), Is.False);
        }


        [Test]
        public async Task CheckAndStageAsync_OnLinuxIgnoresAReleaseThatOnlyHasTheWindowsAssets()
        {
            SetupReleases(false, StageableRelease("v4.0.0"));

            Assert.That(await Check("3.9.0", includePrereleases: false, isSelfContained: true, UpdatePlatform.Linux), Is.Null);
        }


        [Test]
        public async Task CheckAndStageAsync_OnLinuxLeavesNothingStagedWhenTheTarballHasNoExecutable()
        {
            const string assetName = "JiraStopWatch-v4.0.0-linux-x64-self-contained.tar.gz";
            byte[] tarGz = MakeTarGz(Array.Empty<byte>(), false, entryName: "Other");
            SetupReleases(false, ReleaseWith("v4.0.0", false, assetName, tarGz));

            Assert.That(await Check("3.9.0", includePrereleases: false, isSelfContained: true, UpdatePlatform.Linux), Is.Null);
            Assert.That(Directory.Exists(Path.Combine(stagingBaseDir, "4.0.0")), Is.False);
        }


        private ReleaseInfo ReleaseWith(string tag, bool isPrerelease, string assetName, byte[] assetBytes)
        {
            string checksumName = assetName + ".sha256";
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


        /// <summary>A .tar.gz whose executable entry has mode 0755, as the release script writes it.</summary>
        private static byte[] MakeTarGz(byte[] exeBytes, bool includeSibling, string entryName = "StopWatch")
        {
            using MemoryStream output = new MemoryStream();
            using (GZipStream gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
            using (TarWriter tar = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: false))
            {
                PaxTarEntry exe = new PaxTarEntry(TarEntryType.RegularFile, entryName)
                {
                    DataStream = new MemoryStream(exeBytes),
                    Mode = (UnixFileMode)Convert.ToInt32("755", 8),
                };
                tar.WriteEntry(exe);

                if (includeSibling)
                {
                    tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "libSkiaSharp.so")
                    {
                        DataStream = new MemoryStream(new byte[] { 1, 2, 3 }),
                        Mode = (UnixFileMode)Convert.ToInt32("644", 8),
                    });
                }
            }

            return output.ToArray();
        }


        private Task<PendingUpdate> Check(string currentVersion, bool includePrereleases, bool isSelfContained = true, UpdatePlatform platform = UpdatePlatform.Windows)
        {
            return service.CheckAndStageAsync(
                checkForUpdatesEnabled: true,
                includePrereleases: includePrereleases,
                currentVersion: currentVersion,
                isSelfContained: isSelfContained,
                platform: platform,
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
