using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Runtime.InteropServices;


namespace OldNewGateway
{

    // PENDING STATUS
    public enum ServiceState
    {
        SERVICE_STOPPED = 0x00000001,
        SERVICE_START_PENDING = 0x00000002,
        SERVICE_STOP_PENDING = 0x00000003,
        SERVICE_RUNNING = 0x00000004,
        SERVICE_CONTINUE_PENDING = 0x00000005,
        SERVICE_PAUSE_PENDING = 0x00000006,
        SERVICE_PAUSED = 0x00000007,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ServiceStatus
    {
        public int dwServiceType;
        public ServiceState dwCurrentState;
        public int dwControlsAccepted;
        public int dwWin32ExitCode;
        public int dwServiceSpecificExitCode;
        public int dwCheckPoint;
        public int dwWaitHint;
    };

   

    public partial class OldNewGateway : ServiceBase
    {
        private int eventID = 1;

        // PENDING STATUS
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(System.IntPtr handle, ref ServiceStatus serviceStatus);

        public OldNewGateway()
        {
            InitializeComponent();
            myEventLog = new System.Diagnostics.EventLog();
            if (!System.Diagnostics.EventLog.SourceExists("OldNewGatewaySource"))
            {
                System.Diagnostics.EventLog.CreateEventSource("OldNewGatewaySource", "OldNewGatewayLog");
            }
            myEventLog.Source = "OldNewGatewaySource";
            myEventLog.Log = "OldNewGatewayLog";
        }

        protected override void OnStart(string[] args)
        {
            myEventLog.WriteEntry("OldNewGateway started");

            // Update the service state to Start Pending.
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);


            // set up a timer that triggers every minute.
            Timer myTimer = new Timer();
            myTimer.Interval = 60000; // 60 seconds
            myTimer.Elapsed += new ElapsedEventHandler(this.OnTimer);
            myTimer.Start();

            // Update the service state to Running.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
        }

        protected override void OnContinue()
        {
            myEventLog.WriteEntry("OldNewGateway started again");
        }

        protected override void OnStop()
        {
            // Update the service state to Stop Pending.
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOP_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            myEventLog.WriteEntry("OldNewGateway stopped");

            // Update the service state to Stopped.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOPPED;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);  
        }

        public void OnTimer (object sender, ElapsedEventArgs args)
        {
            // TODO: Insert monitoring activities here.
            myEventLog.WriteEntry("monitoring the system", EventLogEntryType.Information, eventID++);
        }
    }
}
