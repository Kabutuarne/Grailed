using UnityEngine;
using UnityEngine.UI;

public class EnemyNameHealthUI : MonoBehaviour
{
    public EnemyStats target;
    public Text nameText; // assign a Text (or TMP via wrapper)
    public Slider healthSlider;
    public Vector3 worldOffset = new Vector3(0f, 2f, 0f);
    public bool billboardToCamera = true;

    Camera cam;

    void Start()
    {
        cam = Camera.main;
        if (target == null)
            target = GetComponentInParent<EnemyStats>();
        if (target != null && nameText != null)
            nameText.text = target.monsterName;
        UpdateBarImmediate();
    }

    void LateUpdate()
    {
        if (target == null) return;
        UpdateBarImmediate();

        if (billboardToCamera && cam != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
        }
    }

    void UpdateBarImmediate()
    {
        if (healthSlider != null && target != null && target.maxHealth > 0f)
        {
            healthSlider.value = Mathf.Clamp01(target.health / target.maxHealth);
        }
        if (target != null)
        {
            transform.position = target.transform.position + worldOffset;
        }
    }
}
