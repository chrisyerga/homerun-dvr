using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Amazon.SimpleDB;
using Amazon.SimpleDB.Model;
using HdHomerunLib.Listings;

namespace HdHomerunLib.JobQueue
{
    public class Job
    {
        /// <summary>
        /// Parameterless constructor used by various magical an horrific ASP.NET thingies
        /// </summary>
        private Job()
        {
        }

        public void AcknowledgeReceipt()
        {
            PutAttributesRequest req = new PutAttributesRequest();
            req.DomainName = job_queue_table;
            req.ItemName = id.ToString();
            req.Attribute.Add( new ReplaceableAttribute() { Name = attribute_job_handledby, Value = System.Environment.MachineName, Replace = true } );
            AddLastUpdatedAttribute(req.Attribute);

            client.PutAttributes(req);
        }


        //======================================================
        //  Public Property Accessors
        //======================================================

        public string Action
        {
            get { return action; }
        }

        public string Path
        {
            get { return path; }
        }

        public int Priority
        {
            get { return priority; }
        }

        public Guid Id
        {
            get { return id; }
        }

        public double Progress
        {
            get { return progress; }
        }

        public string Title
        {
            get { return title; }
        }

        public string EpisodeTitle
        {
            get { return episodeTitle; }
        }

        public string Description
        {
            get { return description; }
        }

        public string HandledBy
        {
            get { return handledBy; }
        }

        public string Phase
        {
            get { return phase; }
        }

        public string Status
        {
            get { return status; }
        }

        public DateTime LastUpdate
        {
            get { return lastUpdate.ToLocalTime(); }
        }

        public string Notes
        {
            get { return notes; }
        }

        public string CloudUri1000
        {
            get { return cloudUri1000; }
        }

        public string CloudUri250
        {
            get { return cloudUri250; }
        }

        //==============================================================
        //  UpdateXXX Methods -- update DB as well as internal state
        //==============================================================

        public void UpdatePhase(string phase)
        {
            UpdateAttribute(attribute_job_phase, phase);
            this.phase = phase;
        }

        public void UpdateAction(string action)
        {
            UpdateAttribute(attribute_job_action, action);
            this.action = action;
        }

        public void UpdateProgress(double progress)
        {
            if (progress < 0.0 || progress > 100.0)
            {
                throw new ArgumentOutOfRangeException("progress", "Must be between 0..100");
            }

            string value = string.Format("{0:F1}", progress);
            UpdateAttribute(attribute_job_progress, value);
            this.progress = progress;
        }

        public void UpdatePriority(int priority)
        {
            if (priority < 0 || priority > 100)
            {
                throw new ArgumentOutOfRangeException("priority", "Must be between 0..100");
            }

            string value = string.Format("{0:D3}", priority);
            UpdateAttribute(attribute_job_priority, value);
            this.priority = priority;
        }

        public void UpdateStatus(string status)
        {
            UpdateAttribute(attribute_job_status, status);
            this.status = status;
        }

        public void UpdateHandledBy(string handledBy)
        {
            UpdateAttribute(attribute_job_handledby, handledBy);
            this.handledBy = handledBy;
        }

        public void UpdateNotes(string notes)
        {
            UpdateAttribute(attribute_job_notes, notes);
            this.notes = notes;
        }

        private void UpdateAttribute(string name, string value)
        {
            PutAttributesRequest req = new PutAttributesRequest();
            req.DomainName = job_queue_table;
            req.ItemName = id.ToString();
            req.Attribute.Add(new ReplaceableAttribute() { Name = name, Value = value, Replace = true });
            AddLastUpdatedAttribute(req.Attribute);

            client.PutAttributes(req);
        }


        public static Job AddEncodeJob(string Path, Program program, int Priority)
        {
            Job result = new Job();
            result.action = action_encode;
            result.path = Path;
            result.id = Guid.NewGuid();
            result.priority = Priority;
            result.title = program.Title;
            result.episodeTitle = program.EpisodeTitle;
            result.description = program.Description;

            PutAttributesRequest req = new PutAttributesRequest();

            req.DomainName = job_queue_table;
            req.ItemName = result.id.ToString();
            req.Attribute.Add(new ReplaceableAttribute() { Name = attribute_job_action, Value = result.action, Replace = true });
            req.Attribute.Add(new ReplaceableAttribute() { Name = attribute_job_path, Value = result.path, Replace = true });
            req.Attribute.Add(new ReplaceableAttribute() { Name = attribute_job_priority, Value = string.Format("{0:D3}", result.priority), Replace = true });
            req.Attribute.Add(new ReplaceableAttribute() { Name = attribute_job_handledby, Value = "unassigned", Replace = true });
            req.Attribute.Add(new ReplaceableAttribute() { Name = attribute_job_programtitle, Value = program.Title, Replace = true });
            if (program.EpisodeTitle != null)
            {
                req.Attribute.Add(new ReplaceableAttribute() { Name = attribute_job_episodetitle, Value = program.EpisodeTitle, Replace = true });
            }
            if (program.Description != null)
            {
                req.Attribute.Add(new ReplaceableAttribute() { Name = attribute_job_programdescription, Value = program.Description, Replace = true });
            }
            AddLastUpdatedAttribute(req.Attribute);
            client.PutAttributes(req);

            return result;
        }

        private static void AddLastUpdatedAttribute(List<ReplaceableAttribute> attributes)
        {
            string datestring = DateTime.UtcNow.ToString();
            attributes.Add(new ReplaceableAttribute() { Name = attribute_job_lastupdate, Value = datestring, Replace = true });
        }

        public static int GetJobQueueLength()
        {
            QueryRequest req = new QueryRequest();
            QueryResponse resp;

            req.DomainName = job_queue_table;
            resp = client.Query(req);

            return resp.QueryResult.ItemName.Count();
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
                DomainName = job_queue_table
            };

            req.AttributeName.Add(attribute_job_action);
            req.AttributeName.Add(attribute_job_path);
            req.AttributeName.Add(attribute_job_priority);
            req.AttributeName.Add(attribute_job_handledby);
            req.AttributeName.Add(attribute_job_notes);
            req.AttributeName.Add(attribute_job_phase);
            req.AttributeName.Add(attribute_job_progress);
            req.AttributeName.Add(attribute_job_status);
            req.AttributeName.Add(attribute_job_programtitle);
            req.AttributeName.Add(attribute_job_programdescription);
            req.AttributeName.Add(attribute_job_episodetitle);
            req.AttributeName.Add(attribute_job_lastupdate);
            req.AttributeName.Add(attribute_job_cloudUri1000);
            req.AttributeName.Add(attribute_job_cloudUri250);

            return req;
        }

        /// <summary>
        /// Returns an encode job that is pending and next in priority order
        /// </summary>
        /// <returns></returns>
        public static Job GetEncodeJob()
        {
            var req = GetEmptyQuery();
            Job result = null;

            req.QueryExpression = string.Format(
                    "['action' = 'encode'] intersection['priority' < '101'] intersection ['handled_by' = 'unassigned'] sort 'priority'");

            var resp = client.QueryWithAttributes(req);
            if (resp.QueryWithAttributesResult.Item.Count > 0)
            {
                Item item = resp.QueryWithAttributesResult.Item[0];
                result = ConstructFromItem(item);
            }

            return result;
        }

        private static string EscapeQueryString(string queryString)
        {
            queryString = queryString.Replace(@"\", @"\\");
            return queryString.Replace("'", "\\'");
        }

        public static void DeleteJob(Guid Id)
        {
            var req = new DeleteAttributesRequest
            {
                DomainName = job_queue_table,
                ItemName = Id.ToString()
            };

            client.DeleteAttributes(req);
        }

        public static void DeleteJob(Job job)
        {
            var req = new DeleteAttributesRequest
            {
                DomainName = job_queue_table,
                ItemName = job.Id.ToString()
            };

            client.DeleteAttributes(req);
        }

        public static Job GetJobById(Guid Id)
        {
            var req = new GetAttributesRequest
            {
                DomainName = job_queue_table,
                ItemName = Id.ToString()
            };
           
            req.AttributeName.Add(attribute_job_action);
            req.AttributeName.Add(attribute_job_path);
            req.AttributeName.Add(attribute_job_priority);
            req.AttributeName.Add(attribute_job_handledby);
            req.AttributeName.Add(attribute_job_notes);
            req.AttributeName.Add(attribute_job_phase);
            req.AttributeName.Add(attribute_job_progress);
            req.AttributeName.Add(attribute_job_status);
            req.AttributeName.Add(attribute_job_programtitle);
            req.AttributeName.Add(attribute_job_programdescription);
            req.AttributeName.Add(attribute_job_episodetitle);
            req.AttributeName.Add(attribute_job_lastupdate);

            Job result = null;
            try
            {
                var resp = client.GetAttributes(req);

                Item item = new Item();
                item.Attribute = resp.GetAttributesResult.Attribute;
                item.Name = Id.ToString();
                result = ConstructFromItem(item);
            }
            catch (Exception e)
            {
            }

            return result;
        }

        public static Job GetJobByPath(string path)
        {
            var req = GetEmptyQuery();
            Job result = null;

            req.QueryExpression = string.Format(
                    "['action' = 'encode'] intersection ['path' = '{0}']", EscapeQueryString(path));

            var resp = client.QueryWithAttributes(req);
            if (resp.QueryWithAttributesResult.Item.Count > 0)
            {
                Item item = resp.QueryWithAttributesResult.Item[0];
                result = ConstructFromItem(item);
            }

            return result;
        }

        public static List<Job> GetAllFinishedJobs()
        {
            var req = GetEmptyQuery();

            req.MaxNumberOfItems = 250;
            req.QueryExpression = "['action' = 'done'] intersection ['lastupdate' != 'X'] sort 'lastupdate' desc";

            List<Job> jobs = new List<Job>();
            var resp = client.QueryWithAttributes(req);

            foreach (Item item in resp.QueryWithAttributesResult.Item)
            {
                Job result = ConstructFromItem(item);
                jobs.Add(result);
            }

            return jobs;
        }

        public static List<Job> GetAllCloudAvailableJobs()
        {
            var req = GetEmptyQuery();

            req.MaxNumberOfItems = 250;
            req.QueryExpression = "['action' = 'done'] intersection ['lastupdate' != 'X'] intersection ['cloudmedia-1000-uri' starts-with 'http'] sort 'lastupdate' desc";

            List<Job> jobs = new List<Job>();
            var resp = client.QueryWithAttributes(req);

            foreach (Item item in resp.QueryWithAttributesResult.Item)
            {
                Job result = ConstructFromItem(item);
                jobs.Add(result);
            }

            return jobs;
        }
        public static List<Job> GetAllAssignedJobs()
        {
            var req = GetEmptyQuery();

            req.MaxNumberOfItems = 250;
            req.QueryExpression = "['handled_by' != 'unassigned'] intersection ['action' != 'done'] intersection ['lastupdate' != 'X'] sort 'lastupdate' desc";

            List<Job> jobs = new List<Job>();
            var resp = client.QueryWithAttributes(req);

            foreach (Item item in resp.QueryWithAttributesResult.Item)
            {
                Job result = ConstructFromItem(item);

                if (DateTime.UtcNow - result.lastUpdate < TimeSpan.FromMinutes(30))
                {
                    jobs.Add(result);
                }
            }

            return jobs;
        }

        public static List<Job> GetAllFailedJobs()
        {
            var req = GetEmptyQuery();

            // Do this in two stages. First find any jobs that claim to be assigned an in-progress
            // but haven't had their status updated in over 30 minutes. These are abandoned jobs that
            // somehow failed without their owner detecting and marking them aborted.
            req.MaxNumberOfItems = 250;
            req.QueryExpression = "['action' = 'encode'] intersection['priority' < '101'] sort 'priority'";

            List<Job> jobs = new List<Job>();
            var resp = client.QueryWithAttributes(req);
            foreach (Item item in resp.QueryWithAttributesResult.Item)
            {
                Job result = ConstructFromItem(item);

                if (result.handledBy != "unassigned" && DateTime.UtcNow - result.lastUpdate > TimeSpan.FromMinutes(30))
                {
                    // Here's an orphaned job. Update it's action to indicate failure.
                    result.UpdateAction("abandoned");
                    result.UpdateHandledBy("unassigned");
                }
            }

            // Next find any jobs that are explicitly marked aborted.
            req = GetEmptyQuery();
            req.MaxNumberOfItems = 250;
            req.QueryExpression = "['action' = 'aborted'] union ['action' = 'abandoned']";

            resp = client.QueryWithAttributes(req);
            foreach (Item item in resp.QueryWithAttributesResult.Item)
            {
                Job result = ConstructFromItem(item);
                jobs.Add(result);
            }

            return jobs;
        }

        public static List<Job> GetAllPendingJobs()
        {
            var req = GetEmptyQuery();

            req.MaxNumberOfItems = 250;
            req.QueryExpression = "['action' = 'encode'] intersection['priority' < '101'] sort 'priority'";

            List<Job> jobs = new List<Job>();

            var resp = client.QueryWithAttributes(req);

            foreach (Item item in resp.QueryWithAttributesResult.Item)
            {
                Job result = ConstructFromItem(item);

                if (result.handledBy != "unassigned" && DateTime.UtcNow - result.lastUpdate > TimeSpan.FromMinutes(30))
                {
                    // Seems like an orphaned job, don't add it
                }
                else
                {
                    jobs.Add(result);
                }
            }

            return jobs;
        }

        private static Job ConstructFromItem(Item item)
        {
            Job result = new Job()
            {
                id = new Guid(item.Name),
                action = GetItemAttribute(item, attribute_job_action),
                path = GetItemAttribute(item, attribute_job_path),
                handledBy = GetItemAttribute(item, attribute_job_handledby),
                notes = GetItemAttribute(item, attribute_job_notes),
                phase = GetItemAttribute(item, attribute_job_phase),
                status = GetItemAttribute(item, attribute_job_status),
                title = GetItemAttribute(item, attribute_job_programtitle),
                episodeTitle = GetItemAttribute(item, attribute_job_episodetitle),
                description = GetItemAttribute(item, attribute_job_programdescription),
                cloudUri1000 = GetItemAttribute(item, attribute_job_cloudUri1000),
                cloudUri250 = GetItemAttribute(item, attribute_job_cloudUri250)
            };

            if (GetItemAttribute(item, attribute_job_priority) != null)
            {
                result.priority = int.Parse(GetItemAttribute(item, attribute_job_priority));
            }
            if (GetItemAttribute(item, attribute_job_progress) != null)
            {
                result.progress = double.Parse(GetItemAttribute(item, attribute_job_progress));
            }
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
            return string.Format("{0} - {1}:{2}", id, action, title);
        }

        string action;
        string path;
        int priority;
        Guid id;
        string phase;
        string status;
        double progress;
        string notes;
        string handledBy;
        string title;
        string episodeTitle;
        string description;
        DateTime lastUpdate;
        string cloudUri1000;
        string cloudUri250;
    
        // =========================================================
        //  Static Items 
        // =========================================================
        private static AmazonSimpleDBClient client = new AmazonSimpleDBClient(AwsConfig.aws_id, AwsConfig.aws_secret);

        private static readonly string job_queue_table = "job-queue";
        private static readonly string attribute_job_action = "action";
        private static readonly string attribute_job_path = "path";
        private static readonly string attribute_job_phase = "phase";
        private static readonly string attribute_job_status = "status";
        private static readonly string attribute_job_progress = "progress";
        private static readonly string attribute_job_notes = "notes";
        private static readonly string attribute_job_priority = "priority";
        private static readonly string attribute_job_handledby = "handled_by";
        private static readonly string attribute_job_programtitle = "programtitle";
        private static readonly string attribute_job_episodetitle = "episodetitle";
        private static readonly string attribute_job_programdescription = "programdescription";
        private static readonly string attribute_job_lastupdate = "lastupdate";
        private static readonly string attribute_job_cloudUri1000 = "cloudmedia-1000-uri";
        private static readonly string attribute_job_cloudUri250 = "cloudmedia-250-uri";
        
        private static readonly string action_encode = "encode";

    }
}