using System;
using System.Linq;
using StopWatch.Logging;
using StopWatch.Plugins;

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

            Action<string, Exception> log = (message, ex) => Logger.Instance.Log(message, ex);

            PluginJira = new PluginJiraApi(JiraService, JiraClient, log);
            TimeLoadRegistry = new TimeLoadRegistry();
            TimeLoad = new TimeLoadPipeline(JiraService, TimeLoadRegistry, PluginJira, log);

            Issues = new IssueListViewModel(settings);

            ActiveTimer = new ActiveTimerViewModel(() => Issues.Issues.Cast<ITimerSource>());
        }


        public JiraApiRequestFactory JiraApiRequestFactory { get; private set; }

        public RestClientFactory RestClientFactory { get; private set; }

        public JiraClient JiraClient { get; private set; }

        public IssueJiraService JiraService { get; private set; }

        /// <summary>The Jira API plugins use. Shared, so the pipeline can count writes made through it.</summary>
        public PluginJiraApi PluginJira { get; private set; }

        public TimeLoadRegistry TimeLoadRegistry { get; private set; }

        public TimeLoadPipeline TimeLoad { get; private set; }

        public IssueListViewModel Issues { get; private set; }

        public ActiveTimerViewModel ActiveTimer { get; private set; }
    }
}
