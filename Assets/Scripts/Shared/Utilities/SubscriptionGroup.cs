using System;
using System.Collections.Generic;

namespace Shared.Utilities
{
    public sealed class SubscriptionGroup
    {
        private readonly List<Action> unbindActions = new();

        public void Add(Action bind, Action unbind)
        {
            bind?.Invoke();
            if (unbind != null)
                unbindActions.Add(unbind);
        }

        public void UnbindAll()
        {
            for (var i = unbindActions.Count - 1; i >= 0; i--)
                unbindActions[i]?.Invoke();

            unbindActions.Clear();
        }
    }
}
