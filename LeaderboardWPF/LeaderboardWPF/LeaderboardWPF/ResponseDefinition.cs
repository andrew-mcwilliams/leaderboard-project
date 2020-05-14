using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace LeaderboardWPF
{
    public abstract class ResponseDefinition
    {
        public bool Success { get; set; }
        public string ErrorString { get; set; }
    }

    public class ScoreDefinition
    {
        public string Username { get; set; }
        public int Score { get; set; }
        public long Timestamp { get; set; }
    }
    public class GetScoresResponse : ResponseDefinition
    {
        public List<ScoreDefinition> Scores { get; set; }
    }
}
