using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(QuestProgressor))]
    [CanEditMultipleObjects]
    public class QuestProgressorEditor : Editor
    {
        private QuestProgressor questProgressor;
        private SerializedProperty questSourceType;

        private bool showQuestPicker = true;
        private bool showLastStarted = true;
        private bool showUtilityButtons = true;

        // Quest picker state
        private List<QuestPickerEntry> pickerEntries = new List<QuestPickerEntry>();
        private string[] pickerLabels = new string[0];
        private int selectedQuestIndex = 0;
        private bool isStarting = false;
        private bool isChecking = false;
        private bool isClaiming = false;
        private bool isLoadingAll = false;
        private bool showLastChecked = true;
        private bool showLastClaimed = true;

        private void OnEnable()
        {
            this.questProgressor = (QuestProgressor)target;
            this.questSourceType = serializedObject.FindProperty("questSourceType");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Quest Progressor", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // ── Source selector ───────────────────────────────────────────────
            EditorGUILayout.LabelField("Quest Source", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField(new GUIContent("Source Type", "Where to load the available quests from"), GUILayout.Width(EditorGUIUtility.labelWidth - 4));
            QuestSourceType newSourceType = (QuestSourceType)EditorGUILayout.EnumPopup((QuestSourceType)this.questSourceType.enumValueIndex, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck())
            {
                this.questSourceType.enumValueIndex = (int)newSourceType;
                this.pickerEntries.Clear();
                this.pickerLabels = new string[0];
                this.selectedQuestIndex = 0;
            }
            bool canLoadAll = Application.isPlaying && SaiService.Instance != null && SaiService.Instance.IsAuthenticated;
            GUI.backgroundColor = (canLoadAll && !this.isLoadingAll) ? new Color(0.3f, 0.9f, 0.5f) : Color.gray;
            EditorGUI.BeginDisabledGroup(!canLoadAll || this.isLoadingAll);
            if (GUILayout.Button(this.isLoadingAll ? "Loading..." : "Load All", GUILayout.Width(90), GUILayout.Height(18)))
                this.LoadAllQuests();
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // ── Quest picker ──────────────────────────────────────────────────
            this.showQuestPicker = EditorGUILayout.Foldout(this.showQuestPicker, "Quest Picker", true);
            if (this.showQuestPicker)
            {
                EditorGUI.indentLevel++;

                bool canLoad = Application.isPlaying
                               && SaiService.Instance != null
                               && SaiService.Instance.IsAuthenticated;

                // Refresh button
                GUI.backgroundColor = canLoad ? new Color(0.4f, 0.9f, 1f) : Color.gray;
                EditorGUI.BeginDisabledGroup(!canLoad);
                if (GUILayout.Button("Refresh Quest List", GUILayout.Height(26)))
                    this.RefreshQuestPicker();
                EditorGUI.EndDisabledGroup();
                GUI.backgroundColor = Color.white;

                if (!Application.isPlaying)
                    EditorGUILayout.HelpBox("Enter Play Mode and log in to populate the quest list.", MessageType.Info);
                else if (!canLoad)
                    EditorGUILayout.HelpBox("Not authenticated. Please login first.", MessageType.Warning);

                if (this.pickerEntries.Count == 0)
                {
                    EditorGUILayout.HelpBox("No quests loaded. Click Refresh Quest List or load chain members first.", MessageType.None);
                }
                else
                {
                    this.selectedQuestIndex = Mathf.Clamp(this.selectedQuestIndex, 0, this.pickerLabels.Length - 1);
                    this.selectedQuestIndex = EditorGUILayout.Popup("Select Quest", this.selectedQuestIndex, this.pickerLabels);

                    QuestPickerEntry selected = this.pickerEntries[this.selectedQuestIndex];

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    GUIStyle richStyle = new GUIStyle(EditorStyles.label) { richText = true };
                    EditorGUILayout.LabelField($"<b>{selected.displayName}</b>", richStyle);
                    EditorGUILayout.LabelField($"Source: {selected.sourceLabel}");
                    EditorGUILayout.LabelField($"Quest Def ID: {selected.questDefinitionId}");
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.Space(4);

                    EditorGUILayout.BeginHorizontal();
                    GUI.backgroundColor = this.isStarting ? Color.gray : new Color(1f, 0.85f, 0f);
                    EditorGUI.BeginDisabledGroup(this.isStarting || !canLoad);
                    if (GUILayout.Button(this.isStarting ? "Starting..." : "Start Quest", GUILayout.Height(30)))
                        this.StartSelectedQuest(selected.questDefinitionId);
                    EditorGUI.EndDisabledGroup();
                    GUI.backgroundColor = this.isChecking ? Color.gray : new Color(0.2f, 0.85f, 0.85f);
                    EditorGUI.BeginDisabledGroup(this.isChecking || !canLoad);
                    if (GUILayout.Button(this.isChecking ? "..." : "Check", GUILayout.Height(30)))
                        this.CheckSelectedQuest(selected.questDefinitionId);
                    EditorGUI.EndDisabledGroup();
                    GUI.backgroundColor = this.isClaiming ? Color.gray : new Color(0.4f, 1f, 0.4f);
                    EditorGUI.BeginDisabledGroup(this.isClaiming || !canLoad);
                    if (GUILayout.Button(this.isClaiming ? "..." : "Claim", GUILayout.Height(30)))
                        this.ClaimSelectedQuest(selected.questDefinitionId);
                    EditorGUI.EndDisabledGroup();
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            // ── Last claimed quest ─────────────────────────────────────────────────────
            this.showLastClaimed = EditorGUILayout.Foldout(this.showLastClaimed, "Last Claimed Quest", true);
            if (this.showLastClaimed)
            {
                EditorGUI.indentLevel++;
                ClaimQuestResponse lastClaim = this.questProgressor.LastClaimedQuest;
                if (lastClaim != null)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    GUIStyle rich = new GUIStyle(EditorStyles.label) { richText = true };
                    EditorGUILayout.LabelField($"Claim ID: {lastClaim.id}", rich);
                    EditorGUILayout.LabelField($"Quest Def ID: {lastClaim.quest_definition_id}");
                    EditorGUILayout.LabelField($"Progress ID: {lastClaim.progress_id}");
                    EditorGUILayout.LabelField($"Claimed At: {lastClaim.claimed_at}");
                    if (lastClaim.rewards_granted != null && lastClaim.rewards_granted.Length > 0)
                    {
                        EditorGUILayout.Space(2);
                        EditorGUILayout.LabelField("Rewards Granted:", EditorStyles.boldLabel);
                        foreach (ClaimQuestGrantedReward r in lastClaim.rewards_granted)
                            EditorGUILayout.LabelField($"  • {r.reward_type}" +
                                (r.amount > 0 ? $" x{r.amount}" : "") +
                                (r.quantity > 0 ? $" qty:{r.quantity}" : "") +
                                (!string.IsNullOrEmpty(r.item_definition_id) ? $" (item: {r.item_definition_id})" : ""));
                    }
                    EditorGUILayout.EndVertical();
                }
                else
                {
                    EditorGUILayout.HelpBox("No quest claimed this session.", MessageType.None);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            // ── Last checked quest ────────────────────────────────────────────
            this.showLastChecked = EditorGUILayout.Foldout(this.showLastChecked, "Last Checked Quest", true);
            if (this.showLastChecked)
            {
                EditorGUI.indentLevel++;
                CheckQuestResponse lastCheck = this.questProgressor.LastCheckedQuest;
                if (lastCheck != null && lastCheck.progress != null)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    GUIStyle rich = new GUIStyle(EditorStyles.label) { richText = true };
                    string statusColor = lastCheck.progress.status == "in_progress" ? "#FFD700"
                                      : lastCheck.progress.status == "completed"    ? "#00FF88"
                                      : "#AAAAAA";
                    EditorGUILayout.LabelField($"Status: <b><color={statusColor}>{lastCheck.progress.status}</color></b>", rich);
                    if (lastCheck.quest_definition != null)
                    {
                        EditorGUILayout.LabelField($"Quest: <b>{lastCheck.quest_definition.name}</b>", rich);
                        EditorGUILayout.LabelField($"Type: {lastCheck.quest_definition.quest_type}");
                    }
                    EditorGUILayout.LabelField($"Progress ID: {lastCheck.progress.id}");
                    EditorGUILayout.LabelField($"Quest Def ID: {lastCheck.progress.quest_definition_id}");
                    EditorGUILayout.LabelField($"Version: {lastCheck.progress.version}");
                    EditorGUILayout.LabelField($"Updated: {lastCheck.progress.updated_at}");
                    if (!string.IsNullOrEmpty(lastCheck.progress.progress_data_json))
                        this.DrawProgressData(lastCheck.progress.progress_data_json);
                    if (lastCheck.quest_definition?.rewards != null && lastCheck.quest_definition.rewards.Length > 0)
                    {
                        EditorGUILayout.Space(2);
                        EditorGUILayout.LabelField("Rewards:", EditorStyles.boldLabel);
                        foreach (QuestReward r in lastCheck.quest_definition.rewards)
                            EditorGUILayout.LabelField($"  • {r.reward_type}" +
                                (r.amount > 0 ? $" x{r.amount}" : "") +
                                (!string.IsNullOrEmpty(r.item_definition_id) ? $" (item: {r.item_definition_id})" : ""));
                    }
                    EditorGUILayout.EndVertical();
                }
                else
                {
                    EditorGUILayout.HelpBox("No quest checked this session.", MessageType.None);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // ── Last started quest ────────────────────────────────────────────
            this.showLastStarted = EditorGUILayout.Foldout(this.showLastStarted, "Last Started Quest", true);
            if (this.showLastStarted)
            {
                EditorGUI.indentLevel++;

                StartQuestResponse last = this.questProgressor.LastStartedQuest;
                if (last != null)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    GUIStyle rich = new GUIStyle(EditorStyles.label) { richText = true };
                    string statusColor = last.status == "in_progress" ? "#FFD700"
                                      : last.status == "completed"    ? "#00FF88"
                                      : "#AAAAAA";
                    EditorGUILayout.LabelField($"Status: <b><color={statusColor}>{last.status}</color></b>", rich);
                    EditorGUILayout.LabelField($"Progress ID: {last.id}");
                    EditorGUILayout.LabelField($"Quest Def ID: {last.quest_definition_id}");
                    EditorGUILayout.LabelField($"User ID: {last.user_id}");
                    EditorGUILayout.LabelField($"Version: {last.version}");
                    EditorGUILayout.LabelField($"Created: {last.created_at}");
                    EditorGUILayout.EndVertical();
                }
                else
                {
                    EditorGUILayout.HelpBox("No quest started this session.", MessageType.None);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // ── Utility Actions ───────────────────────────────────────────────
            this.showUtilityButtons = EditorGUILayout.Foldout(this.showUtilityButtons, "Utility Actions", true);
            if (this.showUtilityButtons)
            {
                EditorGUI.indentLevel++;

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Clear Last Quest", GUILayout.Height(30)))
                    this.questProgressor.ClearLastStartedQuest();
                GUI.backgroundColor = Color.white;

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Events are automatically registered/unregistered with SaiAuth login/logout events.", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void RefreshQuestPicker()
        {
            this.pickerEntries = this.questProgressor.BuildQuestPickerEntries();
            this.BuildPickerLabels();
            this.selectedQuestIndex = 0;
            Repaint();
        }

        private void BuildPickerLabels()
        {
            if (this.pickerEntries.Count == 0)
            {
                this.pickerLabels = new string[0];
                return;
            }

            this.pickerLabels = new string[this.pickerEntries.Count];
            for (int i = 0; i < this.pickerEntries.Count; i++)
            {
                QuestPickerEntry e = this.pickerEntries[i];
                this.pickerLabels[i] = $"[{e.sourceLabel}]  {e.displayName}";
            }
        }

        private void LoadAllQuests()
        {
            if (SaiService.Instance == null || !SaiService.Instance.IsAuthenticated) return;

            QuestSourceType sourceType = (QuestSourceType)this.questSourceType.enumValueIndex;
            switch (sourceType)
            {
                case QuestSourceType.ChainQuest:
                    this.LoadAllChainQuests();
                    break;
            }
        }

        private void LoadAllChainQuests()
        {
            ChainQuest chainQuest = SaiService.Instance?.ChainQuest;
            if (chainQuest == null) return;

            this.isLoadingAll = true;
            Repaint();

            chainQuest.GetChains(
                onSuccess: response =>
                {
                    if (response.chains == null || response.chains.Length == 0)
                    {
                        this.isLoadingAll = false;
                        this.RefreshQuestPicker();
                        return;
                    }

                    int pending = response.chains.Length;
                    foreach (ChainQuestData chain in response.chains)
                    {
                        chainQuest.GetChainMembers(
                            chainId: chain.id,
                            onSuccess: _ =>
                            {
                                pending--;
                                if (pending <= 0)
                                {
                                    this.isLoadingAll = false;
                                    this.RefreshQuestPicker();
                                }
                                Repaint();
                            },
                            onError: err =>
                            {
                                pending--;
                                if (pending <= 0)
                                {
                                    this.isLoadingAll = false;
                                    this.RefreshQuestPicker();
                                }
                                Debug.LogWarning($"[QuestProgressorEditor] Failed to load members for chain {chain.id}: {err}");
                                Repaint();
                            }
                        );
                    }
                },
                onError: error =>
                {
                    this.isLoadingAll = false;
                    Debug.LogError($"[QuestProgressorEditor] Failed to load chains: {error}");
                    Repaint();
                }
            );
        }

        private void ClaimSelectedQuest(string questDefinitionId)
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[QuestProgressorEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[QuestProgressorEditor] Not authenticated! Please login first.");
                return;
            }

            this.isClaiming = true;
            Repaint();

            this.questProgressor.ClaimQuest(
                questDefinitionId: questDefinitionId,
                onSuccess: response =>
                {
                    this.isClaiming = false;
                    Debug.Log($"[QuestProgressorEditor] Quest claimed  id={response.id}  claimed_at={response.claimed_at}");
                    Repaint();
                },
                onError: error =>
                {
                    this.isClaiming = false;
                    Debug.LogError($"[QuestProgressorEditor] Failed to claim quest: {error}");
                    Repaint();
                }
            );
        }

        private void CheckSelectedQuest(string questDefinitionId)
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[QuestProgressorEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[QuestProgressorEditor] Not authenticated! Please login first.");
                return;
            }

            this.isChecking = true;
            Repaint();

            this.questProgressor.CheckQuest(
                questDefinitionId: questDefinitionId,
                onSuccess: response =>
                {
                    this.isChecking = false;
                    Debug.Log($"[QuestProgressorEditor] Quest checked  status={response.progress?.status}  quest={response.quest_definition?.name}");
                    Repaint();
                },
                onError: error =>
                {
                    this.isChecking = false;
                    Debug.LogError($"[QuestProgressorEditor] Failed to check quest: {error}");
                    Repaint();
                }
            );
        }

        private void DrawProgressData(string json)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Progress Data:", EditorStyles.boldLabel);

            GUIStyle clauseHeader = new GUIStyle(EditorStyles.label) { richText = true, fontStyle = FontStyle.Bold, fontSize = 10 };
            GUIStyle fieldLabel   = new GUIStyle(EditorStyles.label) { richText = true, fontSize = 10 };

            foreach (var clause in this.ParseTopLevelEntries(json))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"<color=#4DD0E1><b>{clause.Key}</b></color>", clauseHeader);

                if (clause.Value.TrimStart().StartsWith("{"))
                {
                    foreach (var field in this.ParseFlatObject(clause.Value))
                    {
                        bool isNumericProgress = field.Key == "opened" || field.Key == "required";
                        string valColor = isNumericProgress ? "#FFD700" : "#CCCCCC";
                        EditorGUILayout.LabelField(
                            $"  <color=#888888>{field.Key}:</color>  <color={valColor}>{field.Value}</color>",
                            fieldLabel);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField($"  <color=#CCCCCC>{clause.Value}</color>", fieldLabel);
                }

                EditorGUILayout.EndVertical();
            }
        }

        private System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>
            ParseTopLevelEntries(string json)
        {
            var result = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>();
            json = json.Trim();
            if (json.StartsWith("{")) json = json.Substring(1);
            if (json.EndsWith("}"))  json = json.Substring(0, json.Length - 1);

            int i = 0;
            while (i < json.Length)
            {
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                if (i >= json.Length) break;
                if (json[i] == ',') { i++; continue; }
                if (json[i] != '"') { i++; continue; }
                i++;

                int keyStart = i;
                while (i < json.Length && json[i] != '"') i++;
                string key = json.Substring(keyStart, i - keyStart);
                i++;

                while (i < json.Length && (char.IsWhiteSpace(json[i]) || json[i] == ':')) i++;
                string value = this.ReadJsonValue(json, ref i);
                result.Add(new System.Collections.Generic.KeyValuePair<string, string>(key, value));
            }
            return result;
        }

        private System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>
            ParseFlatObject(string json)
        {
            var result = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>();
            json = json.Trim();
            if (json.StartsWith("{")) json = json.Substring(1);
            if (json.EndsWith("}"))  json = json.Substring(0, json.Length - 1);

            int i = 0;
            while (i < json.Length)
            {
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                if (i >= json.Length) break;
                if (json[i] == ',') { i++; continue; }
                if (json[i] != '"') { i++; continue; }
                i++;

                int keyStart = i;
                while (i < json.Length && json[i] != '"') i++;
                string key = json.Substring(keyStart, i - keyStart);
                i++;

                while (i < json.Length && (char.IsWhiteSpace(json[i]) || json[i] == ':')) i++;
                string value = this.ReadJsonValue(json, ref i);

                if (value.StartsWith("\"") && value.EndsWith("\""))
                    value = value.Substring(1, value.Length - 2);

                result.Add(new System.Collections.Generic.KeyValuePair<string, string>(key, value));
            }
            return result;
        }

        private string ReadJsonValue(string json, ref int i)
        {
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length) return "";

            if (json[i] == '{' || json[i] == '[')
            {
                char open  = json[i];
                char close = open == '{' ? '}' : ']';
                int start = i, depth = 0;
                while (i < json.Length)
                {
                    if (json[i] == open)  depth++;
                    else if (json[i] == close) { depth--; if (depth == 0) { i++; break; } }
                    i++;
                }
                return json.Substring(start, i - start);
            }
            if (json[i] == '"')
            {
                int start = i++;
                while (i < json.Length && json[i] != '"') { if (json[i] == '\\') i++; i++; }
                i++;
                return json.Substring(start, i - start);
            }
            {
                int start = i;
                while (i < json.Length && json[i] != ',' && json[i] != '}' && json[i] != ']') i++;
                return json.Substring(start, i - start).Trim();
            }
        }

        private void StartSelectedQuest(string questDefinitionId)
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[QuestProgressorEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[QuestProgressorEditor] Not authenticated! Please login first.");
                return;
            }

            this.isStarting = true;
            Repaint();

            this.questProgressor.StartQuest(
                questDefinitionId: questDefinitionId,
                onSuccess: response =>
                {
                    this.isStarting = false;
                    Debug.Log($"[QuestProgressorEditor] Quest started  id={response.id}  status={response.status}  quest_def={response.quest_definition_id}");
                    Repaint();
                },
                onError: error =>
                {
                    this.isStarting = false;
                    Debug.LogError($"[QuestProgressorEditor] Failed to start quest: {error}");
                    Repaint();
                }
            );
        }
    }
}
