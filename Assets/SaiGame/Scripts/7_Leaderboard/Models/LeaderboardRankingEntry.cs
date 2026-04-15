using System;

namespace SaiGame.Services
{
    [Serializable]
    public class LeaderboardRankingEntry
    {
        public int rank;
        public string user_id;
        public float score;
        public string metadata;
        public string updated_at;
    }
}
