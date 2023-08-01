using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HdHomerunLib.Listings;

namespace HdHomerunLib.Seeker
{
    public interface IRankProgram
    {
        int RankProgram(Program prog);
        string GetName();
    }

    public class Ranking
    {

        string[] SkipExactTitles = 
        {
            "Paid Programming",
            "Celebrity Ab Secrets",
            "Internet Millions",
            "Your Baby Can Read",
            "Select Comfort",
            "Is Colon Detox Hype?",
            "Bringing Wall Street to Main Street",
            "Cook Healthy with the NuWave Infrared Oven Pro",
            "Today"
        };

        string[] SkipContainedPhrases =
        {
            "Paid Program",
            "News"
        };

        string[] SkipGenres = 
        {
            "Shopping",
            "Religious",
            "Fundraiser",
            "News",
            "Consumer"
        };

        public bool IgnoreProgram(Program program)
        {
            StringComparison compareType = StringComparison.CurrentCultureIgnoreCase;

            foreach (string exact in SkipExactTitles)
            {
                if (exact.Equals(program.Title, compareType))
                {
                    return true;
                }
            }

            foreach (string phrase in SkipContainedPhrases)
            {
                if (program.Title.ToLowerInvariant().Contains(phrase.ToLowerInvariant()))
                {
                    return true;
                }
            }

            foreach (string genre in SkipGenres)
            {
                if (program.Genres.Contains(genre))
                {
                    return true;
                }
            }

            return false;
        }
        public int RankProgram(Program program)
        {
            return 0;
        }
    }
}
