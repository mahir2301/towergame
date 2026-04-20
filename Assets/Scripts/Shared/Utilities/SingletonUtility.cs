using UnityEngine;
using System;

namespace Shared.Utilities
{
    public static class SingletonUtility
    {
        public static bool TryAssign<T>(T current, T candidate, Action<T> assign) where T : Component
        {
            if (current != null && current != candidate)
            {
                UnityEngine.Object.Destroy(candidate.gameObject);
                return false;
            }

            assign?.Invoke(candidate);
            return true;
        }

        public static void ClearIfCurrent<T>(T current, T candidate, Action clear) where T : class
        {
            if (ReferenceEquals(current, candidate))
                clear?.Invoke();
        }
    }
}
