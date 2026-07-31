using UnityEngine;

[CreateAssetMenu(fileName = "StoryData", menuName = "Story/StoryData")]
public class StoryData : ScriptableObject
{
    [System.Serializable]
    public class DialogueLine
    {
        public string dialogue;           
        public Sprite background;         
        public Sprite character1;         
        public Vector2 character1Position; 
    }

    public DialogueLine[] dialogues;
}