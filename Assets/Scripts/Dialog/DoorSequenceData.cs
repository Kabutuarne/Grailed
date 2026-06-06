using UnityEngine;
using UnityEngine.Playables;
using Unity.Cinemachine;

public class DoorSequenceData : MonoBehaviour
{
    [Header("Basic Info")]
    public string sequenceName;

    [Header("Scene Start Timing")]
    public float knockStartDelay = 7f;
    public float knockInterval = 3f;
    public float interactionUnlockDelay = 10f;

    [Header("Dialogue & Timeline")]
    public DialogueData dialogueData;
    public PlayableDirector sequenceTimeline;

    [Header("Missions To Give")]
    public MissionData[] missionsToGive;

    [Header("Item Condition")]
    [Tooltip("Tag of the item to check when the player returns after completing the mission.")]
    public string conditionItemTag;

    [Header("Next Sequences")]
    [Tooltip("Sequence to use if the player returns WITH the tagged item.")]
    public DoorSequenceData nextSequenceWithItem;

    [Tooltip("Sequence to use if the player returns WITHOUT the tagged item.")]
    public DoorSequenceData nextSequenceWithoutItem;

    [Header("Speaker")]
    public Animator speakerAnimator;
    public AnimationClip[] talkAnimations;

    [Header("Talk Animation")]
    [Min(0.01f)]
    public float talkLoopSpeed = 0.5f;

    [Header("Cameras")]
    public CinemachineCamera playerVirtualCamera;
    public CinemachineCamera cinematicVirtualCamera;

    public int cinematicPriority = 20;
    public int inactiveCinematicPriority = 0;
    public int gameplayPriority = 10;

    [Header("Audio")]
    public AudioClip knockClip;

    public bool HasBeenPlayed()
    {
        if (MissionManager.Instance == null)
            return false;

        return MissionManager.Instance.HasSequenceBeenPlayed(this);
    }

    public void MarkAsPlayed()
    {
        if (MissionManager.Instance != null)
            MissionManager.Instance.MarkSequenceAsPlayed(this);
    }
}