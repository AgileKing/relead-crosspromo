using UnityEngine;

namespace Relead.CrossPromo
{
    /// <summary>
    /// The offline slideshow content, bundled in the package (Runtime/Resources/ReleadCrossPromoFallback.asset).
    /// A few JPG screenshots per game (~1080 px), name and store ids. Refreshed by a new package version only.
    /// </summary>
    [CreateAssetMenu(menuName = "Relead/Cross-Promo Fallback Game Set", fileName = "ReleadCrossPromoFallback")]
    public sealed class FallbackGameSet : ScriptableObject
    {
        public const string ResourceName = "ReleadCrossPromoFallback";
        public FallbackGame[] games = new FallbackGame[0];
    }
}
