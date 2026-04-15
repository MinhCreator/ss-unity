using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Server response for POST /api/v1/games/{gameId}/quests/{questDefinitionId}/check
    /// </summary>
    [Serializable]
    public class CheckQuestResponse
    {
        public CheckQuestProgressRecord progress;
        public QuestDefinitionData quest_definition;
    }
}
