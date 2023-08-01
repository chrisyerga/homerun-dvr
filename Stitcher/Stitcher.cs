using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HdHomerunLib.Listings;
using HdHomerunLib.Receiver;

namespace HdHomerunLib.Stitcher
{
    /// <summary>
    /// Manages stitching together the raw video slices from VideoStorage into recordings for a Program.
    /// </summary>
    public class Stitcher
    {
        private VideoStorage videoStorage;
        private HdHomerunLib.Listings.Listings listings;

        public Stitcher(VideoStorage storage, Listings.Listings epg)
        {
            videoStorage = storage;
            listings = epg;
        }

        public IEnumerable<Program> GetPotentialRecordings(string service, DateTime startTime, DateTime endTime)
        {
            // Get the list of time spans that have been recorded for this service
            List<RecordedRegion> spans = videoStorage.GetRecordedRegions(service);

            // Quantize the start and end times to day granularity. Since we can only request
            // Listings data for an entire day at a time, we need this to iterate the listings.
            DateTime startDay = new DateTime(startTime.Year, startTime.Month, startTime.Day);
            DateTime endDay = new DateTime(endTime.Year, endTime.Month, endTime.Day);
            DateTime currentDay = startDay;

            // For each full day of listings
            while (currentDay <= endDay)
            {
                List<Program> programs = listings.GetListingsForDay(service, currentDay);
                foreach (Program program in programs)
                {
                    // First see if this program is within the requested range
                    if (program.StartTime < endTime && program.EndTime > startTime)
                    {
                        // It's within range. Now walk all the recorded regions to see
                        // if this program is inside one of them
                        foreach (RecordedRegion region in spans)
                        {
                            if (region.ContainsProgram(program))
                            {
                                yield return program;
                            }
                        }
                    }
                }

                currentDay = currentDay + TimeSpan.FromDays(1);
            }
        }

        public IEnumerable<Program> GetPotentialRecordings(string service, DateTime forDay)
        {
            foreach (Program program in GetPotentialRecordings(service, forDay, forDay + new TimeSpan(23, 59, 59)))
            {
                yield return program;
            }
        }

        public IEnumerable<Program> GetPotentialRecordings(string service)
        {
            // Get the list of recorded time spans for the service
            List<RecordedRegion> regions = videoStorage.GetRecordedRegions(service);
            DateTime start = new DateTime(2099, 1, 1);
            DateTime end = new DateTime(1999, 1, 1);

            // Get the Start and End of all spans
            foreach (RecordedRegion region in regions)
            {
                if (region.StartTime < start)
                {
                    start = region.StartTime;
                }
                if (region.EndTime > end)
                {
                    end = region.EndTime;
                }
            }

            // Return programs within the totality of the spans
            foreach (Program program in GetPotentialRecordings(service, start, end))
            {
                yield return program;
            }
        }



    }
}
