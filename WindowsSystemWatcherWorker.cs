using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Windows.Automation;
using HyperWindowsService.Messages;
using System.Reactive.Linq;

namespace HyperWindowsService
{

    public class WindowsSystemWatcherWorker : BackgroundService
    {
        private CancellationTokenSource _stopTokenSource { get; set; }
        private readonly ILogger<WindowsSystemWatcherWorker> _logger;
        private readonly HyperClient _hyperClient;
        private readonly IObservable<WindowEventMessage> _windowEvents;
        public WindowsSystemWatcherWorker(ILogger<WindowsSystemWatcherWorker> logger, HyperClient hyperClient)
        {
            _logger = logger;
            _hyperClient = hyperClient;
            _windowEvents = HookupEvents();
            _stopTokenSource = new CancellationTokenSource();
        }

        private IObservable<WindowEventMessage> HookupEvents()
        {
            var windowOpenedEvent = Observable.FromEventPattern<AutomationEventHandler, AutomationEventArgs>((handler) =>
            {
                Automation.AddAutomationEventHandler(WindowPattern.WindowOpenedEvent, AutomationElement.RootElement, TreeScope.Children, handler);
            }, (handler) =>
            {
                Automation.RemoveAutomationEventHandler(WindowPattern.WindowOpenedEvent, AutomationElement.RootElement, handler);
            }).Publish().RefCount();

            var windowClosedMessages = windowOpenedEvent.Select(e =>
            {
                // Note: must subscribe to closed event after open event to have access to the handle that was closed
                // since the automation element is disposed of before close event is fired
                // Another solution is to use a CacheRequest
                var element = e.Sender as AutomationElement;
                var elementName = element.Current.Name;
                var processId = element.Current.ProcessId;
                var windowHandle = element.Current.NativeWindowHandle;

                return Observable.FromEventPattern<AutomationEventHandler, AutomationEventArgs>((handler) =>
                {
                    Automation.AddAutomationEventHandler(WindowPattern.WindowClosedEvent, element, TreeScope.Element, handler);
                }, (handler) =>
                {
                    Automation.RemoveAutomationEventHandler(WindowPattern.WindowClosedEvent, element, handler);
                }).Select(e =>
                {
                    return new WindowEventMessage()
                    {
                        Event = "window.closed",
                        WindowHandle = windowHandle, //element.Cached.NativeWindowHandle,
                        ProcessId = processId, //element.Cached.ProcessId,
                        Name = elementName, //element.Cached.Name
                    };
                }).FirstAsync();
            }).Merge();

            var windowOpenedMessages = windowOpenedEvent.Select(e =>
            {
                var element = e.Sender as AutomationElement;
                return new WindowEventMessage()
                {
                    Event = "window.opened",
                    WindowHandle = element.Current.NativeWindowHandle,
                    ProcessId = element.Current.ProcessId,
                    Name = element.Current.Name
                };
            });

            return windowOpenedMessages.Merge(windowClosedMessages);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _windowEvents.Subscribe(async message =>
            {
                    _logger.LogInformation("Client :: sending message");
                    await _hyperClient.SendMessageAsync(message, _stopTokenSource.Token);
            });

            while (!_stopTokenSource.IsCancellationRequested)
            {
                _logger.LogInformation("Client :: connecting to server");
                await _hyperClient.ConnectAsync(_stopTokenSource.Token);
                _logger.LogInformation("Client :: connected, awaiting disconnect");
                await _hyperClient.WaitForDisconnectAsync(_stopTokenSource.Token);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _stopTokenSource.Cancel();
        }
    }
}