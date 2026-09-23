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

using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace StopWatch.Update
{
    /// <summary>
    /// Reads the latest non-prerelease GitHub release for this repo through
    /// the public, unauthenticated GitHub REST API - no token needed, and
    /// well within its 60 requests/hour/IP anonymous rate limit for a
    /// once-per-launch check.
    /// </summary>
    internal sealed class GitHubReleaseSource : IReleaseSource
    {
        private const string Owner = "menegrete";
        private const string Repo = "jirastopwatch";

        public async Task<ReleaseInfo> GetLatestReleaseAsync()
        {
            using HttpResponseMessage response = await HttpClient.GetAsync(
                $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");
            response.EnsureSuccessStatusCode();

            using System.IO.Stream stream = await response.Content.ReadAsStreamAsync();
            using JsonDocument document = await JsonDocument.ParseAsync(stream);

            JsonElement root = document.RootElement;

            List<ReleaseAsset> assets = new List<ReleaseAsset>();
            foreach (JsonElement assetElement in root.GetProperty("assets").EnumerateArray())
            {
                assets.Add(new ReleaseAsset
                {
                    Name = assetElement.GetProperty("name").GetString(),
                    DownloadUrl = assetElement.GetProperty("browser_download_url").GetString(),
                });
            }

            return new ReleaseInfo
            {
                TagName = root.GetProperty("tag_name").GetString(),
                Assets = assets,
                HtmlUrl = root.GetProperty("html_url").GetString(),
            };
        }


        public async Task<byte[]> DownloadAssetAsync(string url)
        {
            using HttpResponseMessage response = await HttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }


        private static readonly HttpClient HttpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            HttpClient client = new HttpClient();
            // GitHub's API rejects requests with no User-Agent.
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"JiraStopWatch/{AppInfo.Version}");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.Timeout = System.TimeSpan.FromSeconds(30);
            return client;
        }
    }
}
