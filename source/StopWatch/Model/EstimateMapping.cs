using System;
using StopWatch.Plugin;

namespace StopWatch
{
    /// <summary>Translates between the host's estimate methods and the contract's, which are kept apart so no host type leaks to plugins.</summary>
    internal static class EstimateMapping
    {
        public static PluginEstimateUpdate ToPlugin(EstimateUpdateMethods method)
        {
            switch (method)
            {
                case EstimateUpdateMethods.Leave:
                    return PluginEstimateUpdate.Leave;
                case EstimateUpdateMethods.SetTo:
                    return PluginEstimateUpdate.SetTo;
                case EstimateUpdateMethods.ManualDecrease:
                    return PluginEstimateUpdate.ManualDecrease;
                default:
                    return PluginEstimateUpdate.Auto;
            }
        }


        public static EstimateUpdateMethods ToHost(PluginEstimateUpdate update)
        {
            switch (update)
            {
                case PluginEstimateUpdate.Leave:
                    return EstimateUpdateMethods.Leave;
                case PluginEstimateUpdate.SetTo:
                    return EstimateUpdateMethods.SetTo;
                case PluginEstimateUpdate.ManualDecrease:
                    return EstimateUpdateMethods.ManualDecrease;
                default:
                    return EstimateUpdateMethods.Auto;
            }
        }
    }
}
