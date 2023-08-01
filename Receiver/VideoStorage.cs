using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HdHomerunLib.Listings;

namespace HdHomerunLib.Receiver
{
    /// <summary>
    /// Describes a contiguous region of time that has been recorded on a service.
    /// </summary>
    public class RecordedRegion
    {
        public DateTime StartTime;
        public DateTime EndTime;

        /// <summary>
        /// Returns true if this span of recorded service contains the given Program.
        /// </summary>
        /// <param name="program"></param>
        /// <returns></returns>
        public bool ContainsProgram(Program program)
        {
            DateTime programEnd = program.StartTime + program.Duration;

            return StartTime <= program.StartTime && programEnd <= EndTime;
        }

        public TimeSpan Duration
        {
            get { return EndTime - StartTime; }
        }
    }

    /// <summary>
    /// Abstracts the storage of raw captured MPEG-2 video by service.
    /// <para>
    /// This class is designed to support the high-level approach used by the overall system,
    /// which is to simply record each given service continuously. The data is stored here by
    /// a Receiver which has no knowledge of Listings Data or program boundaries, etc. So
    /// essentially this is just a raw store of video data by service.
    /// </para>
    /// <para>
    /// The Receiver records separate files for a chunk of time. For example there might be
    /// five minute chunks of time stored in successive files for a given service. There's
    /// no guarantee of any time alignment for these file chunks, but the receiver does
    /// ensure that there is no gap between chunks. So a series of 5 minute files can be
    /// appended to create a contiguous usable MPEG-2 transport stream.
    /// </para>
    /// <remarks>
    /// This class maintains state about the slices of video stored here. The slices are
    /// stored as files and the filenames themselves contain sufficient metadata to recreate
    /// all internal data. So if the service stops for any reason, simply restarting and 
    /// scanning the files is sufficient to rebuilt the data. However, <i>while the VideoStorage
    /// instance is alive</i> nobody should write or modify any of the files under its
    /// root directory, as it will not know and thus will not have complete data about the
    /// contents of the VideoStorage. Indeed, there should be no reason at all for anyone to
    /// ever look at the files maintained by the VideoStorage, but the structure is obvious
    /// by simply looking at the filesystem so resist the temptation.
    /// </remarks>
    /// </summary>
    public class VideoStorage
    {
        /// <summary>
        /// The sub-directory under the root directory where the video slices for each
        /// service are stored.
        /// </summary>
        private string servicesDirectory;

        /// <summary>
        /// The sub-directory under the root directory where output recordings are
        /// stored. This is the one subdirectory under the root where others are allowed
        /// to look, as this is the staging directory where recordings for a program
        /// are stitched together and made available externally.
        /// </summary>
        private string outputDirectory;

        /// <summary>
        /// List of data about all video recorded for each service.
        /// </summary>
        private Dictionary<string, SortedList<DateTime, FileInfo>> services = new Dictionary<string, SortedList<DateTime, FileInfo>>();

        /// <summary>
        /// Construct a VideoStorage instance. It's only useful to have one of these, but that's
        /// not enforced.
        /// </summary>
        /// <param name="DirectoryBase">Base directory under which the Video is stored</param>
        public VideoStorage(string DirectoryBase)
        {
            servicesDirectory = Path.Combine(DirectoryBase, "Services");
            outputDirectory = Path.Combine(DirectoryBase, "Output");

            // First make certain the storage base dir and subdirs are present
            if (false == Directory.Exists(DirectoryBase))
            {
                Directory.CreateDirectory(DirectoryBase);
            }
            if (false == Directory.Exists(servicesDirectory))
            {
                Directory.CreateDirectory(servicesDirectory);
            }
            if (false == Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            // Enumerate services. Each subdir is a service ID
            string[] directories = Directory.GetDirectories(servicesDirectory);
            foreach (string serviceDir in directories)
            {
                string serviceName = Path.GetFileName(serviceDir);
                SortedList<DateTime, FileInfo> serviceList = new SortedList<DateTime, FileInfo>();

                // Get all video files for this service
                string[] files = Directory.GetFiles(serviceDir, "*.mpg");
                foreach (string file in files)
                {
                    // And add data about this file to the list for this service
                    FileInfo fileInfo = new FileInfo(file);

                    // Don't add file if it hasn't been finalized yet (indicated
                    // by a file end time in the past)
                    if (fileInfo.FileEndTime > fileInfo.FileStartTime)
                    {
                        serviceList.Add(fileInfo.FileStartTime, fileInfo);
                    }
                }

                // Finally add our list of files/info for this service to the master dictionary
                services.Add(serviceName, serviceList);
            }
        }

        /// <summary>
        /// Returns a list of service strings indicating the services we have some video for.
        /// </summary>
        public List<string> GetRecordedServices()
        {
            List<string> result = new List<string>();

            lock (services)
            {
                // Enumerate the services we know about
                foreach (string serviceName in services.Keys)
                {
                    // Now see if there are any files actually present
                    SortedList<DateTime, FileInfo> files = services[serviceName];

                    if (files.Count > 0)
                    {
                        result.Add(serviceName);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Returns a list of all contiguous spans of time that are recorded for
        /// the given service.
        /// </summary>
        /// <param name="serviceName">The service to check</param>
        /// <returns>List of regions containing start and end time for the span</returns>
        public List<RecordedRegion> GetRecordedRegions(string serviceName)
        {
            SortedList<DateTime, FileInfo> recordingList;

            // Get the list of files for the service. If the service isn't one
            // we know about then bail.
            lock (services)
            {
                if (services.TryGetValue(serviceName, out recordingList) == false)
                {
                    return null;
                }
            }

            // Now find all contiguous regions of video for this service
            List<RecordedRegion> result = new List<RecordedRegion>();
            RecordedRegion current = null;

            // Epsilon of gap below which we assume that subsequent files are contiguous.
            // In practice the time gaps tend to be in the tens of milliseconds. Note
            // that these gaps are the time of file creation, not gaps in the MPEG
            // timeline. The Receiver has enough buffer to maintain a seamless transition
            // between files even if something prevents us from creating a file for a 
            // few seconds.
            TimeSpan epsilon = TimeSpan.FromSeconds(5);

            // Walk the list of files for the service in time order
            foreach (FileInfo recording in recordingList.Values)
            {
                // Handle case where this is the first time in the loop
                if (current == null)
                {
                    current = new RecordedRegion();
                    current.StartTime = recording.FileStartTime;
                    current.EndTime = recording.FileEndTime;
                }
                else
                {
                    // Calculate gap between this file's start and the end of the current
                    // region. If it's below the epsilon threshold, then extend the
                    // current region.
                    TimeSpan gap = recording.FileStartTime - current.EndTime;
                    if (gap < epsilon)
                    {
                        current.EndTime = recording.FileEndTime;
                    }
                    else
                    {
                        // Found a gap. Close out the current region and start a new one.
                        result.Add(current);
                        current = new RecordedRegion();
                        current.StartTime = recording.FileStartTime;
                        current.EndTime = recording.FileEndTime;
                    }
                }
            }

            // Add the in-progress region, if one
            if (current != null)
            {
                result.Add(current);
            }

            return result;
        }

        /// <summary>
        /// A Program is a class that refers to an instance of a program that has aired on a
        /// service. It describes the service the program airs on and the time span during
        /// which it aired, along with descriptive metadata. Given one of these, this method
        /// accumulates all the video segments necessary to completely contain the program.
        /// <para>
        /// Note that because there are no guarantees about the alignment of the video file
        /// slices (and we can only accumulate at the granularity of a full file) it's 
        /// pretty much a guarantee that there will be excess video at the start and end
        /// that extends beyond the program content. It turns out that this is desirable
        /// for commercial detection and finding the true start time for a program. The
        /// commercial detection does a better job with more lead-in content and the programs
        /// frequently start either a bit sooner or a bit later than the scheduled time and
        /// we can more accurately find this during detection.
        /// </para>
        /// </summary>
        /// <param name="program"></param>
        /// <returns>
        /// A pathname that can be used to fetch the recording containing the program.
        /// </returns>
        public void GetRecordingForProgram(Program program, FileStream output)
        {
            // Get the list of recorded regions for the service this program aired on
            List<RecordedRegion> regions = GetRecordedRegions(program.Service);
            if (regions == null)
            {
                // If we don't have any regions, then you're outta luck
                return;
            }

            // Now figure out if this particular airing occured in one of our available regions
            bool haveProgram = false;
            foreach (RecordedRegion region in regions)
            {
                if (region.ContainsProgram(program))
                {
                    haveProgram = true;
                    break;
                }
            }

            // Do we have it recorded?
            if (false == haveProgram)
            {
                return;
            }

            // We got it. Now combine the data into a single file
            SortedList<DateTime, FileInfo> recordingList;
            lock (services)
            {
                if (services.TryGetValue(program.Service, out recordingList) == false)
                {
                    // "Shouldn't happen"
                    return;
                }
            }
          
            // Now assemble the actual file.
            int chunkSize = 4 * 1024 * 1024;    // Not sure this needs to be so big
            byte[] buffer = new byte[chunkSize];
            foreach (FileInfo fileInfo in recordingList.Values)
            {
                if (fileInfo.IntersectsProgram(program))
                {
                    System.Console.WriteLine("   Copying fragment from {0} to {1}", fileInfo.FileStartTime.ToLongTimeString(), fileInfo.FileEndTime.ToLongTimeString());
                    FileStream inputFile = new FileStream(fileInfo.Pathname, FileMode.Open, FileAccess.Read);
                    long bytesLeft = inputFile.Length;

                    while (bytesLeft > 0)
                    {
                        int bytesRead = inputFile.Read(buffer, 0, chunkSize);
                        output.Write(buffer, 0, bytesRead);

                        bytesLeft -= bytesRead;
                    }

                    inputFile.Close();
                }
            }

            output.Close();
        }

        public string GetRecordingForProgram(Program program)
        {
            // Handle the case where this is the first time we've ever
            // created a recording and the directory doesn't yet exist.
            if (false == Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            // Determine the time boundary of the resulting recording
            SortedList<DateTime, FileInfo> recordingList;
            lock (services)
            {
                if (services.TryGetValue(program.Service, out recordingList) == false)
                {
                    // "Shouldn't happen"
                    return null;
                }
            }
            DateTime recordingStartTime = new DateTime(2099, 12, 31);
            DateTime recordingEndTime = new DateTime(1999, 12, 31);
            foreach (FileInfo fileInfo in recordingList.Values)
            {
                if (fileInfo.IntersectsProgram(program))
                {
                    if (fileInfo.FileStartTime < recordingStartTime)
                    {
                        recordingStartTime = fileInfo.FileStartTime;
                    }
                    if (fileInfo.FileEndTime > recordingEndTime)
                    {
                        recordingEndTime = fileInfo.FileEndTime;
                    }
                }
            }

            // I pretty much hate this part. It's a hack to store metadata in the filename.
            // We encode the service, start/end time of the program and the start/end time
            // of the file in the name. This started out so innocently but every so often
            // I need more metadata and it gets jammed in here. Really we need a way to
            // store some parallel metadata with the video in a separate file.
            // TODO: !!FIXME
            string programTitle = program.Title;
            programTitle = programTitle.Replace(':', '-');
            programTitle = programTitle.Replace('\\', '-');
            programTitle = programTitle.Replace('/', '-');
            programTitle = programTitle.Replace('*', '-');
            programTitle = programTitle.Replace('?', '-');
            programTitle = programTitle.Replace('<', '-');
            programTitle = programTitle.Replace('>', '-');
            string filename = outputDirectory + "\\" + program.Service + "-" + programTitle +
                "-P-" + program.StartTime.ToFileTime().ToString() + "+" + program.EndTime.ToFileTime().ToString() +
                "-F-" + recordingStartTime.ToFileTime().ToString() + "+" + recordingEndTime.ToFileTime().ToString() +
                ".mpg";
            if (File.Exists(filename))
            {
                // Already did this?
                return filename;
            }

            FileStream fs = new FileStream(filename, FileMode.Create);
            GetRecordingForProgram(program, fs);

            return filename;
        }

        public void CullData(string service, DateTime priorTo)
        {
            // Get the list of recording slices for the service
            SortedList<DateTime, FileInfo> recordingList;
            lock (services)
            {
                if (services.TryGetValue(service, out recordingList) == false)
                {
                    // We don't know about that service -- can't cull anything
                    return;
                }
            }

            // Walk all recording slices and delete any that end prior to the
            // given time
            FileInfo[] allFiles = recordingList.Values.ToArray();
            foreach (FileInfo fileInfo in allFiles)
            {
                if (fileInfo.FileEndTime < priorTo)
                {
                    System.Console.WriteLine("--> Deleting {0}-{1}: {2}",
                        fileInfo.FileStartTime, fileInfo.FileEndTime, Path.GetFileName(fileInfo.Pathname));

                    File.Delete(fileInfo.Pathname);
                    lock (services)
                    {
                        if (recordingList[fileInfo.FileStartTime] != fileInfo)
                        {
                            throw new ApplicationException("Logic error somewhere -- expected only one file per start time");
                        }
                        recordingList.Remove(fileInfo.FileStartTime);
                    }
                }
            }

            // As a final step delete any unfinalized recording slices
            string dir = Path.Combine(servicesDirectory, service);
            foreach (string path in Directory.GetFiles(dir, "*.mpg"))
            {
                FileInfo info = new FileInfo(path);
                if (info.FileEndTime < info.FileStartTime)
                {
                    // Inverted duration means we never finalized the recording to
                    // update the end time. Kill it if it's been around for a while
                    if (DateTime.Now - info.FileStartTime > TimeSpan.FromHours(3))
                    {
                        System.Console.WriteLine("--> Deleting partial recording {0}", Path.GetFileName(path));
                        File.Delete(path);
                    }
                }
            }

        }

        public void CullData(Program program)
        {
            // Get the list of recording slices for the service this program aired on
            SortedList<DateTime, FileInfo> recordingList;
            lock (services)
            {
                if (services.TryGetValue(program.Service, out recordingList) == false)
                {
                    // We don't know about that service -- can't cull anything
                    return;
                }
            }

            // Walk all recording slices and delete any that are wholly contained
            // within the program. Slices that intersect the program, but extend
            // outside its bounds cannot be culled as they may contain video
            // needed for other programs.
            FileInfo[] allFiles = recordingList.Values.ToArray();
            foreach (FileInfo fileInfo in allFiles)
            {
                if (fileInfo.ContainedWithinProgram(program))
                {
                    System.Console.WriteLine("--> Deleting {0}-{1}: {2}",
                        fileInfo.FileStartTime, fileInfo.FileEndTime, Path.GetFileName(fileInfo.Pathname));

                    File.Delete(fileInfo.Pathname);
                    lock (services)
                    {
                        if (recordingList[fileInfo.FileStartTime] != fileInfo)
                        {
                            throw new ApplicationException("Logic error somewhere -- expected only one file per start time");
                        }
                        recordingList.Remove(fileInfo.FileStartTime);
                    }
                }
            }
        }

        /// <summary>
        /// Get a FileStream that can be used to store video data for a given service. This
        /// abstracts away the directory structure and filename scheme from clients of the
        /// VideoStorage class.
        /// </summary>
        /// <remarks>
        /// In order to finalize the recording and assure the metadata is consistent, the
        /// client must call VideoStorage.ReleaseStream() when they are finished writing
        /// to it. Otherwise, we assume a failure during recording and do not use that
        /// file.
        /// </remarks>
        /// <param name="serviceName">Service that is being recordd</param>
        /// <returns>Filestream that the client can write to. VideoStorage.ReleaseStream
        /// <i>must</i> be called on this stream when the client is done.</returns>
        public FileStream GetStreamForService(string serviceName)
        {
            string serviceDirectory = Path.Combine(servicesDirectory, serviceName);

            // If this is the first slice being recorded for this service, it's directory
            // might not yet exist.
            if (false == Directory.Exists(serviceDirectory))
            {
                Directory.CreateDirectory(serviceDirectory);
            }

            // Now construct a pathname for the file based on the start time of the recording
            // which is assumed to be Now.
            DateTime recordingStartTime = DateTime.Now;
            DateTime recordingEndTime = new DateTime(1999, 12, 31); // End time is unknown at the moment
            string filename = serviceDirectory + "\\" + serviceName +
                "-F-" + recordingStartTime.ToFileTime().ToString() + "+" + recordingEndTime.ToFileTime().ToString() +
                ".mpg";

            // Create the file and return the stream. Note that we don't add any tracking 
            // metadata to our internal data structures about this file yet. Only when 
            // it's complete does it get added.
            return new FileStream(filename, FileMode.CreateNew, FileAccess.Write);
        }

        public void ReleaseStream(FileStream stream)
        {
            string originalName = stream.Name;
            FileInfo info = new FileInfo(stream.Name);
            string serviceName = info.Service;

            // Close/Flush the stream
            stream.Close();

            // Now that the file is complete we know the end time so we rename the
            // file with the new name indicating both start and end times for the
            // recording slice.
            DateTime recordingStartTime = info.FileStartTime;
            DateTime recordingEndTime = DateTime.Now;

            // Yay -- more magic crap to keep in sync
            string serviceDirectory = Path.Combine(servicesDirectory, serviceName);
            string newName = serviceDirectory + "\\" + serviceName +
                "-F-" + recordingStartTime.ToFileTime().ToString() + "+" + recordingEndTime.ToFileTime().ToString() +
                ".mpg";

            // Rename the file
            File.Move(originalName, newName);
            info = new FileInfo(newName);

            // Now add the file to the list of files for the service
            SortedList<DateTime, FileInfo> recordingList;
            lock (services)
            {
                if (services.TryGetValue(serviceName, out recordingList) == false)
                {
                    // First file for this service.
                    recordingList = new SortedList<DateTime, FileInfo>();
                    services[serviceName] = recordingList;
                }

                recordingList.Add(recordingStartTime, info);
            }
        }

    }

}
