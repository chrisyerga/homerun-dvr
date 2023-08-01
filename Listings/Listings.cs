using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SchedulesDirect;

namespace HdHomerunLib.Listings
{
    public class Program
    {
        string service;
        string serviceName;
        string affiliate;
        int channelNumber;
        int stationId;

        DateTime time;
        TimeSpan duration;
        string programId;

        string title;
        string episodeTitle;
        string description;
        string episodeNumber;
        string showType;
        string series;
        DateTime originalAirDate;

        public string[] Actors = new string[0];
        public string[] Genres = new string[0];

        bool complete;

        internal Program(stationsStation Station, schedulesSchedule Schedule)
        {
            service = Station.callSign;
            affiliate = Station.affiliate;
            try
            {
                channelNumber = int.Parse(Station.fccChannelNumber);
            }
            catch (Exception)
            {
                channelNumber = 0;
            }
            serviceName = Station.name;
            stationId = Station.id;

            // Sample schedule entry: 
            // <schedule program="SH000000010000" station="10150" time="2008-12-29T15:00:00Z" duration="PT00H30M" />
            DateTime utcStart = DateTime.SpecifyKind(Schedule.time, DateTimeKind.Utc);
            time = utcStart.ToLocalTime();
            string durString = Schedule.duration;
            int durHours = int.Parse(durString.Substring(2, 2));
            int durMinutes = int.Parse(durString.Substring(5, 2));
            duration = new TimeSpan(durHours, durMinutes, 0);

            programId = Schedule.program;

            complete = false;
        }

        internal void TryResolveCrew(productionCrewCrew crew)
        {
            if (programId != crew.program)
            {
                // Not useful to us
                return;
            }

            List<string> members = new List<string>();

            foreach (crewMember member in crew.member)
            {
                if ((member.role == "Actor") || (member.role == "Guest"))
                {
                    members.Add(member.givenname + " " + member.surname);
                }
            }

            Actors = members.ToArray();
        }

        internal void TryResolveGenre(genresProgramGenre genre)
        {
            if (programId != genre.program)
            {
                return;
            }

            List<string> genreStrings = new List<string>();

            foreach (genresProgramGenreGenre item in genre.genre)
            {
                genreStrings.Add(item.@class);
            }

            Genres = genreStrings.ToArray();
        }

        internal bool TryResolvingProgram(programsProgram programData)
        {
            if (programId != programData.id)
            {
                // Not useful to us
                return false;
            }

            // We've found our programData, so stash the interesting bits away
            title = programData.title;
            episodeTitle = programData.subtitle;
            description = programData.description;
            episodeNumber = programData.syndicatedEpisodeNumber;

            originalAirDate = programData.originalAirDate;
            series = programData.series;
            showType = programData.showType;

            // We're done now
            complete = true;

            return true;
        }

        public string Service
        {
            get { return service; }
        }

        public string Affiliate
        {
            get { return affiliate; }
        }

        public int ChannelNumber
        {
            get { return channelNumber; }
        }

        public DateTime StartTime
        {
            get { return time; }
        }

        public TimeSpan Duration
        {
            get { return duration; }
        }

        public DateTime EndTime
        {
            get { return time + duration; }
        }

        public string Title
        {
            get { return title; }
        }

        public string EpisodeTitle
        {
            get { return episodeTitle; }
        }

        public string EpisodeNumber
        {
            get { return episodeNumber; }
        }

        public string Description
        {
            get { return description; }
        }

        public string UriParams
        {
            get
            {
                return "service=" + service + "&time=" + time.ToFileTime();
            }
        }

        public string ToString()
        {
            return string.Format("{0}{1}", Title, EpisodeTitle == null ? "" : "("+EpisodeTitle+")");
        }
    }

    public interface IListingsStore
    {
        SchedulesDirect.xtvdResponse GetListingsForDate(DateTime dateParam);
    }

    public class Listings
    {
        IListingsStore store;
        Dictionary<string, List<Program>> listingsCache = new Dictionary<string, List<Program>>();

        public Listings(IListingsStore Store)
        {
            store = Store;
        }

        public Program GetListingsForTime(string Service, DateTime Time)
        {
            List<Program> allDay = GetListingsForDay(Service, Time);

            foreach (Program program in allDay)
            {
                if (program.StartTime <= Time && Time - program.StartTime < program.Duration)
                {
                    return program;
                }
            }

            return null;
        }

        public List<Program> GetListingsForDay(string Service, DateTime Time)
        {
            List<Program> results = null;

            // First check the cache
            if (listingsCache.TryGetValue(Service, out results))
            {
                // We got something. See if it's the correct day
                Program midDay = results[results.Count / 2];
                if (midDay.StartTime.Year == Time.Year &&
                    midDay.StartTime.Month == Time.Month &&
                    midDay.StartTime.Day == Time.Day)
                {
                    // That's it
                    return results;
                }

                // Cache miss. Erase the old cache entry
                listingsCache.Remove(Service);
            }

            xtvdResponse full = store.GetListingsForDate(Time);
            stationsStation serviceStation = null;
            int stationId = 0;
            results = new List<Program>();
            HashSet<string> interestingPrograms = new HashSet<string>();

            // Iterate the stations looking for the service in question
            foreach (stationsStation station in full.xtvd.stations)
            {
                // Service ID = callSign 
                if (station.callSign == Service)
                {
                    // Got it. Can stop looking now.
                    serviceStation = station;
                    stationId = station.id;
                    break;
                }
            }

            // Now iterate all schedule entries looking for ones for our station
            foreach (schedulesSchedule schedule in full.xtvd.schedules)
            {
                if (schedule.station == stationId)
                {
                    // Create a Program entry for this item. Note that at this point
                    // all we have is the station and schedule data. The program info
                    // (title, description, etc.) must be resolved later when we
                    // iterate the program entries
                    Program program = new Program(serviceStation, schedule);
                    results.Add(program);

                    // Add this programId to the list of items we need to find later
                    interestingPrograms.Add(schedule.program);
                }
            }

            // Iterate all programIds looking for ones we must link up to our Programs
            foreach (programsProgram programData in full.xtvd.programs)
            {
                if (interestingPrograms.Contains(programData.id))
                {
                    // No need to be smart here. Just give every one of the Programs
                    // a chance to resolve against this
                    foreach (Program program in results)
                    {
                        program.TryResolvingProgram(programData);
                    }
                }
            }

            foreach (productionCrewCrew crew in full.xtvd.productionCrew)
            {
                if (interestingPrograms.Contains(crew.program))
                {
                    foreach (Program program in results)
                    {
                        program.TryResolveCrew(crew);
                    }
                }
            }

            foreach (genresProgramGenre genre in full.xtvd.genres)
            {
                if (interestingPrograms.Contains(genre.program))
                {
                    foreach (Program program in results)
                    {
                        program.TryResolveGenre(genre);
                    }
                }
            }

            // Update the cache
            listingsCache[Service] = results;

            return results;
        }
    }
}
