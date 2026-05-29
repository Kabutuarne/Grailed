using UnityEngine;

// pārvalda burvju sākšanu un turpināšanu, kad spēlētājs tur Cast ievadi.
[RequireComponent(typeof(PlayerInventory))]
public class PlayerCast : MonoBehaviour
{
    private PlayerInputActions input;
    private PlayerInventory inventory;
    private PlayerStats stats;

    [Header("UI")]
    public CastUI castUI;
    public PlayerUI playerUI;

    private bool isCasting;
    private ScrollItem currentScroll;
    private ISpellCastDefinition currentSpell;
    private IInstantCastSpell currentInstantSpell;
    private IChanneledCastSpell currentChanneledSpell;
    private IChannelCastRuntime currentChannelRuntime;
    private float elapsed;
    private float castTimeActual;

    void Awake()
    {
        input = new PlayerInputActions(); // izveido ievades darbības, kas tiek izmantotas burvju lietošanai.
        inventory = GetComponent<PlayerInventory>(); // saglabā inventāra komponentu, lai varētu meklēt priekšmetus.
        stats = GetComponent<PlayerStats>(); // saglabā statistikas komponentu, lai izmantotu burvju ātruma reizinātāju.

        if (playerUI == null)
            playerUI = Object.FindFirstObjectByType<PlayerUI>(); // atrod UI objektu, ja tas nav piešķirts.
    }

    void OnEnable()
    {
        if (input == null)
            input = new PlayerInputActions(); // ja nepieciešams, izveido ievades karti no jauna.

        input.Enable(); // sāk klausīties burvju ievades komandas.
    }

    void OnDisable()
    {
        if (input != null)
            input.Disable(); // pārtrauc ievades klausīšanos, kad šis skripts ir atslēgts.
    }

    void Update()
    {
        bool holdingCast = ReadButtonHeld(); // pārbauda, vai burvju poga pašlaik tiek turēta.
        bool backpackOpen = IsBackpackOpen(); // neļauj burt, kamēr ir atvērts somas UI.
        bool movementBlocksCast = IsMovementBlockingCast(); // pārbauda, vai kustība bloķē burvju lietošanu.

        if (holdingCast && !backpackOpen && !movementBlocksCast)
        {
            if (!isCasting)
                TryBeginCastFromHand(); // sāk burvju lietošanu no rokā esošā priekšmeta.
            else
                ContinueCasting(); // turpina aktīvo burvju vai kanāla procesu.
        }
        else if (isCasting)
        {
            CancelCast(); // pārtrauc burvju lietošanu, ja nosacījumi vairs nav derīgi.
        }
    }

    public void OnDamageTaken()
    {
        if (!isCasting)
            return; // nav ko pārtraukt, ja burvju lietošana nav aktīva.

        if (HasCastPermission(provider => provider.CanCastWhileHit))
            return; // daži aksesuāri var atļaut burvēt arī pēc trieciena.

        CancelCast(); // pārtrauc burvju lietošanu, ja ievainojums to aptur.
    }

    private void TryBeginCastFromHand()
    {
        ScrollItem scroll = GetCurrentHandScroll(); // nosaka priekšmetu, kas pašlaik ir rokā
        if (scroll == null)
            return; // rokā nav derīga Scroll priekšmeta

        if (!scroll.TryGetSpell(out currentSpell, out currentInstantSpell, out currentChanneledSpell))
            return; //ja rokā esošais priekšmets nesatur Spell datus

        currentScroll = scroll; // atceras, kurš Scroll tiek lietots
        currentChannelRuntime = null; // atiestata aktīvo Channel darbību, pirms sākas jauns burvju process
        elapsed = 0f; // atiestata taimeri.

        float castSpeed = stats != null ? Mathf.Max(0.01f, stats.castSpeedMultiplier) : 1f; // pielieto pašreizējo Cast ātruma reizinātāju
        castTimeActual = Mathf.Max(0.01f, currentSpell.CastTime / castSpeed); // aprēķina faktisko Cast laiku, pielietojot ātruma reizinātāju

        isCasting = true; // norāda, ka Spell uzsākšanas process ir sācies
        if (castUI != null)
            castUI.Show(castTimeActual, castTimeActual); // parāda Spell progresu UI
    }

    private void ContinueCasting() //Izsaukts katru Update() kadru, kamēr Spell izsaukšana ir aktīva
    {
        if (ShouldCancelCurrentCast())
        {
            CancelCast(); // pārtrauc Spell izsaukšanu, ja spēlētājs vairs nevar turpināt
            return;
        }
        // kad Cast jau darbojas, PlayerCast tikai uztur stāvokli un UI
        if (currentChannelRuntime != null)
        {
            if (castUI != null)
                castUI.ShowChanneling(); // turpina rādīt Cast UI, kamēr efekts ir aktīvs
            return;
        }
        elapsed += Time.deltaTime; // palielina atskaiti
        float remaining = castTimeActual - elapsed; // aprēķina atlikušo laiku
        if (remaining > 0f) //parbauda vai laiks ir vel atlicis
        {
            if (castUI != null)
                castUI.UpdateRemaining(castTimeActual, remaining); // atjaunina taimera displeju
            return;
        }
        if (currentChanneledSpell != null) // ja ir Channel tipa burvestība (kas pēc izsaukšanas turpina darboties)
        {
            currentChannelRuntime = currentChanneledSpell.StartChannel(gameObject); // sāk Channel efektu un saglabā atsauci uz tā izpildi
            if (currentChannelRuntime == null)
            {
                CancelCast(); // pārtrauc, ja Channel nevar sākt
                return;
            }
            if (castUI != null)
                castUI.ShowChanneling(); // parāda Channel stāvokli, ja tas sācies veiksmīgi

            return;
        }
        if (currentInstantSpell != null) // ja ir Instant tipa burvestība (kas iedarbojas vienreiz pēc izsaukšanas)
        {
            bool castSucceeded = currentInstantSpell.TryCast(gameObject); // mēģina izpildīt Instant Spell
            if (!castSucceeded)
            {
                CancelCast(); // pārtrauc, ja Spell izsaukšana neizdevās
                return;
            }
            EndCast(); // pabeidz Spell izsaukšanu, kad Instant efekts ir pielietots
        }
    }

    private bool ShouldCancelCurrentCast()
    {
        if (!ReadButtonHeld())
            return true; // pārtrauc, ja spēlētājs atlaiž darbibas pogu

        if (IsBackpackOpen())
            return true; // pārtrauc, ja darbibas laikā atvērās inventāra UI

        if (IsMovementBlockingCast())
            return true; // pārtrauc, ja kustība būtu jāaptur
                         // (piem. spēlētājs sāk skriet UN viņam nav aksesuāra, kas ļautu veikt darbību kustībā)

        if (currentScroll == null)
            return true; // pārtrauc, ja Scroll vairs nav derīgs (piem. tas tika patērēts vai iznīcināts)

        return GetCurrentHandScroll() != currentScroll; // pārtrauc, ja spēlētājs nomainīja priekšmetu rokā, vai izmeta
    }

    private ScrollItem GetCurrentHandScroll()
    {
        if (inventory == null || inventory.rightHandItem == null)
            return null; // rokā nav neviena priekšmeta

        GameObject handItem = inventory.rightHandItem;

        ScrollItem scroll = handItem.GetComponent<ScrollItem>();
        if (scroll != null)
            return scroll; // priekšmets rokā ir pats Scroll

        WandItem wand = handItem.GetComponent<WandItem>();
        if (wand != null)
            return wand.GetSelectedScroll(); // Wand var saturēt Scroll iekšēji
                                             // tāpēc pārbauda vai ir kāds izvēlēts Scroll priekšmets tajā Wand

        return null; // priekšmets nav izsaucams Spell
    }

    private bool IsMovementBlockingCast()
    {
        Vector2 moveInput = Vector2.zero;

        try { moveInput = input.Player.Move.ReadValue<Vector2>(); } // nolasa pašreizējo kustības ievadi.
        catch { }

        return moveInput.sqrMagnitude > 0.0001f &&
               !HasCastPermission(provider => provider.CanCastWhileMoving); // kustība bloķē burvju lietošanu, ja aksesuārs to neaizliedz.
    }

    private bool HasCastPermission(System.Func<ICastPermissionProvider, bool> predicate)
    {
        if (inventory == null || inventory.accessories == null)
            return false; // bez aksesuāriem nav īpašu atļauju.

        foreach (GameObject accessoryObject in inventory.accessories)
        {
            if (accessoryObject == null)
                continue; // izlaiž tukšus aksesuāru slotus.

            ICastPermissionProvider provider = GetInterfaceFromObject<ICastPermissionProvider>(accessoryObject);
            if (provider != null && predicate(provider))
                return true; // atgriež true, ja kāds aksesuārs dod pieprasīto atļauju.
        }

        return false;
    }

    private T GetInterfaceFromObject<T>(GameObject target) where T : class
    {
        if (target == null)
            return null; // bez mērķa nav ko pārbaudīt.

        MonoBehaviour[] behaviours = target.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is T typed)
                return typed; // atgriež pirmo komponentu, kas ievieš pieprasīto interfeisu.
        }

        return null;
    }

    private bool ReadButtonHeld()
    {
        if (input == null)
            return false; // ievades karte vēl nav pieejama.

        try { return input.Player.Cast.ReadValue<float>() > 0f; } // atgriež true, kamēr burvju poga tiek turēta.
        catch { return false; } // droši atgriežas, ja ievades sistēma nav pieejama.
    }

    private bool IsBackpackOpen()
    {
        return playerUI != null && playerUI.IsBackpackOpen; // pārbauda, vai inventāra UI pašlaik ir atvērts.
    }

    private void CancelCast()
    {
        StopChannelRuntime(); // aptur jebkuru aktīvo kanālu pirms atcelšanas.

        if (castUI != null)
            castUI.Interrupt(); // parāda, ka burvju lietošana tika pārtraukta.

        ResetCastState(); // notīra aktīvo burvju stāvokli.
    }

    private void EndCast()
    {
        StopChannelRuntime(); // aptur kanāla objektu pirms pabeigšanas.

        if (castUI != null)
            castUI.Complete(); // atzīmē, ka burvju UI ir pabeigts.

        ResetCastState(); // notīra visu burvju stāvokli nākamajai darbībai.
    }

    private void StopChannelRuntime()
    {
        if (currentChannelRuntime == null)
            return; // pašlaik nekas nekanalējas.

        currentChannelRuntime.StopChannel(); // aptur notiekošo kanāla efektu.
        currentChannelRuntime = null; // notīra izpildes atsauci.
    }

    private void ResetCastState()
    {
        isCasting = false; // beidz burvju stāvokli.
        currentScroll = null; // notīra pašreizējo rullīša atsauci.
        currentSpell = null; // notīra pašreizējo burvju definīciju.
        currentInstantSpell = null; // notīra uzreizējā burvju atsauci.
        currentChanneledSpell = null; // notīra kanāla burvju atsauci.
        elapsed = 0f; // atiestata pagājušo laiku.
        castTimeActual = 0f; // atiestata gala burvju ilgumu.
    }
}