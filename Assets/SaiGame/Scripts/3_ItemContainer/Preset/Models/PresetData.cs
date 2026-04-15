using System;

namespace SaiGame.Services
{
    [Serializable]
    public class PresetData
    {
        public string id;
        public string definition_id;
        public PresetDefinition definition;
        public string preset_type;
        public int max_slots;
        public bool is_temp;
        public PresetSlotData[] slots;
        public string created_at;
        public string updated_at;
    }
}
