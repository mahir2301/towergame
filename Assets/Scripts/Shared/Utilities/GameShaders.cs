using UnityEngine;

namespace Shared.Utilities
{
    public static class GameShaders
    {
        private static Shader _lit;
        private static Shader _fallback;

        public static Shader Lit => _lit ??= Shader.Find("Universal Render Pipeline/Lit");
        public static Shader Fallback => _fallback ??= Shader.Find("Sprites/Default");

        public static Material CreateLitMaterial(Color color)
        {
            var shader = Lit != null ? Lit : Fallback;
            var mat = new Material(shader);
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetColor("_BaseColor", color);
            return mat;
        }
    }
}
