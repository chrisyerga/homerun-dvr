using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Silicondust.HDHomeRun;
using HdHomerunLib.MpegTSParser;

// KTVU (FOX)  2 [SD] qam:79,1
// KRON (IND)  4 [SD] qam:79,2
// KPIX (CBS)  5 [SD] qam:79,3
// KGO  (ABC)  7 [SD] qam:79,4
// KQED (PBS)  9 [SD] qam:79,5
// KOFY       13 [SD] qam:79,7
// KBCW (CW)  12 [SD] qam:79,10
// KNTV (NBC)  4 [SD] qam:84,1
// KICU        6 [SD] qam:84,2
// KTEH (PBS) 10 [SD] qam:84,4

// ABC         7 [HD] qam:117,1
// KQED (PBS)  9 [HD] qam:117,2
// WGN  (IND)    [SD] qam:118,4
// ???           [SD] qam:120,4
// KPIX (CBS)    [HD] qam:122,1
// KTVU (FOX)    [HD] qam:122,2
// KTVU (FOX)    [SD] qam:122,3
// Discovery     [SD] qam:129,3

namespace HdHomerunLib
{
    public class Class1
    {
        bool done = false;

        static void DoQuery(MultiTuner hd, string query)
        {
            System.Console.WriteLine("\n=== {0} ===========================================", query);
            System.Console.WriteLine(hd.Get(query));
        }

        static void DoSet(MultiTuner hd, string name, string value)
        {
            System.Console.WriteLine("\n===SET=== {0} TO {1} =====================================", name, value);
            System.Console.WriteLine(hd.Set(name, value));
        }

        public static string GetLocalIP()
        {
            // Get host name
            string strHostName = Dns.GetHostName();
            Console.WriteLine("Host Name: " + strHostName);

            // Find host by name
            IPHostEntry iphostentry1 = Dns.GetHostEntry(strHostName);
            IPHostEntry iphostentry = Dns.GetHostByName(strHostName);

            // Enumerate IP addresses
            int nIP = 0;
            foreach (IPAddress ipaddress in iphostentry.AddressList)
            {
                Console.WriteLine("IP #" + ++nIP + ": " +
                                  ipaddress.ToString());
            }

            //! Need to set preferred network interface and remember this
            //! We could also eventually have different networked QAM tuners
            //! available on different networks.
            return iphostentry.AddressList[0].ToString();
        }

        void ReceiveThread()
        {
            const int udpPort = 6502;
            UdpClient client = new UdpClient(udpPort);
            IPEndPoint receivePoint;
            TransportStreamParser parser = new TransportStreamParser();
            TSDemux accumulator = new TSDemux(parser);

            FileStream capture = new FileStream(string.Format(@"c:\ts-capture-{0}.mpg", DateTime.Now.ToFileTime()), FileMode.Create);
            receivePoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), udpPort);
            int bytes = 0;
            int nextMeg = 1024 * 1024;

            //           while (bytes < 25 * 1024 * 1024)
            while (true)
            {
                byte[] data = client.Receive(ref receivePoint);
                //                capture.Write(data, 0, data.Length);
                bytes += data.Length;
                if (bytes > nextMeg)
                {
                    //                    System.Console.WriteLine("Bytes read: {0}", bytes);
                    nextMeg += 1024 * 1024;
                }

#if true
                int offset = 0;
                while (offset < data.Length)
                {
                    byte[] tsp = new byte[ParseUtils.MPEG_TS_PACKET_SIZE];
                    Buffer.BlockCopy(data, offset, tsp, 0, ParseUtils.MPEG_TS_PACKET_SIZE);

                    try
                    {
                        TSPacket packet = new TSPacket(tsp);
                        packet.ParseData();

                        parser.AcceptPacket(packet);
                    }
                    catch (Exception)
                    { }
                    offset += ParseUtils.MPEG_TS_PACKET_SIZE;
                }
#endif
            }

            capture.Close();
            done = true;
        }

        public void DoStuff()
        {
            Collection<Device> devices = Network.FindDevices();

            System.Threading.Thread thread = new System.Threading.Thread(ReceiveThread);
            thread.Start();

            foreach (Device dev in devices)
            {
                MultiTuner multi = (MultiTuner)dev;
                Tuner tuner = multi.Tuners[0];
                Channel channel = tuner.Channel;

                //                DoSet(multi, "/tuner1/channel", "qam:84");
                DoSet(multi, "/tuner1/channel", "qam:129");
                //               DoSet(multi, "/tuner1/program", "4");
                DoSet(multi, "/tuner1/target", string.Format("{0}:{1}", GetLocalIP(), "6502"));
                //                DoSet(multi, "/tuner1/filter", "0x0000-0x00FF 0x07C0-0x07C1 0x1FF0-0x1FFF");
                //                DoSet(multi, "/tuner1/channel", "qam:92");
                DoSet(multi, "/tuner1/filter", "0x0000-0x007F 0x0800-0x080F 0x0C00-0x1FFE");


                DoQuery(multi, "/tuner1/channel");
                DoQuery(multi, "/tuner1/channelmap");
                DoQuery(multi, "/tuner1/status");
                DoQuery(multi, "/tuner1/streaminfo");
                DoQuery(multi, "/tuner1/filter");

                System.Console.WriteLine(dev);

                while (!done)
                {
                    System.Threading.Thread.Sleep(2000);
#if false
                    DoQuery(multi, "/tuner1/channel");
                    DoQuery(multi, "/tuner1/channelmap");
                    DoQuery(multi, "/tuner1/program");
                    DoQuery(multi, "/tuner1/status");
                    DoQuery(multi, "/tuner1/streaminfo");
                    DoQuery(multi, "/tuner1/filter");
#endif
                }

                System.Console.WriteLine("Exiting");
            }
        }
    }
}
