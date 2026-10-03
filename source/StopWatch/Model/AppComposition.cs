using System;
using System.Linq;

namespace StopWatch
{
    /// <summary>
    /// The composition root: builds the Jira client, the issue services and
    /// the view models once, so that the main window and the plugin host are
    /// handed the same instances instead of the window constructing them
    /// itself.
    /// </summary>
    internal class AppComposition
    {
        public AppComposition(Settings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            JiraApiRequestFactory = new JiraApiRequestFactory(new RestRequestFactory());

            RestClientFactory = new RestClientFactory();
            RestClientFactory.BaseUrl = settings.JiraBaseUrl;

            JiraClient = new JiraClient(JiraApiRequestFactory, new JiraApiRequester(RestClientFactory, JiraApiRequestFactory));

            JiraService = new IssueJiraService(JiraClient, settings);

            Issues = new IssueListViewModel(settings);

            ActiveTimer = new ActiveTimerViewModel(() => Issues.Issues.Cast<ITimerSource>());
        }


        public JiraApiRequestFactory JiraApiRequestFactory { get; private set; }

        public RestClientFactory RestClientFactory { get; private set; }

        public JiraClient JiraClient { get; private set; }

        public IssueJiraService JiraService { get; private set; }

        public IssueListViewModel Issues { get; private set; }

        public ActiveTimerViewModel ActiveTimer { get; private set; }
    }
}
