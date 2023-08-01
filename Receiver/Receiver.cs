using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using HdHomerunLib.Listings;

namespace HdHomerunLib.Receiver
{
    public class FileInfo
    {
        public DateTime FileStartTime;
        public DateTime FileEndTime;
        public string Pathname;
        public string Service;
        public DateTime ProgramStartTime;
        public DateTime ProgramEndTime;

        public FileInfo(string filePath)
        {
            string worker;

            // Save path
            Pathname = filePath;

            // Strip off any path portion and remove the .mpg from the end
            worker = Path.GetFileName(filePath);
            worker = worker.Substring(0, worker.Length - 4);

            // Is it legacy (servicename-starttime.mpg) or new (servicename-starttime+endtime.mpg)?
            int dashIndex = worker.LastIndexOf('-');
            int plusIndex = worker.LastIndexOf('+');
            bool newFormat = plusIndex > 0;

            if (newFormat)
            {
                // We need file start/end times
                if (false == GetTimes("-F-", out FileStartTime, out FileEndTime))
                {
                    throw new FormatException(string.Format("Failed to parse file times for {0}", filePath));
                }

                // Program start/end times are optional
                GetTimes("-P-", out ProgramStartTime, out ProgramEndTime);
            }
            else
            {
                FileStartTime = DateTime.FromFileTime(long.Parse(worker.Substring(dashIndex + 1)));
                FileEndTime = FileStartTime + TimeSpan.FromMinutes(5);
            }

            Service = worker.Substring(0, worker.IndexOf('-'));
        }

        bool GetTimes(string indicator, out DateTime startTime, out DateTime endTime)
        {
            startTime = new DateTime();
            endTime = new DateTime();

            int index = Pathname.IndexOf(indicator);
            if (index < 0)
            {
                return false;
            }

            // WGNAMER-Bewitched+128751192000000000-128751210000000000.logo
            string worker = Pathname.Substring(index + indicator.Length);
            string start = worker.Substring(0, 18);
            string end = worker.Substring(19, 18);
            if (worker[18] != '+')
            {
                return false;
            }

            startTime = DateTime.FromFileTime(long.Parse(start));
            endTime = DateTime.FromFileTime(long.Parse(end));

            return true;
        }

        public bool IntersectsProgram(Program program)
        {
            if ((FileEndTime < program.StartTime) ||
                (FileStartTime > program.EndTime))
            {
                return false;
            }

            return true;
        }

        public bool ContainedWithinProgram(Program program)
        {
            return ((FileStartTime > program.StartTime) && (FileEndTime < program.EndTime));
        }
    }

    public class Receiver
    {
        private int port;
        private object filelock = new object();
        private FileStream file = null;
        private string service;
        private VideoStorage storage;
        static long nextPort = 6502;
        object staticlock = new object();
        Tuner tuner;

        public Receiver(VideoStorage Storage, Tuner tuner, string Service)
        {
            port = (int)System.Threading.Interlocked.Increment(ref nextPort);
            service = Service;
            storage = Storage;

            tuner.TuneTo(Service, port);
            this.tuner = tuner;
            System.Threading.Thread thread = new System.Threading.Thread(WriterThread);
            thread.Start();
        }

        public Receiver(VideoStorage Storage, string Service, int Port)
        {
            port = Port;
            service = Service;
            storage = Storage;

            System.Threading.Thread thread = new System.Threading.Thread(WriterThread);
            thread.Start();
        }

        void WriterThread()
        {
            bool startedThread = false;

            while (true)
            {
                string filename;

                lock (filelock)
                {
                    if (file != null)
                    {
                        storage.ReleaseStream(file);
                    }

                    file = storage.GetStreamForService(service);
                }
                System.Console.WriteLine("New file: {0}", file.Name);

                if (!startedThread)
                {
                    System.Threading.Thread thread = new System.Threading.Thread(ReceiveThread);
                    thread.Start();
                    startedThread = true;
                }

                // Report status to monitoring DB periodically
                tuner.UpdateStatus();

                // Sleep for 5 minutes
                System.Threading.Thread.Sleep(1000 * 60 * 5);
            }
        }

        void ReceiveThread()
        {
            UdpClient client = new UdpClient(port);
            IPEndPoint receivePoint;

            receivePoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), port);
            long bytes = 0;
            byte[] buffer = new byte[2 * 1024 * 1024];
            int offset = 0;
            int flushLevel = 2 * 1000 * 1000;
            client.Client.ReceiveBufferSize = 3 * 1024 * 1024;
            int biggestavail = -1;
            DateTime datarateStart = DateTime.Now;
            int dataRead = -1;
            TimeSpan samplePeriod = TimeSpan.FromSeconds(1);
            double lowestrate = 9999.0;

            client.Client.ReceiveTimeout = 1000;
            while (true)
            {
                if (dataRead == -1)
                {
                    datarateStart = DateTime.Now;
                    dataRead = 0;
                }

                byte[] data;

                try
                {
                    data = client.Receive(ref receivePoint);
                    dataRead += data.Length;

                    Buffer.BlockCopy(data, 0, buffer, offset, data.Length);
                    offset += data.Length;
                    bytes += data.Length;

                }
                catch (SocketException e)
                {
                    int poop = 1;
                }

                if (DateTime.Now - datarateStart > samplePeriod)
                {
                    double rate = (double)dataRead / (DateTime.Now - datarateStart).Seconds;

                    rate = rate * 8.0;
                    rate = rate / (1024.0 * 1024.0);

                    if (rate < 0.5)
                    {
                        System.Console.WriteLine("####### ALARM ####### Rate too low.: {0}", rate);
                        if (tuner != null)
                        {
                            System.Console.WriteLine("Retuning...");
                            tuner.Retune();
                            lowestrate = 9999.0;
                        }
                    }
                    if (rate < lowestrate)
                    {
                        System.Console.WriteLine(">>> New low rate for service: {0}", rate);
                        lowestrate = rate;
                    }

                    dataRead = -1;
                }

                if (client.Available > biggestavail)
                {
                    biggestavail = client.Available;
                    System.Console.WriteLine("Avail={0}", biggestavail);
                }

                if (offset > flushLevel)
                {
                    lock (filelock)
                    {
                        file.Write(buffer, 0, offset);
                        offset = 0;
                    }

                    System.Console.WriteLine("Bytes written: {0:0.00}MB", (double)bytes / (1024.0 * 1024.0));
                }
            }
        }
    }
}
