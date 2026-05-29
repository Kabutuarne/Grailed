using UnityEngine;

public class PlayerConsume : MonoBehaviour
{
    private PlayerInputActions input;
    public CastUI castUI;

    private PlayerStats stats;
    private PlayerInventory inventory;
    private PlayerUI playerUI;

    private bool isConsuming;
    private float consumeTotal;
    private float consumeElapsed;
    private int backpackIndex = -1;
    private bool wasHolding;

    private enum Source
    {
        None,
        Hand,
        Backpack
    }

    private Source source = Source.None;

    private void Awake()
    {
        input = new PlayerInputActions(); // izveido ievades karti patēriņa darbībām.
        stats = GetComponent<PlayerStats>(); // saglabā spēlētāja statistikas komponentu ātruma pielāgojumam.
        inventory = GetComponent<PlayerInventory>(); // saglabā inventāra sistēmu priekšmetu piekļuvei.
        playerUI = FindFirstObjectByType<PlayerUI>(); // atrod UI objektu, kas seko somas stāvoklim.
    }

    private void OnEnable()
    {
        if (input == null)
            input = new PlayerInputActions(); // izveido ievadi no jauna, ja objekts bija atspējots.

        input.Enable(); // atkal sāk klausīties patēriņa ievadi.
    }

    private void OnDisable()
    {
        if (input != null)
            input.Disable(); // pārtrauc klausīties ievadi, kad šis komponents ir atspējots.
    }

    void Update()
    {
        if (isConsuming)
        {
            UpdateConsumeProgress(); // turpina patēriņa taimeri, kamēr darbība ir aktīva.
            return;
        }

        bool holdingNow = ReadConsumeHeld(); // pārbauda, vai spēlētājs joprojām tur patēriņa taustiņu.
        if (holdingNow && !wasHolding)
            TryStartConsume(); // sāk patērēt tikai tad, kad poga tiek nospiesta no jauna.

        wasHolding = holdingNow; // atceras iepriekšējo ievades stāvokli robežu noteikšanai.
    }

    public bool TryStartConsumeFromHand()
    {
        if (inventory == null || inventory.rightHandItem == null)
            return false; // rokā nav nekā, ko patērēt.

        ConsumableItem consumable = inventory.rightHandItem.GetComponent<ConsumableItem>();
        if (consumable == null)
            return false; // rokā esošais objekts nav patēriņa priekšmets.

        if (IsMovementBlockingConsume() || IsBackpackOpen() || !ReadConsumeHeld())
            return false; // pārtrauc patēriņu, ja kustība, soma vai ievade to bloķē.

        BeginConsume(consumable, Source.Hand, -1); // sāk lietot priekšmetu no rokas.
        return true;
    }

    public bool TryStartConsumeFromBackpack(int index)
    {
        if (inventory == null || inventory.backpack == null || index < 0 || index >= inventory.backpack.Length)
            return false; // somas indekss nav derīgs.

        GameObject item = inventory.backpack[index];
        if (item == null)
            return false; // somas slots ir tukšs.

        ConsumableItem consumable = item.GetComponent<ConsumableItem>();
        if (consumable == null)
            return false; // izvēlētais slots nesatur patēriņa priekšmetu.

        if (IsMovementBlockingConsume() || !ReadConsumeHeld())
            return false; // nepatērē, kamēr kusties vai ja poga vairs netiek turēta.

        BeginConsume(consumable, Source.Backpack, index); // sāk patērēt no somas slota.
        return true;
    }

    private void TryStartConsume()
    {
        if (IsBackpackOpen())
        {
            InventorySlotUI slot = InventorySlotUI.HoveredSlot; // pārbauda, kurš somas slots ir pašlaik piefiksēts.
            if (slot != null &&
                slot.slotType == InventorySlotUI.SlotType.Backpack &&
                slot.slotIndex >= 0)
            {
                TryStartConsumeFromBackpack(slot.slotIndex); // patērē no izvēlētā somas priekšmeta.
            }

            return;
        }

        TryStartConsumeFromHand(); // citādi patērē priekšmetu, kas ir rokā.
    }

    private void BeginConsume(ConsumableItem consumable, Source consumeSource, int sourceBackpackIndex)
    {
        float baseTime = Mathf.Max(0.01f, consumable.baseConsumeTime); // nodrošina, ka priekšmetam ir derīgs pamata ilgums.
        float speed = stats != null ? Mathf.Max(0.01f, stats.consumeSpeedMultiplier) : 1f; // pielieto spēlētāja ātruma reizinātāju.

        consumeTotal = Mathf.Max(0.01f, baseTime / speed); // aprēķina faktiskā patēriņa ilgumu.
        consumeElapsed = 0f; // atiestata progresa skaitītāju darbības sākumā.
        source = consumeSource; // atceras, no kurienes priekšmets nāk.
        backpackIndex = sourceBackpackIndex; // atceras, kurš somas slots tika izmantots.
        isConsuming = true; // norāda, ka patēriņa darbība ir aktīva.

        if (castUI != null)
            castUI.Show(consumeTotal, consumeTotal); // parāda patēriņa progresa joslu.
    }

    private void UpdateConsumeProgress()
    {
        if (ShouldCancelConsume())
        {
            CancelConsume(); // pārtrauc patēriņa darbību, ja spēlētājs atbrīvo ievadi vai kustas.
            return;
        }

        consumeElapsed += Time.deltaTime; // palielina pašreizējās patēriņa darbības taimeri.
        float remaining = Mathf.Max(0f, consumeTotal - consumeElapsed); // aprēķina atlikušo laiku.

        if (castUI != null)
            castUI.UpdateRemaining(consumeTotal, remaining); // atjaunina vizuālo progresa displeju.

        if (consumeElapsed >= consumeTotal)
            FinishConsume(); // pabeidz patēriņa darbību, kad taimeris beidzas.
    }

    private bool ShouldCancelConsume()
    {
        if (!ReadConsumeHeld())
            return true; // nekavējoties pārtrauc, ja patēriņa taustiņš vairs netiek turēts.

        if (IsMovementBlockingConsume())
            return true; // pārtrauc, ja patēriņa laikā sākas kustība.

        // rokas patēriņš tiek atcelts, ja patēriņa laikā atveras soma.
        if (source == Source.Hand && IsBackpackOpen())
            return true; // neļauj lietot rokas priekšmetus, kad somas UI ir aktīvs.

        return false;
    }

    private void FinishConsume()
    {
        if (inventory != null)
        {
            if (source == Source.Hand)
                inventory.ConsumeFromHand(gameObject); // patērē priekšmetu, kas atrodas rokā.
            else if (source == Source.Backpack && backpackIndex >= 0)
                inventory.ConsumeFromBackpack(backpackIndex, gameObject); // patērē priekšmetu no somas slota.
        }

        if (castUI != null)
            castUI.Complete(); // atzīmē, ka patēriņa UI ir pabeigts.

        ResetConsumeState(); // notīra pagaidu patēriņa stāvokli nākamajai darbībai.
    }

    private void CancelConsume()
    {
        if (castUI != null)
            castUI.Interrupt(); // parāda, ka patēriņa darbība tika pārtraukta.

        ResetConsumeState(); // notīra visu patēriņa progresu un avota izsekošanu.
    }

    private void ResetConsumeState()
    {
        isConsuming = false; // aptur patēriņa ciklu.
        consumeTotal = 0f; // notīra kopējo ilgumu.
        consumeElapsed = 0f; // notīra pagājušo progresu.
        source = Source.None; // noņem pašreizējo avota atsauci.
        backpackIndex = -1; // noņem somas slota indeksu.
    }

    private bool ReadConsumeHeld()
    {
        if (input == null)
            return false; // ievades karte vēl nav pieejama.

        try { return input.Player.Consume.ReadValue<float>() > 0f; } // atgriež true, kamēr patēriņa ievade tiek turēta.
        catch { return false; } // droši atgriežas, ja ievades sistēma nav pieejama.
    }

    private bool IsMovementBlockingConsume()
    {
        Vector2 moveInput = Vector2.zero;

        try { moveInput = input.Player.Move.ReadValue<Vector2>(); } // nolasa pašreizējo kustības ievadi.
        catch { }

        return moveInput.sqrMagnitude > 0.0001f; // jebkuru nozīmīgu kustības ievadi uzskata par bloķētāju.
    }

    private bool IsBackpackOpen()
    {
        return playerUI != null && playerUI.IsBackpackOpen; // pārbauda, vai somas UI pašlaik ir atvērts.
    }
}