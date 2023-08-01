using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Silicondust.HDHomeRun;

namespace HdHomerunLib.Receiver
{
    public class TunerManager
    {
        // 84-5 (Paid programming?)
        // 119-5
        // 120-4
        static Dictionary<string, QamSi> serviceSi = new Dictionary<string, QamSi>()
        {
            { "KPIX",    new QamSi(79, 3) },
            { "KGO",     new QamSi(79, 4) },
            { "WGNAMER", new QamSi(118, 4) },
            { "DSCP",    new QamSi(129, 3) },
            { "KFTYDT",  new QamSi(95, 1) },
            { "KTVU",    new QamSi(79, 1) },
            { "KRON",    new QamSi(79, 2) },
            { "KBCW",    new QamSi(79, 10) },
            { "KOFY",    new QamSi(79, 7) },
            { "KNTV",    new QamSi(84, 1) },
            { "KICU",    new QamSi(84, 2) },
            { "KCSMDT",  new QamSi(91, 4) },
            { "KQEDDT3", new QamSi(91, 8) },
            { "KTEH",    new QamSi(84, 4) },
            { "KKPX",    new QamSi(84, 5) },
            { "KQED",    new QamSi(79, 5) },
            { "FOODP",   new QamSi(78, 4) },
            { "DISNP",   new QamSi(78, 6) },
            { "USAP",    new QamSi(78, 7) },
            { "AETVP",   new QamSi(78, 9) },
            { "FXP",     new QamSi(78, 10) },
            { "LIFEP",   new QamSi(78, 11) },
            { "AMCP",    new QamSi(78, 12) },
            { "SCIFIP",  new QamSi(85, 13) },
            { "HISTP",   new QamSi(86, 9) },
            { "BRAVOP",  new QamSi(86, 10) },
            { "HGTVP",   new QamSi(86, 11) },
            { "TVLANDP", new QamSi(86, 14) },
            { "LMN",     new QamSi(89, 4) },
            { "APL",     new QamSi(119, 3) },
            { "TRAV",    new QamSi(119, 4) },
            { "OXYGENP", new QamSi(129, 2) },
            { "TLCP",    new QamSi(129, 4) },
            { "COMEDYP", new QamSi(129, 5) },
            { "VH1P",    new QamSi(129, 6) },
            { "TOONP",   new QamSi(129, 7) },
            { "EP",      new QamSi(129, 9) },
            { "MTVP",    new QamSi(129, 10) },
            { "NIKP",    new QamSi(129, 11) },
            { "SPIKEP",  new QamSi(129, 8) },
        };
    
        public void DiscoverDevices()
        {
            // Discover HDHR devices
            Collection<Device> devices = Network.FindDevices();

            // Save to our list
            foreach (Device dev in devices)
            {
                MultiTuner multi = (MultiTuner)dev;

                tuners.Add(new Tuner(multi, 0));
                tuners.Add(new Tuner(multi, 1));
            }

            // We're initialized now
            initialized = true;
        }

        public Tuner AcquireDevice()
        {
            if (false == initialized)
            {
                throw new ApplicationException("TunerManager not initialized. Call DiscoverDevices() first.");
            }

            foreach (Tuner t in tuners)
            {
                if (t.InUse == false)
                {
                    t.InUse = true;
                    return t;
                }
            }

            return null;
        }

        public void ReleaseDevice(Tuner t)
        {
            t.Detune();
            t.InUse = false;
        }

        public static QamSi GetQamSi(string service)
        {
            QamSi result = null;

            if (serviceSi.TryGetValue(service, out result))
            {
                return result;
            }

            return null;
        }

        public static string[] GetKnownServices()
        {
            return serviceSi.Keys.ToArray();
        }

        bool initialized = false;
        List<Tuner> tuners = new List<Tuner>();
    }

    public class QamSi
    {
        public QamSi(int channel, int prog)
        {
            QamChannel = channel;
            ProgramNumber = prog;
        }

        public int QamChannel;
        public int ProgramNumber;
    }

    public class Tuner
    {
        private string action;
        private string service;

        public Tuner(MultiTuner device, int TunerNumber)
        {
            hdhrDevice = device;
            tunerNumber = TunerNumber;
            inUse = false;

            action = "idle";
            service = "";
        }

        internal bool InUse
        {
            get { return inUse; }
            set { inUse = value; }
        }

        public void TuneTo(string Service, int Port)
        {
            QamSi si = TunerManager.GetQamSi(Service);

            if (si != null)
            {
                action = "tuned";
                service = Service;
                TuneTo(si.QamChannel, si.ProgramNumber, Port);
            }
        }

        public void TuneTo(int QamChannel, int ProgramNumber, int Port)
        {
            UpdateStatus();

            hdhrDevice.Set(TunerName + "/channel", "qam:" + QamChannel);
            hdhrDevice.Set(TunerName + "/program", ProgramNumber.ToString());
            hdhrDevice.Set(TunerName + "/target", string.Format("{0}:{1}", GetLocalIP(), Port));

            qamChannel = QamChannel;
            program = ProgramNumber;
            port = Port;
        }

        public void Retune()
        {
            TuneTo(qamChannel, program, port);
        }

        public void Detune()
        {
            action = "idle";
            service = "";
            hdhrDevice.Set(TunerName + "/program", 999.ToString());
        }

        public void UpdateStatus()
        {
            TunerStatus.UpdateStatus(hdhrDevice.Id.ToString("X") + "-" + tunerNumber.ToString(), action, service);
        }

        public void ConsoleQuery(string query)
        {
            System.Console.WriteLine("\n=== {0} ===========================================", query);
            System.Console.WriteLine(hdhrDevice.Get(TunerName + "/" + query));
        }

        private string GetLocalIP()
        {
            // Get host name
            string strHostName = Dns.GetHostName();

            // Find host by name
            IPHostEntry iphostentry = Dns.GetHostEntry(strHostName);

            // Enumerate IP addresses looking for an IPv4 addr, which is what the
            // HDHomerun takes
            foreach (IPAddress ipaddr in iphostentry.AddressList)
            {
                if (ipaddr.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ipaddr.ToString();
                }
            }

            return "bad.ip.addr.val";
        }

        private string TunerName
        {
            get { return string.Format("/tuner{0}", tunerNumber); }
        }

        private MultiTuner hdhrDevice;
        private int tunerNumber;
        private bool inUse;

        private int qamChannel;
        private int program;
        private int port;
    }
}
