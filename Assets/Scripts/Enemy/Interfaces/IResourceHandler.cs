public interface IResourceHandler
{
    void Heal(float amount);
    void RestoreMana(float amount);
    void RestoreEnergy(float amount);

    void ClampResources();
}