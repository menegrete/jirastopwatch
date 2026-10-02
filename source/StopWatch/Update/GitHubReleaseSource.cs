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
    /// Reads releases of this repo through the public, unauthenticated
    /// GitHub REST API - no token needed, and well within its 60
    /// requests/hour/IP anonymous rate limit for a once-per-launch check
    /// (one request per check either way). Without pre-releases it uses
    /// `releases/latest`, which GitHub already restricts to the latest
    /// non-draft, non-prerelease release.
    /// </summary>
    internal sealed class GitHubReleaseSource : IReleaseSource
    {
        private const string Owner = "menegrete";
        private const string Repo = "jirastopwatch";

        // GitHub lists releases newest-first by creation date, so the
        // highest version is always among the most recent ones.
        private const int ReleasesPageSize = 30;

        public async Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(bool includePrereleases)
        {
            string url = includePrereleases
                ? $"https://api.github.com/repos/{Owner}/{Repo}/releases?per_page={ReleasesPageSize}"
                : $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";

            using HttpResponseMessage response = await HttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            using System.IO.Stream stream = await response.Content.ReadAsStreamAsync();
            using JsonDocument document = await JsonDocument.ParseAsync(stream);

            List<ReleaseInfo> releases = new List<ReleaseInfo>();
            if (!includePrereleases)
            {
                releases.Add(ParseRelease(document.RootElement));
                return releases;
            }

            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("draft", out JsonElement draft) && draft.GetBoolean())
                    continue;

                releases.Add(ParseRelease(element));
            }
            return releases;
        }


        private static ReleaseInfo ParseRelease(JsonElement root)
        {
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
                IsPrerelease = root.TryGetProperty("prerelease", out JsonElement prerelease) && prerelease.GetBoolean(),
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
