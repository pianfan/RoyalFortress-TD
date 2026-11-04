using UnityEngine;

namespace RoyalFortress.GateOfDead
{
    /// <summary>Individually placed runes follow the original synchronized glow cycle.</summary>
    public sealed class GateAlignedRunes : MonoBehaviour
    {
        public GateOfDeadPresentation presentation;
        public SpriteRenderer[] upper;
        public SpriteRenderer[] lower;
        [Range(0, 1)] public float upperOpacity = .65f;
        [Range(0, 1)] public float lowerOpacity = .60f;
        private void LateUpdate()
        {
            if (!presentation) return;
            SetAlpha(upper, presentation.RuneAlpha(false) * upperOpacity);
            SetAlpha(lower, presentation.RuneAlpha(true) * lowerOpacity);
        }
        private static void SetAlpha(SpriteRenderer[] sprites, float alpha)
        {
            if (sprites == null) return;
            foreach (var sprite in sprites)
            {
                if (!sprite) continue;
                var color = sprite.color; color.a = alpha; sprite.color = color;
            }
        }
    }
}
