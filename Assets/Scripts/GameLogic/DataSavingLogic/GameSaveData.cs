using System;
using System.Collections.Generic;

[Serializable]
public class GameSaveData
{
    public bool isEmpty = true;
    public string saveName = "";
    public string timestamp = "";
    public float playTimeSeconds = 0f;

    // Attributes
    public float intelligence = 10f;
    public float strength = 10f;
    public float staminaAttr = 10f;
    public float agility = 10f;

    // Resources (negative = not saved, will use defaults)
    public float health = -1f;
    public float mana = -1f;
    public float stamina = -1f;

    // Position
    public bool hasSavedPosition = false;
    public float posX, posY, posZ;
    public float rotY;

    // Story flags
    public bool introHasPlayed = false;

    // ----- Mission progression -----
    // Missions the player has available but not yet completed
    public List<string> unlockedMissionIds = new List<string>();
    // The most recently unlocked mission (can be used for UI hints)
    public string lastUnlockedMissionId = "";
    // Missions that have been completed
    public List<string> completedMissionIds = new List<string>();
    // Missions that have been started at least once
    public List<string> playedMissionIds = new List<string>();

    // ----- Sequences -----
    // Sequences that have been played through to the end
    public List<string> completedSequenceIds = new List<string>();
    // Sequences that have been started (playing or completed)
    public List<string> playedSequenceIds = new List<string>();

    // ----- Door progression -----
    public List<string> doorCurrentSequenceKeys = new List<string>();
    public List<string> doorCurrentSequenceValues = new List<string>();

    // ----- Items & effects -----
    public List<SavedItemData> savedItems = new List<SavedItemData>();
    public List<SavedEffectData> savedEffects = new List<SavedEffectData>();
}