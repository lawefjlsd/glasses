using UnityEngine;

[CreateAssetMenu(fileName = "StoryData", menuName = "Story/StoryData")]
/// <summary>
/// 스토리 대사와 표시 자산을 보관합니다.
/// </summary>
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