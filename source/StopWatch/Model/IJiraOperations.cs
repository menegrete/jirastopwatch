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

namespace StopWatch
{
    /// <summary>
    /// The slice of <see cref="JiraClient"/> that the issue-row services need.
    ///
    /// Exists so that the worklog rules and the filter lookup can be tested
    /// without going through the REST request plumbing, which is already
    /// covered by JiraClient's own tests.
    /// </summary>
    internal interface IJiraOperations
    {
        bool SessionValid { get; }

        string GetIssueSummary(string key, bool addProjectName);

        TimetrackingFields GetIssueTimetracking(string key);

        bool PostWorklog(string key, DateTimeOffset startTime, TimeSpan time, string comment, EstimateUpdateMethods estimateUpdateMethod, string estimateUpdateValue);

        bool PostComment(string key, string comment);

        SearchResult GetIssuesByJQL(string jql);

        List<Filter> GetFavoriteFilters();
    }
}
