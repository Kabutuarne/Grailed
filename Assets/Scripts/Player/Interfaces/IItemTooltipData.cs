using System.Collections.Generic;
using UnityEngine;

public interface IItemTooltipData
{
    string TooltipTitle { get; }
    Color TooltipTitleColor { get; }
    IReadOnlyList<ItemTooltipRowData> GetTooltipRows();
}