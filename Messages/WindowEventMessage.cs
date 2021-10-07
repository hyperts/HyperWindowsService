using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperWindowsService.Messages
{
    public enum WindowEvent
    {
        Opened,
        Closed
    }
    public class WindowEventMessage
    {
        public int WindowHandle { get; set; }
        public string Event { get; set; }
        public int ProcessId { get; set; }
        public string Name { get; set; }
    }
}
