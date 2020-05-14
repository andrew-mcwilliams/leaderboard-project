using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Policy;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace LeaderboardWPF
{

    public class TimestampRange
    {
        public long TimestampBegin { get; set; }
        public long TimestampEnd { get; set; }
    }

    public abstract class RequestDefinition
    {
        [ReadOnly(true)]
        [JsonIgnore]
        public HttpMethod Method { get; set; }

        [ReadOnly(true)]
        public string Action { get; set; }
    }

    public class GetScores : RequestDefinition
    {
        public GetScores()
        {
            Action = "GetScores";
            Method = HttpMethod.Get;

            TimestampRange = new TimestampRange();

        }
        public string Username { get; set; }
        public string Order { get; set; }
        public int? MaxEntries { get; set; }

        [ExpandableObject]
        public TimestampRange TimestampRange { get; set; }
    }

    public class SubmitScore : RequestDefinition
    {
        public SubmitScore()
        {
            Action = "SubmitScore";
            Method = HttpMethod.Post;
        }
        public string Username { get; set; }
        public int Score { get; set; }
        public long? Timestamp { get; set; }
    }

    public class DeleteScore : RequestDefinition
    {
        public DeleteScore()
        {
            Action = "DeleteScore";
            Method = HttpMethod.Post;

            TimestampRange = new TimestampRange();
        }

        public string Username { get; set; }

        [ExpandableObject]
        public TimestampRange TimestampRange { get; set; }
    }

    public class ClearScores : RequestDefinition
    {
        public ClearScores()
        {
            Action = "ClearScores";
            Method = HttpMethod.Post;

            TimestampRange = new TimestampRange();
        }

        [ExpandableObject]
        public TimestampRange TimestampRange { get; set; }
    }
}
