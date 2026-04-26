namespace Shared.Runtime
{
    public class AntennaRuntime : TowerRuntime
    {
        [UnityEngine.Header("Relay")]
        [UnityEngine.SerializeField] private int range = 10;

        public int Range => range;
    }
}
