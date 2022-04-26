using LiveSharp.Dashboard.Models;
using LiveSharp.Debugging;
using LiveSharp.Shared.Debugging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Principal;

namespace LiveSharp.Dashboard.Services
{
    public class DebuggingService
    {
        private readonly ILogger _logger;

        public ConcurrentDictionary<string, string> Panels { get; } = new();

        public event EventHandler<string> MethodWatch; 
        public event EventHandler<string> MethodStart; 
        public event EventHandler<string> MethodEnd; 
        public event EventHandler<PanelUpdate> PanelUpdated;
        public event EventHandler PanelsCleared;

        public event EventHandler<DebuggerTreeInvocationViewModel> InvocationModelChanged;
        
        public DebugEventProcessor EventProcessor { get; } = new();

        public DebuggingService(ILogger logger)
        {
            _logger = logger;
        }

        public void FeedEvents(IReadOnlyList<DebugEvent> debugEvents)
        {
            EventProcessor.FeedEvents(debugEvents);
        }

        public void UpdateInvocationModel(DebuggerTreeInvocationViewModel invocationViewModel)
        {
            InvocationModelChanged?.Invoke(this, invocationViewModel);
        }

        public void NewMethodWatch(string content)
        {
            MethodWatch?.Invoke(this, content);
        }

        public void NewMethodStart(string content)
        {
            MethodStart?.Invoke(this, content);
        }

        public void NewMethodEnd(string content)
        {
            MethodEnd?.Invoke(this, content);
        }

        public void UpdatePanel(string panelName, string content)
        {
            Panels[panelName] = content;
            
            PanelUpdated?.Invoke(this, new PanelUpdate {
                PanelName = panelName,
                Content = content
            });
        }

        public void ClearPanels()
        {
            Panels.Clear();
            
            PanelsCleared?.Invoke(this, EventArgs.Empty);
        }
    }

    public class PanelUpdate
    {
        public string PanelName { get; set; }
        public string Content { get; set; }
    }

    public class InspectorInstanceUpdate
    {
        public string[] AliveObjects { get; }
        public string UpdatedObjectKey { get; }
        public string UpdateHtml { get; }

        public InspectorInstanceUpdate(string[] aliveObjects, string updatedObjectKey, string updateHtml)
        {
            AliveObjects = aliveObjects;
            UpdatedObjectKey = updatedObjectKey;
            UpdateHtml = updateHtml;
        }
    }
}