using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Amazon.SimpleDB;
using Amazon.SimpleDB.Model;
using HdHomerunLib.Listings;


namespace HdHomerunLib
{
    public class UserAction
    {
        /// <summary>
        /// Parameterless constructor used by various magical an horrific ASP.NET thingies
        /// </summary>
        private UserAction()
        {
        }

        //======================================================
        //  Public Property Accessors
        //======================================================

        public string User
        {
            get { return user; }
        }

        public string Action
        {
            get { return action; }
        }

        public string Title
        {
            get { return title; }
        }

        public string EpisodeTitle
        {
            get { return episodeTitle; }
        }

        public DateTime LastUpdate
        {
            get { return lastUpdate.ToLocalTime(); }
        }

        public static UserAction AddUserAction(string user, string action, string title, string episode)
        {
            UserAction result = new UserAction();
            result.id = Guid.NewGuid();

            result.user = user;
            result.action = action;
            result.title = title;
            result.episodeTitle = episode;

            PutAttributesRequest req = new PutAttributesRequest();

            req.DomainName = domain;
            req.ItemName = result.id.ToString();
            req.Attribute.Add(new ReplaceableAttribute() { Name = attribute_action, Value = result.action, Replace = true });
            req.Attribute.Add(new ReplaceableAttribute() { Name = attribute_user, Value = result.user, Replace = true });
            req.Attribute.Add(new ReplaceableAttribute() { Name = attribute_title, Value = result.title, Replace = true });
            req.Attribute.Add(new ReplaceableAttribute() { Name = attribute_episode, Value = result.episodeTitle, Replace = true });
            AddLastUpdatedAttribute(req.Attribute);
            client.PutAttributes(req);

            return result;
        }

        private static void AddLastUpdatedAttribute(List<ReplaceableAttribute> attributes)
        {
            string datestring = DateTime.UtcNow.ToString();
            attributes.Add(new ReplaceableAttribute() { Name = attribute_job_lastupdate, Value = datestring, Replace = true });
        }

        /// <summary>
        /// Internal helper that constructs an empty <typeparamref name="QueryWithAttributesRequest"/>
        /// instance that is fully constructed with all appropriate attributes etc. This way the client
        /// only needs to set the QueryExpression and not risk missing some attributes, etc.
        /// </summary>
        /// <returns>QueryWithAttributesRequest needing only QueryExpression property set</returns>
        private static QueryWithAttributesRequest GetEmptyQuery()
        {
            var req = new QueryWithAttributesRequest
            {
                MaxNumberOfItems = 1,
                DomainName = domain
            };

            req.AttributeName.Add(attribute_action);
            req.AttributeName.Add(attribute_title);
            req.AttributeName.Add(attribute_episode);
            req.AttributeName.Add(attribute_user);
            req.AttributeName.Add(attribute_job_lastupdate);

            return req;
        }

        private static string EscapeQueryString(string queryString)
        {
            queryString = queryString.Replace(@"\", @"\\");
            return queryString.Replace("'", "\\'");
        }

        public static void DeleteUserAction(Guid Id)
        {
            var req = new DeleteAttributesRequest
            {
                DomainName = domain,
                ItemName = Id.ToString()
            };

            client.DeleteAttributes(req);
        }

        public static List<UserAction> GetAllUserActions()
        {
            var req = GetEmptyQuery();

            req.MaxNumberOfItems = 250;
            req.QueryExpression = "['lastupdate' != 'X'] sort 'lastupdate' desc";

            List<UserAction> results = new List<UserAction>();
            var resp = client.QueryWithAttributes(req);

            foreach (Item item in resp.QueryWithAttributesResult.Item)
            {
                UserAction result = ConstructFromItem(item);
                results.Add(result);
            }

            return results;
        }

        public static List<UserAction> GetAllActionsForUser(string username)
        {
            var req = GetEmptyQuery();

            req.MaxNumberOfItems = 250;
            req.QueryExpression = string.Format("['user' = '{0}'] intersection ['lastupdate' != 'X'] sort 'lastupdate' desc", username);

            List<UserAction> results = new List<UserAction>();
            var resp = client.QueryWithAttributes(req);

            foreach (Item item in resp.QueryWithAttributesResult.Item)
            {
                UserAction result = ConstructFromItem(item);
                results.Add(result);
            }

            return results;
        }

        public static List<UserAction> GetAllActionsForTitle(string title)
        {
            var req = GetEmptyQuery();

            req.MaxNumberOfItems = 250;
            req.QueryExpression = string.Format("['title' = '{0}'] intersection ['lastupdate' != 'X'] sort 'lastupdate' desc", title);

            List<UserAction> results = new List<UserAction>();
            var resp = client.QueryWithAttributes(req);

            foreach (Item item in resp.QueryWithAttributesResult.Item)
            {
                UserAction result = ConstructFromItem(item);
                results.Add(result);
            }

            return results;
        }

        public static List<UserAction> GetAllActionsForEpisode(string episode)
        {
            var req = GetEmptyQuery();

            req.MaxNumberOfItems = 250;
            req.QueryExpression = string.Format("['episode' = '{0}'] intersection ['lastupdate' != 'X'] sort 'lastupdate' desc", episode);

            List<UserAction> results = new List<UserAction>();
            var resp = client.QueryWithAttributes(req);

            foreach (Item item in resp.QueryWithAttributesResult.Item)
            {
                UserAction result = ConstructFromItem(item);
                results.Add(result);
            }

            return results;
        }

        private static UserAction ConstructFromItem(Item item)
        {
            UserAction result = new UserAction()
            {
                id = new Guid(item.Name),
                action = GetItemAttribute(item, attribute_action),
                user = GetItemAttribute(item, attribute_user),
                title = GetItemAttribute(item, attribute_title),
                episodeTitle = GetItemAttribute(item, attribute_episode)
            };

            try
            {
                result.lastUpdate = DateTime.SpecifyKind(DateTime.Parse(GetItemAttribute(item, attribute_job_lastupdate)), DateTimeKind.Utc);
            }
            catch (Exception)
            {
                result.lastUpdate = new DateTime(1999, 1, 1);
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


        public string ToString()
        {
            return string.Format("{0} - {1} on {2}", user, action, title);
        }

        string user;
        string action;
        string title;
        string episodeTitle;
        Guid id;
        DateTime lastUpdate;
    
        // =========================================================
        //  Static Items 
        // =========================================================
        private static AmazonSimpleDBClient client = new AmazonSimpleDBClient(HdHomerunLib.JobQueue.AwsConfig.aws_id, HdHomerunLib.JobQueue.AwsConfig.aws_secret);

        private static readonly string domain = "user-actions";
        private static readonly string attribute_user = "user";
        private static readonly string attribute_action = "action";
        private static readonly string attribute_title = "title";
        private static readonly string attribute_episode = "episode";
        private static readonly string attribute_job_lastupdate = "lastupdate";
    }
}
