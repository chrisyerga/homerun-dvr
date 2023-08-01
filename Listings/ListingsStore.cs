#define SAVE_XML_LISTINGS

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Services;
using SchedulesDirect;
using System.Runtime.Serialization.Formatters.Binary;

namespace HdHomerunLib.Listings
{
    public class ListingsStore : IListingsStore
    {
        string directory;

        public ListingsStore(string Directory)
        {
            directory = Directory;
            if (System.IO.Directory.Exists(directory) == false)
            {
                System.IO.Directory.CreateDirectory(directory);
            }
        }

        public SchedulesDirect.xtvdResponse GetListingsForDate(DateTime dateParam)
        {
            // Normalize date to midnight of that day
            DateTime date = new DateTime(dateParam.Year, dateParam.Month, dateParam.Day);
            string filename = GetListingsPathForDate(date);
            SchedulesDirect.xtvdResponse results;

            // Have we loaded this before?
            if (File.Exists(filename))
            {
                // Yes -- load it from the cached file
                FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.ReadWrite);
                byte[] bytes = new byte[fs.Length];
                fs.Read(bytes, 0, (int)fs.Length);

                MemoryStream ms = new MemoryStream(bytes);
                ms.Position = 0;

                BinaryFormatter bf = new BinaryFormatter();
                results = (SchedulesDirect.xtvdResponse)bf.Deserialize(ms);

                ms.Close();
                fs.Close();
            }
            else
            {
                // Not there. Request from the web service.
                SchedulesDirect.xtvdWebService xtvd = new SchedulesDirect.xtvdWebService();
                System.Net.NetworkCredential cred = new System.Net.NetworkCredential("yergacheffe", "public");
                xtvd.Credentials = cred;

                // TMS date parameters are in UTC. So convert the local times we want to UTC
                DateTime startTime = date.ToUniversalTime();
                DateTime endTime = startTime + new TimeSpan(23, 59, 59);

                // TMS wants dates in this format: "2008-11-11T23:59:59Z";
                string startString = string.Format("{0}-{1}-{2}T{3:00}:00:00Z", startTime.Year, startTime.Month, startTime.Day, startTime.Hour);
                string endString = string.Format("{0}-{1}-{2}T{3:00}:59:59Z", endTime.Year, endTime.Month, endTime.Day, endTime.Hour);
                
                // Make the request
                System.Console.WriteLine("ListingsStore: Requesting listings for {0} from schedulesdirect.org", date);
                results = xtvd.download(startString, endString);
                foreach (string message in results.messages)
                {
                    System.Console.WriteLine("ListingsStore: Response from schedulesdirect.org: {0}", message);
                }

                // And save results to cached file
#if SAVE_XML_LISTINGS
                FileStream fsx = new FileStream(filename + ".xml", FileMode.Create);
                System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(SchedulesDirect.xtvdResponse));
                xs.Serialize(fsx, results);
#endif
                MemoryStream ms = new MemoryStream();
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(ms, results);
                ms.Position = 0;

                FileStream fs = new FileStream(filename, FileMode.Create);
                ms.WriteTo(fs);

                fs.Close();
                ms.Close();
            }

            return results;
        }

        private string GetListingsPathForDate(DateTime dateParam)
        {
            return string.Format("{0}\\listings-{1}-{2}-{3}", directory, dateParam.Year, dateParam.Month, dateParam.Day);
        }
    }
}
