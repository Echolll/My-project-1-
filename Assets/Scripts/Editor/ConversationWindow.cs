using RPG.ScriptableObjects.Contexts;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RPG.Editor
{
    public class ConversationWindow : BaseCustomEditorWindow
    {
        private const float _defaultIndent = 8f;
        private const float _answersIndent = 32f;

        private int _answerIndex;

        private FieldInfo _reflectionName;
        private Vector2 _scroll;
        private Dictionary<PhraseType, Color> _colorDic;

        private float GetDialogueWidth => position.width * 0.8f - _defaultIndent;

        private float GetAnswerWidth => position.width * 0.8f - _answersIndent;

        public ConversationContext SelectConversation { get; set; }

        [MenuItem("Extensions/Windows/Conversation Windows #v", priority = 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<ConversationWindow>(false, "Conversation Window",true);
            window.minSize = window.maxSize = new Vector2(300f, 450f);
        }

        public override void OnEnable()
        {
            if(EditorPrefs.HasKey("Conversation:select"))
                SelectConversation = AssetDatabase.LoadAssetAtPath(EditorPrefs.GetString("Conversation:select"), typeof(ConversationContext)) as ConversationContext;
        }

        protected override void OnDisable()
        {
            if (SelectConversation != null)
                EditorPrefs.SetString("Conversation:select", AssetDatabase.GetAssetPath(SelectConversation));
        }

        private bool PrintHeader()
        {
            EditorGUILayout.BeginHorizontal("box");
            GUILayout.Space(GetDefaultSpace);
            EditorGUILayout.LabelField("Start dialogue:", GUILayout.MaxWidth(90f));            
            GUILayout.Space(GetDefaultSpace / 2f);

            SelectConversation = EditorGUILayout.ObjectField(SelectConversation, typeof(ConversationContext), false, GUILayout.Width(150f)) as ConversationContext;

            GUILayout.Space(GetDefaultSpace);           
            GUI.color = new Color(0.5f, 1f, 0f, 1f);
            if(GUILayout.Button("Load", GUIEditorExtensions.ButtonStyleFontSize16,GUIEditorExtensions.ButtonOptionMediumSize))
            {
                LoadConversationWindow.ShowLoadConversationWindow(this);
                EditorGUILayout.EndHorizontal();
                return false;
            }
            
            GUI.color = new Color(0.5f, 1f, 0f, 1f);
            if (GUILayout.Button("Compile", GUIEditorExtensions.ButtonStyleFontSize16,GUIEditorExtensions.ButtonOptionMediumSize))
            {
                EditorUtility.SetDirty(SelectConversation);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorGUILayout.EndHorizontal();
                return false;
            }

            GUI.color = Color.white;

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            return true;
        }

        private void Compile()
        {

        }

        private ConversationBaseContext PrintBlock(ConversationBaseContext context)
        {
            if(context == null) return null;
            
            DrawInfo(context);

            if(context.Type == PhraseType.Phrase)
            {
                if(context.Dialogue == null)
                {
                    CreateNewContext(context, typeof(DialogueContext));
                }
                PrintBlock(context.Dialogue);
            }
            else if(context.Type == PhraseType.Answers)
            {
                var dialogue = context as DialogueContext;
                CreateNewContext(context, typeof(AnswerContext), dialogue.Answers == null || dialogue.Answers.Count == 0);
                if (dialogue.Answers == null) return null;
                foreach (var answer in dialogue.Answers) 
                {
                   PrintBlock(answer);
                }
            }

            return null;           
        }

        private void DrawInfo(ConversationBaseContext context)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUI.color = _colorDic[context.Type];
            EditorGUILayout.BeginHorizontal("box", GUILayout.Width(context is DialogueContext? GetDialogueWidth : GetAnswerWidth));

            //EditorGUILayout.LabelField(_reflectionName.GetValue(context) as string, GUILayout.Width(GetDefaultSpace * 2f));
            GUI.color = Color.white;
            context.ContextID = EditorGUILayout.TextField(context.ContextID);
            
            context.Type = (PhraseType)EditorGUILayout.EnumPopup(context.Type);
            if(context is AnswerContext && context.Type == PhraseType.Answers)
            {
                Debug.Log($"Can it set type <b>{context.Type}</b> for objects of type <b>{context.GetType().Name}</b>");
                context.Type = PhraseType.None;
            }    
            
            GUILayout.FlexibleSpace();
            if (context.Type == PhraseType.None) EditorGUILayout.LabelField("End of dialogue", GUIEditorExtensions.SmallHeaderLabelStyle, GUILayout.Width(100f));
            if (context.Type == PhraseType.Trade) EditorGUILayout.LabelField("Go to the shop", GUIEditorExtensions.SmallHeaderLabelStyle, GUILayout.Width(100f));
            if (context.Type == PhraseType.Quest) EditorGUILayout.LabelField("Quest update", GUIEditorExtensions.SmallHeaderLabelStyle, GUILayout.Width(100f));

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(GetDefaultSpace * 3f);
            EditorGUILayout.EndHorizontal();
        }
        
        private void CreateNewContext(ConversationBaseContext context, Type type, bool showHelpBox = true)
        {           
            if (showHelpBox)
                EditorGUILayout.HelpBox($"The context is of the type {context.Type} but does not contain a continuation", MessageType.Warning);

            EditorGUILayout.BeginHorizontal();

            GUI.color = GUIEditorExtensions.ColorGUI[GUIEditorExtensions.ColorGUIType.Cyan];
            if(GUILayout.Button("Create", GUILayout.Width(80f)))
            {
                CalculateIndexes();

                var instance = Activator.CreateInstance(type);
                _reflectionName.SetValue(instance, string.Concat(_answerIndex));

                if(type == typeof(DialogueContext)) 
                {
                    context.Dialogue = instance as DialogueContext;    
                }
                else 
                {
                    var dialogue = context as DialogueContext;
                    if (dialogue.Answers == null) dialogue.Answers = new List<AnswerContext>();
                    dialogue.Answers.Add(instance as AnswerContext);
                }
 
                EditorUtility.SetDirty(SelectConversation);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            InitStyles();

            if (!PrintHeader()) return;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (SelectConversation != null) PrintBlock(SelectConversation.Context);

            EditorGUILayout.EndScrollView();
        }

        private void CalculateIndexes()
        {
           var pair = SelectConversation.GetAllAnswerContexts();
           _answerIndex = pair.Item1.Count;
        }

        private void InitStyles()
        {
            if(_colorDic == null) 
            {
                _colorDic = new Dictionary<PhraseType, Color>
               {
                   {PhraseType.None , GUIEditorExtensions.ColorGUI[GUIEditorExtensions.ColorGUIType.Red]},
                   {PhraseType.Phrase , GUIEditorExtensions.ColorGUI[GUIEditorExtensions.ColorGUIType.Turquoise]},
                   {PhraseType.Answers , GUIEditorExtensions.ColorGUI[GUIEditorExtensions.ColorGUIType.Yellow]},
                   {PhraseType.Quest , GUIEditorExtensions.ColorGUI[GUIEditorExtensions.ColorGUIType.Green]},
                   {PhraseType.Trade , GUIEditorExtensions.ColorGUI[GUIEditorExtensions.ColorGUIType.Purple]}
               };
            
            }

            if (_reflectionName == null)
            {
                _reflectionName = typeof(BaseContext).GetField("_name", GUIEditorExtensions.GetPrivateReflectionFlags);
            }
        }      
    }
}
