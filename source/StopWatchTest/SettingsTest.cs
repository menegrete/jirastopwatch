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
    using NUnit.Framework;
    using StopWatch;


    [TestFixture]
    public class SettingsTest
    {
        // Captured by BinaryFormatter-serializing two PersistedIssue records under
        // .NET 6 (the pre-migration format) and Base64-encoding the resulting bytes.
        // Issue 0: Key="ABC-123", TimerRunning=true, InitialStartTime=2024-05-01T10:30:00-03:00,
        //          SessionStartTime=2024-05-01T10:30:00, TotalTime=90min, Comment="hello",
        //          EstimateUpdateMethod=SetTo, EstimateUpdateValue="1h".
        // Issue 1: Key="XYZ-9", TimerRunning=false, InitialStartTime=null,
        //          SessionStartTime=2024-01-01, TotalTime=0, Comment="",
        //          EstimateUpdateMethod=Auto, EstimateUpdateValue="".
        private const string LegacyBlob =
            "AAEAAAD/////AQAAAAAAAAAMAgAAAEBzZXJpYWxpemUsIFZlcnNpb249MS4wLjAuMCwgQ3VsdHVyZT1uZXV0cmFsLCBQdWJsaWNLZXlUb2tlbj1udWxsBAEAAAB1U3lzdGVtLkNvbGxlY3Rpb25zLkdlbmVyaWMuTGlzdGAxW1tQZXJzaXN0ZWRJc3N1ZSwgc2VyaWFsaXplLCBWZXJzaW9uPTEuMC4wLjAsIEN1bHR1cmU9bmV1dHJhbCwgUHVibGljS2V5VG9rZW49bnVsbF1dAwAAAAZfaXRlbXMFX3NpemUIX3ZlcnNpb24EAAAQUGVyc2lzdGVkSXNzdWVbXQIAAAAICAkDAAAAAgAAAAIAAAAHAwAAAAABAAAABAAAAAQOUGVyc2lzdGVkSXNzdWUCAAAACQQAAAAJBQAAAA0CBQQAAAAOUGVyc2lzdGVkSXNzdWUIAAAAFDxLZXk+a19fQmFja2luZ0ZpZWxkHTxUaW1lclJ1bm5pbmc+a19fQmFja2luZ0ZpZWxkITxJbml0aWFsU3RhcnRUaW1lPmtfX0JhY2tpbmdGaWVsZCE8U2Vzc2lvblN0YXJ0VGltZT5rX19CYWNraW5nRmllbGQaPFRvdGFsVGltZT5rX19CYWNraW5nRmllbGQYPENvbW1lbnQ+a19fQmFja2luZ0ZpZWxkJTxFc3RpbWF0ZVVwZGF0ZU1ldGhvZD5rX19CYWNraW5nRmllbGQkPEVzdGltYXRlVXBkYXRlVmFsdWU+a19fQmFja2luZ0ZpZWxkAQADAAABBAEBFVN5c3RlbS5EYXRlVGltZU9mZnNldA0MFUVzdGltYXRlVXBkYXRlTWV0aG9kcwIAAAACAAAABgYAAAAHQUJDLTEyMwEE+f///xVTeXN0ZW0uRGF0ZVRpbWVPZmZzZXQCAAAACERhdGVUaW1lDU9mZnNldE1pbnV0ZXMAAA0HAPzXzOJp3AhM/wDEiqfJadwIAJymkgwAAAAGCAAAAAVoZWxsbwX3////FUVzdGltYXRlVXBkYXRlTWV0aG9kcwEAAAAHdmFsdWVfXwAIAgAAAAIAAAAGCgAAAAIxaAEFAAAABAAAAAYLAAAABVhZWi05AAoAwACZXArcCAAAAAAAAAAABgwAAAAAAfP////3////AAAAAAkMAAAACw==";


        [Test]
        public void WriteThenReadIssues_JsonRoundTrip()
        {
            List<PersistedIssue> original = new List<PersistedIssue>
            {
                new PersistedIssue
                {
                    Key = "ABC-123",
                    TimerRunning = true,
                    InitialStartTime = new DateTimeOffset(2024, 5, 1, 10, 30, 0, TimeSpan.FromHours(-3)),
                    SessionStartTime = new DateTime(2024, 5, 1, 10, 30, 0),
                    TotalTime = TimeSpan.FromMinutes(90),
                    Comment = "hello",
                    EstimateUpdateMethod = EstimateUpdateMethods.SetTo,
                    EstimateUpdateValue = "1h"
                }
            };

            string json = Settings.Instance.WriteIssues(original);
            Assert.That(json.TrimStart(), Does.StartWith("["));

            List<PersistedIssue> roundTripped = Settings.Instance.ReadIssues(json);

            Assert.That(roundTripped, Has.Count.EqualTo(1));
            Assert.That(roundTripped[0].Key, Is.EqualTo("ABC-123"));
            Assert.That(roundTripped[0].TimerRunning, Is.True);
            Assert.That(roundTripped[0].InitialStartTime, Is.EqualTo(original[0].InitialStartTime));
            Assert.That(roundTripped[0].SessionStartTime, Is.EqualTo(original[0].SessionStartTime));
            Assert.That(roundTripped[0].TotalTime, Is.EqualTo(original[0].TotalTime));
            Assert.That(roundTripped[0].Comment, Is.EqualTo("hello"));
            Assert.That(roundTripped[0].EstimateUpdateMethod, Is.EqualTo(EstimateUpdateMethods.SetTo));
            Assert.That(roundTripped[0].EstimateUpdateValue, Is.EqualTo("1h"));
        }


        [Test]
        public void ReadIssues_DecodesLegacyBinaryFormatterBlob()
        {
            // Also exercises the migration re-save: a successful legacy read writes
            // the app's real user-scoped settings in the new JSON format.
            List<PersistedIssue> issues = Settings.Instance.ReadIssues(LegacyBlob);

            Assert.That(issues, Has.Count.EqualTo(2));

            Assert.That(issues[0].Key, Is.EqualTo("ABC-123"));
            Assert.That(issues[0].TimerRunning, Is.True);
            Assert.That(issues[0].InitialStartTime, Is.EqualTo(new DateTimeOffset(2024, 5, 1, 10, 30, 0, TimeSpan.FromHours(-3))));
            Assert.That(issues[0].SessionStartTime, Is.EqualTo(new DateTime(2024, 5, 1, 10, 30, 0)));
            Assert.That(issues[0].TotalTime, Is.EqualTo(TimeSpan.FromMinutes(90)));
            Assert.That(issues[0].Comment, Is.EqualTo("hello"));
            Assert.That(issues[0].EstimateUpdateMethod, Is.EqualTo(EstimateUpdateMethods.SetTo));
            Assert.That(issues[0].EstimateUpdateValue, Is.EqualTo("1h"));

            Assert.That(issues[1].Key, Is.EqualTo("XYZ-9"));
            Assert.That(issues[1].TimerRunning, Is.False);
            Assert.That(issues[1].InitialStartTime, Is.Null);
            Assert.That(issues[1].SessionStartTime, Is.EqualTo(new DateTime(2024, 1, 1)));
            Assert.That(issues[1].TotalTime, Is.EqualTo(TimeSpan.Zero));
            Assert.That(issues[1].EstimateUpdateMethod, Is.EqualTo(EstimateUpdateMethods.Auto));
        }


        [Test]
        public void ReadIssues_ReturnsEmptyList_WhenDataIsNullOrEmpty()
        {
            Assert.That(Settings.Instance.ReadIssues(null), Is.Empty);
            Assert.That(Settings.Instance.ReadIssues(""), Is.Empty);
        }


        [Test]
        public void ReadIssues_ReturnsEmptyList_WhenLegacyBlobIsCorrupt()
        {
            List<PersistedIssue> issues = Settings.Instance.ReadIssues("not a valid base64 blob and not JSON either###");

            Assert.That(issues, Is.Empty);
        }
    }
}
