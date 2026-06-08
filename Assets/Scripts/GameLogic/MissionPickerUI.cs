using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionPickerUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private Canvas rootCanvas; // assign the Canvas component of root

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
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerInteractor playerInteractor;
    [SerializeField] private PlayerUI playerUI;

    private MissionData selectedMission;
    private MissionEntryUI selectedEntry;
    private readonly List<MissionEntryUI> spawnedEntries = new List<MissionEntryUI>();

    private void Awake()
    {
        if (root != null) root.SetActive(false);
        if (startMissionButton != null) startMissionButton.onClick.AddListener(OnStartMissionPressed);
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
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
        {
            root.SetActive(true);
            if (rootCanvas != null) rootCanvas.enabled = true; // ensure visible
        }
        FreezePlayer(true);
        RefreshMissionList();
    }

    public void Hide()
    {
        if (root != null)
        {
            root.SetActive(false);
        }
        FreezePlayer(false);
    }

    public void RefreshMissionList()
    {
        if (missionListParent == null || missionEntryPrefab == null) return;
        ClearSpawnedEntries();
        selectedMission = null;
        selectedEntry = null;
        UpdateSelectedMissionDetails();

        var missions = MissionManager.Instance?.GetUnlockedMissions() ?? Array.Empty<MissionData>();
        bool hasMissions = missions.Count > 0;
        if (noMissionsMessage != null) noMissionsMessage.SetActive(!hasMissions);

        foreach (var mission in missions)
        {
            var entry = Instantiate(missionEntryPrefab, missionListParent);
            entry.Setup(mission, OnMissionEntrySelected);
            spawnedEntries.Add(entry);
        }
    }

    private void ClearSpawnedEntries()
    {
        foreach (var e in spawnedEntries) if (e != null) Destroy(e.gameObject);
        spawnedEntries.Clear();
    }

    private void OnMissionEntrySelected(MissionData mission, MissionEntryUI entry)
    {
        selectedMission = mission;
        selectedEntry = entry;
        foreach (var e in spawnedEntries) if (e != null) e.SetSelected(e == selectedEntry);
        UpdateSelectedMissionDetails();
    }

    private void UpdateSelectedMissionDetails()
    {
        if (selectedTitleText != null)
            selectedTitleText.text = selectedMission != null ? selectedMission.title : "Select a mission";
        if (selectedDescriptionText != null)
            selectedDescriptionText.text = selectedMission != null ? selectedMission.description : "Choose one of the available missions to see the details.";
        if (selectedAssignedByText != null)
            selectedAssignedByText.text = selectedMission != null ? $"By: {selectedMission.assignedBy}" : "";
        if (selectedDifficultyText != null)
            selectedDifficultyText.text = selectedMission != null ? selectedMission.DifficultyRoman : "";
        if (startMissionButton != null)
            startMissionButton.interactable = selectedMission != null;
    }

    private void OnStartMissionPressed()
    {
        if (selectedMission == null) return;
        MissionManager.Instance?.StartMission(selectedMission);
        Hide();
    }

    private void FreezePlayer(bool freeze)
    {
        if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();
        if (playerInteractor == null) playerInteractor = FindFirstObjectByType<PlayerInteractor>();

        if (playerController != null) playerController.SetControlLocked(freeze);
        if (playerInteractor != null) playerInteractor.SetInteractionLocked(freeze);

        if (playerUI == null) playerUI = FindFirstObjectByType<PlayerUI>();
        if (playerUI != null)
        {
            if (playerUI.hudRoot != null) playerUI.hudRoot.SetActive(true); // always keep HUD on

            if (freeze && playerUI.IsBackpackOpen && playerUI.backpackRoot != null)
                playerUI.backpackRoot.SetActive(false);
        }

        var playerObj = playerController != null ? playerController.gameObject : GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            foreach (var c in playerObj.GetComponents<PlayerCast>()) if (c != null) c.enabled = !freeze;
            foreach (var c in playerObj.GetComponents<PlayerConsume>()) if (c != null) c.enabled = !freeze;
        }

        var pauseMenu = FindFirstObjectByType<PauseMenuManager>();
        if (pauseMenu == null || !pauseMenu.IsPaused)
        {
            Cursor.lockState = freeze ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = freeze;
        }
    }

    public bool IsOpen
    {
        get
        {
            if (rootCanvas != null)
                return rootCanvas.enabled && rootCanvas.gameObject.activeInHierarchy;
            return root != null && root.activeInHierarchy;
        }
    }
}