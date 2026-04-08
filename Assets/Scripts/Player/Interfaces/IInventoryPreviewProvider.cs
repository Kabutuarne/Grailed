using UnityEngine;

public interface IInventoryPreviewProvider
{
    GameObject PreviewPrefab { get; }
    Vector3 PreviewRotation { get; }
    float PreviewScale { get; }
}