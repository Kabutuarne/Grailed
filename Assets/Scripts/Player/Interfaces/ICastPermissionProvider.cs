public interface ICastPermissionProvider
{
    bool CanCastWhileMoving { get; }
    bool CanCastWhileHit { get; }
}