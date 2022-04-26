using System;
using System.Collections.Generic;
using System.Linq;
using LiveSharp.Runtime.IL;
using LiveSharp.Runtime.Virtual;
using System.Collections.Concurrent;

namespace LiveSharp.Runtime
{
    class DashboardUpdateHandler
    {
        private readonly ConcurrentDictionary<string, ILiveSharpDashboard> _liveSharpDashboards;
        private readonly LiveSharpRuntimeProxy _liveSharpRuntimeProxy;

        public DashboardUpdateHandler(ConcurrentDictionary<string, ILiveSharpDashboard> liveSharpDashboard,
            LiveSharpRuntimeProxy liveSharpRuntimeProxy)
        {
            _liveSharpDashboards = liveSharpDashboard;
            _liveSharpRuntimeProxy = liveSharpRuntimeProxy;
        }

        public void CodeUpdated(IReadOnlyList<IUpdatedMethod> updatedMethods)
        {
            var dashboardConfigureUpdate = updatedMethods.FirstOrDefault(IsDashboardConfigure);
            if (dashboardConfigureUpdate != null) {
                _liveSharpRuntimeProxy.ClearHandlers();
                foreach (var dashboard in _liveSharpDashboards.Values) {
                    dashboard.Configure(_liveSharpRuntimeProxy);
                }
            }

            foreach (var updatedMethod in updatedMethods) {
                var declaringType = updatedMethod.DeclaringType;

                if (updatedMethod == dashboardConfigureUpdate)
                    continue;

                if (IsDashboardUpdate(declaringType)) {
                    try {
                        var virtualMethodInfo = (VirtualMethodInfo)updatedMethod.MethodMetadata;
                        var metadata = virtualMethodInfo.DelegateBuilder;

                        foreach (var dashboard in _liveSharpDashboards.Values) {
                            if (dashboard.GetType() != metadata.DeclaringType)
                                continue;
                            
                            if (virtualMethodInfo.Parameters.Length == 1 && metadata.Parameters[0].ParameterType == typeof(ILiveSharpRuntime))
                                metadata.Invoke(dashboard, _liveSharpRuntimeProxy);
                            else if (virtualMethodInfo.Parameters.Length == 0)
                                metadata.Invoke(dashboard);
                        }
                    }
                    catch (Exception e) {
                        _liveSharpRuntimeProxy.Logger.LogError("LiveSharpDashboard Run method failed", e);
                    }
                }
            }
            
        }

        private bool IsDashboardConfigure(IUpdatedMethod updatedMethod)
        {
            var methodIdentifier = updatedMethod.MethodIdentifier;

            if (methodIdentifier.StartsWith("LiveSharp.LiveSharpDashboard") && methodIdentifier.Contains(" Configure "))
                return true;

            if (methodIdentifier.StartsWith("LiveSharp.LiveSharpDashboard") && methodIdentifier.Contains(" <Configure>"))
                return true;

            return false;
        }

        private static bool IsDashboardUpdate(Type declaringType)
        {
            return declaringType.Name == "LiveSharpDashboard" ||
                   declaringType.DeclaringType?.Name == "LiveSharpDashboard";
        }
    }
}