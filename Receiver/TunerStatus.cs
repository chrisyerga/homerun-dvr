using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Amazon.SimpleDB;
using Amazon.SimpleDB.Model;
using HdHomerunLib.JobQueue;

namespace HdHomerunLib.Receiver
{
    public class TunerStatus
    {
        private static AmazonSimpleDBClient client = new AmazonSimpleDBClient(AwsConfig.aws_id, AwsConfig.aws_secret);
        private static readonly string tuner_status_domain = "tuner-status";
        private static readonly string attribute_action = "action";
        private static readonly string attribute_service = "service";
        private static readonly string attribute_lastupdate = "lastupdate";

        private string id;
        private string action;
        private string service;
        private DateTime lastupdate;

        private TunerStatus(string id, string action, string service, DateTime lastupdate)
        {
            this.id = id;
            this.action = action;
            this.service = service;
            this.lastupdate = lastupdate;
        }

        public string TunerId { get { return id; } }
        public string Action { get { return action; } }
        public string Service { get { return service; } }
        public DateTime LastUpdate { get { return lastupdate; } }

        public static void UpdateStatus(string tunerid, string action, string service)
        {
            PutAttributesRequest req = new PutAttributesRequest();
            req.DomainName = tuner_status_domain;
            req.ItemName = tunerid;
            req.Attribute.Add(new ReplaceableAttribute() { Name = attribute_action, Value = action, Replace = true });
            req.Attribute.Add(new ReplaceableAttribute() { Name = attribute_service, Value = service, Replace = true });
            req.Attribute.Add(new ReplaceableAttribute() { Name = attribute_lastupdate, Value = DateTime.UtcNow.ToString(), Replace = true });

            try
            {
                var resp = client.PutAttributes(req);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("TunerStatus.UpdateStatus() -- Exception: {0}", e);
            }
        }

        private void UpdateAttribute(string tunerid, string name, string value)
        {
            PutAttributesRequest req = new PutAttributesRequest();
            req.DomainName = tuner_status_domain;
            req.ItemName = tunerid.ToString();
            req.Attribute.Add(new ReplaceableAttribute() { Name = name, Value = value, Replace = true });
            AddLastUpdatedAttribute(req.Attribute);

            client.PutAttributes(req);
        }

        private static void AddLastUpdatedAttribute(List<ReplaceableAttribute> attributes)
        {
            string datestring = DateTime.Now.ToString();
            attributes.Add(new ReplaceableAttribute() { Name = attribute_lastupdate, Value = datestring, Replace = true });
        }

        public static List<TunerStatus> GetAllTunerStatus()
        {
            var req = new QueryWithAttributesRequest
            {
                MaxNumberOfItems = 100,
                DomainName = tuner_status_domain
            };

            req.AttributeName.Add(attribute_action);
            req.AttributeName.Add(attribute_service);
            req.AttributeName.Add(attribute_lastupdate);

            var resp = client.QueryWithAttributes(req);

            List<TunerStatus> result = new List<TunerStatus>();
            foreach (Item item in resp.QueryWithAttributesResult.Item)
            {
                DateTime lastupdate;

                try
                {
                    lastupdate = DateTime.Parse(GetItemAttribute(item, attribute_lastupdate));
                }
                catch (Exception e)
                {
                    lastupdate = new DateTime(1999, 1, 1);
                }

                TunerStatus t = new TunerStatus(item.Name,
                    GetItemAttribute(item, attribute_action),
                    GetItemAttribute(item, attribute_service),
                    lastupdate);
                result.Add(t);
            }

            return result;
        }

        private static string GetItemAttribute(Item item, string attribute)
        {
            string result = null;

            foreach (Amazon.SimpleDB.Model.Attribute attrib in item.Attribute)
            {
                if (attrib.Name == attribute)
                {
                    if (result == null)
                    {
                        result = attrib.Value;
                    }
                    else if (attrib.Value.CompareTo(result) > 0)
                    {
                        result = attrib.Value;
                    }
                }
            }

            return result;
        }


    }
}
