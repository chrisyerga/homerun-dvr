# homerun-dvr

DVR running on a PC that uses an HDHomeRun device to capture QAM modulated MPEG2 transport streams and extract programs and store them. Integrated with TV guide listings, commercial removal and transcoding.

# Components

### Receiver

> Uses the HDHomeRun device which takes a cable TV signal and tuning/filtering parameters to pull a raw MPEG2 transport stream from a given QAM256 channel from your cable provider.

> What channels are recorded is controlled by the scheduler informed by TV listings and the interests
> expressed in the source code parameters. There's no UI to speak of. I have a web app that shows the recording
> and transcoding pipeline, etc. but it's more for monitoring the system.

> With dozens of MPEG transport streams and 100s of programs, the receiver is almost always recording *something*. It records things opportunistically and then prioritizes transcoding and making them available against a queue full of raw recordings. 

### MPEG TS Parser

> This parser is capable of grabbing a raw MPEG2 transport stream and demuxing it to pull out a given video/audio stream as well as program information.

> It parses the PAT and PMT tables to find the elementary streams contained within and can dump those to an abstract VideoStorage which in this implementation stores to a NAT on the local network.

> It also parses program information from EIT and PSIP tables, however most of the program information is acquired out-of-band from a TV Listings API service.

### Listings

> Uses the (now defunct :/) Schedules Direct API to
> gather EPG listings for the given provider/zip code.

### Orchestration / JobQueue

> The system can utilize many different computers talking to multiple QAM tuners. There can also be multiple transcoders etc. The code uses both AWS and GCP (nee Azure) to maintain a central Job Queue for recording/transcoding etc.
