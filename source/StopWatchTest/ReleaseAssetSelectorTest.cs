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
    using System.Collections.Generic;
    using NUnit.Framework;
    using StopWatch.Update;


    [TestFixture]
    public class ReleaseAssetSelectorTest
    {
        [Test]
        public void AssetFileName_SelfContained_IsTheExe()
        {
            Assert.That(
                ReleaseAssetSelector.AssetFileName("3.1.0", selfContained: true),
                Is.EqualTo("JiraStopWatch-v3.1.0-self-contained.exe"));
        }


        [Test]
        public void AssetFileName_FrameworkDependent_IsTheZip()
        {
            Assert.That(
                ReleaseAssetSelector.AssetFileName("3.1.0", selfContained: false),
                Is.EqualTo("JiraStopWatch-v3.1.0-framework-dependent.zip"));
        }


        [Test]
        public void ChecksumFileName_AppendsSha256ToTheAssetName()
        {
            Assert.That(
                ReleaseAssetSelector.ChecksumFileName("3.1.0", selfContained: true),
                Is.EqualTo("JiraStopWatch-v3.1.0-self-contained.exe.sha256"));
        }


        [Test]
        public void FindAsset_MatchesByNameCaseInsensitively()
        {
            ReleaseInfo release = new ReleaseInfo
            {
                Assets = new List<ReleaseAsset>
                {
                    new ReleaseAsset { Name = "JIRASTOPWATCH-V3.1.0-SELF-CONTAINED.EXE", DownloadUrl = "https://example/asset" },
                },
            };

            ReleaseAsset found = ReleaseAssetSelector.FindAsset(release, "JiraStopWatch-v3.1.0-self-contained.exe");

            Assert.That(found, Is.Not.Null);
            Assert.That(found.DownloadUrl, Is.EqualTo("https://example/asset"));
        }


        [Test]
        public void FindAsset_ReturnsNullWhenNoAssetMatches()
        {
            ReleaseInfo release = new ReleaseInfo { Assets = new List<ReleaseAsset>() };

            Assert.That(ReleaseAssetSelector.FindAsset(release, "JiraStopWatch-v3.1.0-self-contained.exe"), Is.Null);
        }
    }
}
