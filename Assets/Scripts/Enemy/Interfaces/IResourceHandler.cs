public interface IResourceHandler
{
    /// <summary>Positive = heal, negative = damage.</summary>
    void ModifyHealth(float amount);
    /// <summary>Positive = restore, negative = spend.</summary>
    void ModifyMana(float amount);
    /// <summary>Positive = restore, negative = consume.</summary>
    void ModifyEnergy(float amount);
    /// <summary>Clamp all resources to their current maximums.</summary>
    void ClampResources();
}