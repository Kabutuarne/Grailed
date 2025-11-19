using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public Transform rightHand;
    public Transform leftHand;

    public GameObject[] backpack = new GameObject[6];
    public GameObject rightHandItem;
    public GameObject leftHandItem;

    public void PickupToBackpack(GameObject item)
    {
        for (int i = 0; i < backpack.Length; i++)
        {
            if (backpack[i] == null)
            {
                backpack[i] = item;
                item.SetActive(false);
                return;
            }
        }

        Debug.Log("Backpack full. Cope.");
    }

    public void EquipRight(GameObject itemPrefab)
    {
        if (rightHandItem != null) Destroy(rightHandItem);
        rightHandItem = Instantiate(itemPrefab, rightHand);
    }

    public void EquipLeft(GameObject itemPrefab)
    {
        if (leftHandItem != null) Destroy(leftHandItem);
        leftHandItem = Instantiate(itemPrefab, leftHand);
    }
}
