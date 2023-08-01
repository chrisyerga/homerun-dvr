using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using Microsoft.Samples.ServiceHosting.StorageClient;
using System.Data.Services.Client;

namespace HdHomerunLib.AzureTableTest
{
    public class Episode : TableStorageEntity
    {
        public string Title { get { return PartitionKey; } set { } }
        public string EpisodeTitle { get { return RowKey;} set {} }
        public string Rating { get; set; }
        public Episode(string title, string episodeTitle)
        {
            PartitionKey = title;
            RowKey = episodeTitle;
        }

        public Episode() :
            base("", string.Format("{0:d10}", DateTime.Now.Ticks))
        {
        }
    }

    public class EpisodeTable : TableStorageDataServiceContext
    {
        public EpisodeTable() :
            base(StorageAccountInfo.GetDefaultTableStorageAccountFromConfiguration())
        {
        }

        public EpisodeTable(StorageAccountInfo accountInfo) :
            base(accountInfo)
        {
        }

        public DataServiceQuery<Episode> Episodes
        {
            get
            {
                return CreateQuery<Episode>("Episodes");
            }
        }
    }

}
