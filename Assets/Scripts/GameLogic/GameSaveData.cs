using System;
using System.Collections.Generic;

/// <summary>
/// Full serializable snapshot of one save slot.
/// Stored to disk via SaveGame Free (SaveGame.Save / SaveGame.Load).
/// Key used on disk: "slot_N"  (N = 0-5)
/// </summary>
[Serializable]
public class GameSaveData
{
    // =====================================================================
    // Slot meta
    // =====================================================================
    public bool isEmpty = true;
    public string saveName = "";
    public string timestamp = "";   // "MMM dd, yyyy  HH:mm"
    public float playTimeSeconds = 0f;

    // =====================================================================
    // Player attributes (set at character creation, never change after that)
    // =====================================================================
    public float intelligence = 10f;
    public float strength = 10f;
    public float staminaAttr = 10f;
    public float agility = 10f;

    // =====================================================================
    // Runtime resources
    // -1 means "fill to max" and is only set on a brand-new save so that
    // PlayerStats initialises correctly before any save has been written.
    // =====================================================================
    public float health = -1f;
    public float mana = -1f;
    public float stamina = -1f;

    // =====================================================================
    // Player world position and rotation (CabinScene)
    // hasSavedPosition is false on a brand-new save so the spawn point tag
    // is used instead. It is set to true on the first real save-and-quit.
    // =====================================================================
    public bool hasSavedPosition = false;
    public float posX, posY, posZ;
    public float rotY;   // only yaw matters for a first-person character

    // =====================================================================
    // Intro camera fade
    // False on a new save so IntroCameraFade plays the wake-up animation.
    // Set to true after the first save-and-quit so it never plays again.
    // =====================================================================
    public bool introHasPlayed = false;

    // =====================================================================
    // Mission state
    // =====================================================================

    /// <summary>MissionData.Id values for missions the player can currently accept.</summary>
    public List<string> availableMissionIds = new List<string>();

    /// <summary>MissionData.Id values for missions the player has fully completed.</summary>
    public List<string> completedMissionIds = new List<string>();

    /// <summary>MissionData.Id values that have been started at least once.</summary>
    public List<string> playedMissionIds = new List<string>();

    /// <summary>DoorSequenceData asset names that have already played.</summary>
    public List<string> completedSequenceIds = new List<string>();
}