namespace Shared.Utilities
{
    public static class ReferenceValidator
    {
        public static bool Validate(out string issue, params (object reference, string name)[] checks)
        {
            for (var i = 0; i < checks.Length; i++)
            {
                if (checks[i].reference != null)
                    continue;

                issue = $"{checks[i].name} is not assigned.";
                return false;
            }

            issue = null;
            return true;
        }
    }
}
