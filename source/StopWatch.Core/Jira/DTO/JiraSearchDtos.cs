using System.Collections.Generic;

namespace StopWatch
{
    /// <summary>One page of <c>GET /search/jql</c>. The last page has no token.</summary>
    internal class SearchResults
    {
        public List<Issue> Issues { get; set; } = new List<Issue>();
        public string NextPageToken { get; set; }
    }

    /// <summary><c>GET /project/{key}</c>, of which only the issue types are read.</summary>
    internal class ProjectDetails
    {
        public List<IssueTypeFields> IssueTypes { get; set; } = new List<IssueTypeFields>();
    }

    /// <summary>The answer to creating an issue.</summary>
    internal class CreatedIssue
    {
        public string Key { get; set; }
    }

    /// <summary>The answer to posting a worklog.</summary>
    internal class CreatedWorklog
    {
        public string Id { get; set; }
    }
}
