using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogueData", menuName = "Dialogue/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    public List<DialogueLine> lines = new List<DialogueLine>();
}

[System.Serializable]
public class DialogueLine
{
    [Header("Speaker")]
    [Tooltip("Displayed in the subtitle UI as the speaker's name.")]
    public string speakerName;

    [Header("UI")]
    [TextArea(2, 6)]
    public string text;

    [Header("Typewriter")]
    [Min(1f)]
    public float lettersPerSecond = 30f;

    [Header("Optional Audio")]
    public AudioClip talkingLoopClip;
}