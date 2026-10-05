using System;
using System.Threading.Tasks;
using StopWatch.Plugin;

namespace StopWatch.Plugins
{
    /// <summary>
    /// One plugin's side of time loading: registers its handler and
    /// subscribes it to the outcome notifications. Created per plugin, so the
    /// registry always knows whose handler it holds.
    /// </summary>
    internal class PluginTimeLoad : IPluginTimeLoad
    {
        private readonly string pluginId;
        private readonly TimeLoadRegistry registry;

        public PluginTimeLoad(string pluginId, TimeLoadRegistry registry)
        {
            this.pluginId = pluginId;
            this.registry = registry;
        }

        public void RegisterInsteadOf(Func<TimeLoadRequest, Task<InsteadOfResult>> handler)
        {
            registry.RegisterInsteadOf(pluginId, handler);
        }

        public event EventHandler<TimeLoadedEventArgs> After
        {
            add { registry.AddObserver(value); }
            remove { registry.RemoveObserver(value); }
        }
    }


    /// <summary>Runs loads a plugin starts through the host's pipeline, with that plugin as their source.</summary>
    internal class PluginTimeLoader : ITimeLoader
    {
        private readonly string pluginId;
        private readonly TimeLoadPipeline pipeline;

        public PluginTimeLoader(string pluginId, TimeLoadPipeline pipeline)
        {
            this.pluginId = pluginId;
            this.pipeline = pipeline;
        }

        public async Task<TimeLoadResult> LoadAsync(string issueKey, DateTimeOffset startTime, TimeSpan timeElapsed, string comment, PluginEstimateUpdate estimateUpdate, string estimateValue)
        {
            TimeLoadPipelineResult result = await pipeline.LoadAsync(new TimeLoadInput
            {
                IssueKey = issueKey,
                StartTime = startTime,
                TimeElapsed = timeElapsed,
                Comment = comment ?? "",
                EstimateUpdateMethod = EstimateMapping.ToHost(estimateUpdate),
                EstimateUpdateValue = estimateValue ?? "",
                Source = TimeLoadSource.FromPlugin(pluginId)
            });

            return new TimeLoadResult(result.Outcome == TimeLoadOutcome.Succeeded, result.Reason);
        }
    }
}
