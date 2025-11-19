using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public string itemName;
    public GameObject itemPrefab; // for equipping

    public void OnPickedUp()
    {
        gameObject.SetActive(false);
    }
}
