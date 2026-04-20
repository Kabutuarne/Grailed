using UnityEngine;
using UnityEngine.EventSystems;


public class AttributeIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject target;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (target != null) target.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (target != null) target.SetActive(false);
    }
}
