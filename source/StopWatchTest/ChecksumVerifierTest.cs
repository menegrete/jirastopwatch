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
    using System.Security.Cryptography;
    using System.Text;
    using NUnit.Framework;
    using StopWatch.Update;


    [TestFixture]
    public class ChecksumVerifierTest
    {
        [Test]
        public void Verify_TrueWhenDigestMatches()
        {
            byte[] content = Encoding.UTF8.GetBytes("hello world");
            string digest = System.Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

            Assert.That(ChecksumVerifier.Verify(content, $"{digest}  JiraStopWatch-v3.1.0-self-contained.exe\n"), Is.True);
        }


        [Test]
        public void Verify_IsCaseInsensitive()
        {
            byte[] content = Encoding.UTF8.GetBytes("hello world");
            string digest = System.Convert.ToHexString(SHA256.HashData(content)).ToUpperInvariant();

            Assert.That(ChecksumVerifier.Verify(content, $"{digest}  asset.exe\n"), Is.True);
        }


        [Test]
        public void Verify_FalseWhenDigestDoesNotMatch()
        {
            byte[] content = Encoding.UTF8.GetBytes("hello world");
            string wrongDigest = new string('0', 64);

            Assert.That(ChecksumVerifier.Verify(content, $"{wrongDigest}  asset.exe\n"), Is.False);
        }


        [Test]
        public void Verify_FalseWhenChecksumFileIsEmpty()
        {
            Assert.That(ChecksumVerifier.Verify(Encoding.UTF8.GetBytes("data"), ""), Is.False);
        }


        [Test]
        public void ParseDigest_ReadsOnlyTheFirstToken()
        {
            string digest = new string('a', 64);

            Assert.That(ChecksumVerifier.ParseDigest($"{digest}  asset.exe\n"), Is.EqualTo(digest));
        }


        [Test]
        public void ParseDigest_NullWhenTokenIsNotAValidLengthDigest()
        {
            Assert.That(ChecksumVerifier.ParseDigest("not-a-checksum-file"), Is.Null);
        }
    }
}
