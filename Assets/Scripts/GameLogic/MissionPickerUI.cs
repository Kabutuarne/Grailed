using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionPickerUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Mission List")]
    [SerializeField] private Transform missionListParent;
    [SerializeField] private MissionEntryUI missionEntryPrefab;
    [SerializeField] private GameObject noMissionsMessage;

    [Header("Selected Mission Details")]
    [SerializeField] private TMP_Text selectedTitleText;
    [SerializeField] private TMP_Text selectedDescriptionText;
    [SerializeField] private TMP_Text selectedAssignedByText;
    [SerializeField] private TMP_Text selectedDifficultyText;
    [SerializeField] private Button startMissionButton;

    [Header("Controls")]
    [SerializeField] private Button closeButton;

    [Header("Player Control")]
    [Tooltip("Optional PlayerController used to lock movement while the mission picker is open.")]
    [SerializeField] private PlayerController playerController;
    [Tooltip("Optional PlayerInteractor used to prevent world interaction while the mission picker is open.")]
    [SerializeField] private PlayerInteractor playerInteractor;
    [Tooltip("Optional PlayerUI used to hide the HUD while the mission picker is open.")]
    [SerializeField] private PlayerUI playerUI;

    private MissionData selectedMission;
    private MissionEntryUI selectedEntry;
    private readonly List<MissionEntryUI> spawnedEntries = new List<MissionEntryUI>();

    private void Awake()
    {
        if (root != null)
            root.SetActive(false);

        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();

        if (playerInteractor == null)
            playerInteractor = FindFirstObjectByType<PlayerInteractor>();

        if (startMissionButton != null)
            startMissionButton.onClick.AddListener(OnStartMissionPressed);

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    private void OnEnable()
    {
        RefreshMissionList();

        if (MissionManager.Instance != null)
            MissionManager.Instance.OnAvailableMissionsChanged += OnAvailableMissionsChanged;
    }

    private void OnDisable()
    {
        if (MissionManager.Instance != null)
            MissionManager.Instance.OnAvailableMissionsChanged -= OnAvailableMissionsChanged;
    }

    private void OnAvailableMissionsChanged(IReadOnlyCollection<MissionData> missions)
    {
        RefreshMissionList();
    }

    public void Show()
    {
        if (root != null)
            root.SetActive(true);

        FreezePlayer(true);

        RefreshMissionList();
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);

        FreezePlayer(false);
    }

    public void RefreshMissionList()
    {
        if (missionListParent == null || missionEntryPrefab == null)
            return;

        ClearSpawnedEntries();
        selectedMission = null;
        selectedEntry = null;
        UpdateSelectedMissionDetails();

        var missions = MissionManager.Instance?.GetAvailableMissions() ?? Array.Empty<MissionData>();
        bool hasMissions = missions.Count > 0;

        if (noMissionsMessage != null)
            noMissionsMessage.SetActive(!hasMissions);

        foreach (var mission in missions)
        {
            var entry = Instantiate(missionEntryPrefab, missionListParent);
            entry.Setup(mission, OnMissionEntrySelected);
            spawnedEntries.Add(entry);
        }
    }

    private void OnMissionEntrySelected(MissionData mission, MissionEntryUI entry)
    {
        selectedMission = mission;
        selectedEntry = entry;

        foreach (var spawned in spawnedEntries)
        {
            if (spawned != null)
                spawned.SetSelected(spawned == selectedEntry);
        }

        UpdateSelectedMissionDetails();
    }

    private void UpdateSelectedMissionDetails()
    {
        if (selectedTitleText != null)
            selectedTitleText.text = selectedMission != null ? selectedMission.title : "Select a mission";

        if (selectedDescriptionText != null)
            selectedDescriptionText.text = selectedMission != null ? selectedMission.description : "Choose one of the available missions to see the details.";

        if (selectedAssignedByText != null)
            selectedAssignedByText.text = selectedMission != null ? $"Assigned by: {selectedMission.assignedBy}" : string.Empty;

        if (selectedDifficultyText != null)
            selectedDifficultyText.text = selectedMission != null ? $"Difficulty: {selectedMission.DifficultyRoman}" : string.Empty;

        if (startMissionButton != null)
            startMissionButton.interactable = selectedMission != null;
    }

    private void OnStartMissionPressed()
    {
        if (selectedMission == null)
            return;

        MissionManager.Instance?.StartMission(selectedMission);
        Hide();
    }

    private void FreezePlayer(bool freeze)
    {
        if (playerController != null)
            playerController.SetControlLocked(freeze);

        if (playerInteractor != null)
            playerInteractor.SetInteractionLocked(freeze);

        // Hide or show main HUD
        if (playerUI == null)
            playerUI = FindFirstObjectByType<PlayerUI>();

        if (playerUI != null)
        {
            if (playerUI.hudRoot != null)
                playerUI.hudRoot.SetActive(!freeze);

            // Close backpack if open
            if (freeze && playerUI.IsBackpackOpen && playerUI.backpackRoot != null)
                playerUI.backpackRoot.SetActive(false);
        }

        // Disable player cast/consume components so input can't trigger actions while frozen
        var playerObj = playerController != null ? playerController.gameObject : GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            var casts = playerObj.GetComponents<PlayerCast>();
            foreach (var c in casts) if (c != null) c.enabled = !freeze;

            var consumes = playerObj.GetComponents<PlayerConsume>();
            foreach (var c in consumes) if (c != null) c.enabled = !freeze;
        }
        Cursor.lockState = freeze ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = freeze;
    }

    private void ClearSpawnedEntries()
    {
        for (int i = spawnedEntries.Count - 1; i >= 0; i--)
        {
            if (spawnedEntries[i] != null)
                Destroy(spawnedEntries[i].gameObject);
        }

        spawnedEntries.Clear();
    }

    // Public helper to indicate whether the picker root is currently shown.
    public bool IsOpen => root != null && root.activeInHierarchy;
}
