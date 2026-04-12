using UnityEngine;

namespace Utilities
{
    public static class PrefabHelper
    {
        public static void DisableForPreview(GameObject instance)
        {
            foreach (var mb in instance.GetComponentsInChildren<MonoBehaviour>())
                mb.enabled = false;

            foreach (var col in instance.GetComponentsInChildren<Collider>())
                col.enabled = false;

            foreach (var rb in instance.GetComponentsInChildren<Rigidbody>())
                rb.isKinematic = true;
        }
    }
}
