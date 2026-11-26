namespace StopWatch
{
    /// <summary>
    /// An issue as a search returns it. <see cref="Summary"/> is Jira's own,
    /// never composed with the parent's summary or the project name the way
    /// <see cref="IssueSummaryResult"/> is, so it can be compared as it stands.
    /// </summary>
    internal class JiraIssueInfo
    {
        public string Key { get; set; } = "";
        public string Summary { get; set; } = "";
        public string IssueTypeId { get; set; } = "";
        public string IssueTypeName { get; set; } = "";
        public bool IsSubtask { get; set; }

        /// <summary>The parent's key, or empty when the issue has none.</summary>
        public string ParentKey { get; set; } = "";
        public string ProjectKey { get; set; } = "";
        public string Status { get; set; } = "";
    }


    /// <summary>An issue type a project offers.</summary>
    internal class JiraIssueType
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }
}
