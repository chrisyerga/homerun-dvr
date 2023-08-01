using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HdHomerunLib.JobQueue;
using HdHomerunLib.Listings;
using Amazon.SimpleDB;
using Amazon.SimpleDB.Model;

namespace HdHomerunLib.Seeker
{
    public enum RankingCriteria
    {
        TitleMatches,
        TitleContains,
        PrimaryGenreIs,
        AnyGenreIs,
        HasActor
    }

    public enum RankingAction
    {
        SetPriority,
        IncreasePriority,
        DecreasePriority
    }

    public class RankingRule
    {
        public RankingRule(RankingCriteria criteria, string criteriaArgument, RankingAction action, int actionArgument)
        {
            Criteria = criteria;
            CriteriaArgument = criteriaArgument;
            Action = action;
            ActionArgument = actionArgument;
        }

        public RankingRule()
        {
        }

        public bool MeetsCriteria(Program program)
        {
            bool criteriaMet = false;

            // First see if the criteria for this rule has been met by the program
            switch (Criteria)
            {
                case RankingCriteria.TitleMatches:
                    if (program.Title.ToLowerInvariant() == CriteriaArgument.ToLowerInvariant())
                    {
                        criteriaMet = true;
                    }
                    break;

                case RankingCriteria.TitleContains:
                    if (program.Title.ToLowerInvariant().Contains(CriteriaArgument.ToLowerInvariant()))
                    {
                        criteriaMet = true;
                    }
                    break;

                case RankingCriteria.PrimaryGenreIs:
                    if (program.Genres.Length > 0 && (program.Genres[0].ToLowerInvariant() == CriteriaArgument.ToLowerInvariant()))
                    {
                        criteriaMet = true;
                    }
                    break;

                case RankingCriteria.AnyGenreIs:
                    foreach (string genre in program.Genres)
                    {
                        if (genre.ToLowerInvariant() == CriteriaArgument.ToLowerInvariant())
                        {
                            criteriaMet = true;
                            break;
                        }
                    }
                    break;

                case RankingCriteria.HasActor:
                    foreach (string actor in program.Actors)
                    {
                        if (actor.ToLowerInvariant() == CriteriaArgument.ToLowerInvariant())
                        {
                            criteriaMet = true;
                            break;
                        }
                    }
                    break;
            }

            return criteriaMet;
        }

        public bool Apply(Program program, ref int priority)
        {
            bool stopRules = false;

            // If the criteria was met, then apply the action
            if (MeetsCriteria(program))
            {
                switch (Action)
                {
                    case RankingAction.DecreasePriority:
                        priority -= ActionArgument;
                        break;

                    case RankingAction.IncreasePriority:
                        priority += ActionArgument;
                        break;

                    case RankingAction.SetPriority:
                        priority = ActionArgument;
                        stopRules = true;
                        break;
                }
            }

            return stopRules;
        }

        public RankingCriteria Criteria { get; set; }
        public string CriteriaArgument { get; set; }
        public RankingAction Action { get; set; }
        public int ActionArgument { get; set; }
        public Guid Id { get; set; }

        //=====================================================================
        // Data Access Methods for ASP.Net
        //=====================================================================

        public static List<RankingRule> GetAllRules()
        {
            return RankingRuleStore.GetAllRules();
        }

        public static void AddRule(RankingRule rule)
        {
            RankingRuleStore.AddRule(rule);
        }
    }

    public class RankingRuleStore
    {
        static object cacheLock = new object();

        public static void AddRule(RankingRule rule)
        {
            PutAttributesRequest req = new PutAttributesRequest();

            req.DomainName = domain;
            rule.Id = Guid.NewGuid();
            req.ItemName = rule.Id.ToString();
            req.Attribute.Add(new ReplaceableAttribute() { Name = attribute_criteria, Value = rule.Criteria.ToString(), Replace = true });
            req.Attribute.Add(new ReplaceableAttribute() { Name = attribute_criteriaArgument, Value = rule.CriteriaArgument, Replace = true });
            req.Attribute.Add(new ReplaceableAttribute() { Name = attribute_action, Value = rule.Action.ToString(), Replace = true });
            req.Attribute.Add(new ReplaceableAttribute() { Name = attribute_actionArgument, Value = string.Format("{0:D4}", rule.ActionArgument), Replace = true });
            client.PutAttributes(req);

            lock (cacheLock)
            {
                allRules.Add(rule);
            }
        }

        private static AmazonSimpleDBClient client = new AmazonSimpleDBClient(AwsConfig.aws_id, AwsConfig.aws_secret);

        public static List<RankingRule> GetRulesForProgram(Program program)
        {
            List<RankingRule> result = new List<RankingRule>();

            foreach (RankingRule candidateRule in GetAllRules())
            {
                if (candidateRule.MeetsCriteria(program))
                {
                    result.Add(candidateRule);
                }
            }

            return result;
        }

        public static List<RankingRule> GetRulesForGenre(string genre)
        {
            List<RankingRule> result = new List<RankingRule>();

            foreach (RankingRule candidateRule in GetAllRules())
            {
                if (candidateRule.Criteria == RankingCriteria.PrimaryGenreIs ||
                    candidateRule.Criteria == RankingCriteria.AnyGenreIs)
                {
                    if (candidateRule.CriteriaArgument == genre)
                    {
                        result.Add(candidateRule);
                    }
                }
            }

            return result;
        }

        public static List<RankingRule> GetAllRules()
        {
            if (cacheValid == false)
            {
                string nextToken = null;

                // First clear out cache
                List<RankingRule> newCache = new List<RankingRule>();

                // Now iterate grabbing 200 rules at a time until we get them all
                do
                {
                    var req = new QueryWithAttributesRequest
                    {
                        MaxNumberOfItems = 200,
                        DomainName = domain
                    };

                    if (nextToken != null)
                    {
                        req.NextToken = nextToken;
                    }
                    else
                    {
                        nextToken = null;
                    }

                    req.AttributeName.Add(attribute_criteria);
                    req.AttributeName.Add(attribute_criteriaArgument);
                    req.AttributeName.Add(attribute_action);
                    req.AttributeName.Add(attribute_actionArgument);

                    var result = client.QueryWithAttributes(req);
                    if (result.QueryWithAttributesResult.IsSetNextToken())
                    {
                        nextToken = result.QueryWithAttributesResult.NextToken;
                    }

                    // Add the new batch of rules to the cache
                    foreach (Item item in result.QueryWithAttributesResult.Item)
                    {
                        RankingRule rule = ConstructFromItem(item);
                        newCache.Add(rule);
                    }
                }
                while (nextToken != null);

                lock (cacheLock)
                {
                    allRules = newCache;
                    cacheValid = true;
                }
            }

            // Always return a copy to maintain thread safety
            List<RankingRule> output;
            lock (cacheLock)
            {
                output = new List<RankingRule>(allRules);
            }

            return output;
        }

        public static void DeleteRule(string Id)
        {
            DeleteAttributesRequest req = new DeleteAttributesRequest()
            {
                DomainName = domain,
                ItemName = Id
            };

            client.DeleteAttributes(req);

            cacheValid = false;
        }

        private static RankingRule ConstructFromItem(Item item)
        {
            RankingRule result = new RankingRule()
            {
                Id = new Guid(item.Name),
                Action = (RankingAction)Enum.Parse(typeof(RankingAction), GetItemAttribute(item, attribute_action)),
                CriteriaArgument = GetItemAttribute(item, attribute_criteriaArgument),
                Criteria = (RankingCriteria)Enum.Parse(typeof(RankingCriteria), GetItemAttribute(item, attribute_criteria))
            };

            if (GetItemAttribute(item, attribute_actionArgument) != null)
            {
                result.ActionArgument = int.Parse(GetItemAttribute(item, attribute_actionArgument));
            }

            return result;
        }

        private static string GetItemAttribute(Item item, string attribute)
        {
            foreach (Amazon.SimpleDB.Model.Attribute attrib in item.Attribute)
            {
                if (attrib.Name == attribute)
                {
                    return attrib.Value;
                }
            }

            return null;
        }

        private static bool cacheValid = false;
        private static List<RankingRule> allRules = new List<RankingRule>();

        private static readonly string domain = "ranking-rules";
        private static readonly string attribute_criteria = "criteria";
        private static readonly string attribute_criteriaArgument = "criteriaArgument";
        private static readonly string attribute_action = "action";
        private static readonly string attribute_actionArgument = "actionArgument";
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

        public static int RankProgram(Program program)
        {
            // Start at medium priority
            int priority = 50;

            foreach (RankingRule rule in RankingRuleStore.GetAllRules())
            {
                if (rule.Apply(program, ref priority))
                {
                    // True means we should stop running rules
                    break;
                }
            }

            return priority;
        }
    }
}
