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
    using System.IO;
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
            stagingBaseDir = Path.Combine(Path.GetTempPath(), $"StopWatchTest-{System.Guid.NewGuid():N}");
        }


        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(stagingBaseDir))
                Directory.Delete(stagingBaseDir, recursive: true);
        }


        [Test]
        public async Task CheckAndStageAsync_ReturnsNullAndTouchesNoNetworkWhenDisabled()
        {
            // MockBehavior.Strict means any unexpected call (e.g. to
            // GetLatestReleaseAsync) throws - this is itself the assertion
            // that a disabled check makes no network call at all.
            PendingUpdate result = await service.CheckAndStageAsync(
                checkForUpdatesEnabled: false,
                currentVersion: "3.0.0",
                isSelfContained: true,
                stagingBaseDir: stagingBaseDir);

            Assert.That(result, Is.Null);
        }


        [Test]
        public async Task CheckAndStageAsync_ReturnsNullWhenNoNewerReleaseExists()
        {
            releaseSource
                .Setup(r => r.GetLatestReleaseAsync())
                .ReturnsAsync(new ReleaseInfo { TagName = "v3.0.0", Assets = System.Array.Empty<ReleaseAsset>() });

            PendingUpdate result = await service.CheckAndStageAsync(
                checkForUpdatesEnabled: true,
                currentVersion: "3.0.0",
                isSelfContained: true,
                stagingBaseDir: stagingBaseDir);

            Assert.That(result, Is.Null);
        }


        [Test]
        public async Task CheckAndStageAsync_ReturnsNullWhenTheMatchingAssetIsMissing()
        {
            releaseSource
                .Setup(r => r.GetLatestReleaseAsync())
                .ReturnsAsync(new ReleaseInfo { TagName = "v3.1.0", Assets = System.Array.Empty<ReleaseAsset>() });

            PendingUpdate result = await service.CheckAndStageAsync(
                checkForUpdatesEnabled: true,
                currentVersion: "3.0.0",
                isSelfContained: true,
                stagingBaseDir: stagingBaseDir);

            Assert.That(result, Is.Null);
        }


        [Test]
        public async Task CheckAndStageAsync_CarriesTheReleaseUrlIntoTheStagedUpdate()
        {
            const string assetName = "JiraStopWatch-v3.1.0-self-contained.exe";
            const string checksumName = assetName + ".sha256";
            byte[] assetBytes = System.Text.Encoding.UTF8.GetBytes("fake exe contents");
            string digest = System.Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(assetBytes));

            releaseSource
                .Setup(r => r.GetLatestReleaseAsync())
                .ReturnsAsync(new ReleaseInfo
                {
                    TagName = "v3.1.0",
                    HtmlUrl = "https://github.com/menegrete/jirastopwatch/releases/tag/v3.1.0",
                    Assets = new[]
                    {
                        new ReleaseAsset { Name = assetName, DownloadUrl = "https://example.com/" + assetName },
                        new ReleaseAsset { Name = checksumName, DownloadUrl = "https://example.com/" + checksumName },
                    },
                });
            releaseSource
                .Setup(r => r.DownloadAssetAsync("https://example.com/" + checksumName))
                .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes($"{digest}  {assetName}"));
            releaseSource
                .Setup(r => r.DownloadAssetAsync("https://example.com/" + assetName))
                .ReturnsAsync(assetBytes);

            PendingUpdate result = await service.CheckAndStageAsync(
                checkForUpdatesEnabled: true,
                currentVersion: "3.0.0",
                isSelfContained: true,
                stagingBaseDir: stagingBaseDir);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ReleaseUrl, Is.EqualTo("https://github.com/menegrete/jirastopwatch/releases/tag/v3.1.0"));
        }
    }
}
