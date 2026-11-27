using UnityEditor;
using UnityEngine;

namespace Relead.CrossPromo.Editor
{
    // Must stay in a file named CrossPromoBuildSettings.cs: Unity only binds a ScriptableObject asset to
    // its script when file name and class name match. Otherwise the asset is saved with no script.
    /// <summary>Per-game build settings. Create via Assets > Create > Relead > Cross-Promo Build Settings (one per project).</summary>
    [CreateAssetMenu(menuName = "Relead/Cross-Promo Build Settings", fileName = "CrossPromoBuildSettings")]
    public sealed class CrossPromoBuildSettings : ScriptableObject
    {
        [Tooltip("This game's own scheme from the catalog games[].urlScheme, e.g. relead-g01.")]
        public string hostUrlScheme;
        [Tooltip("Android package names of other Relead games to declare in <queries> (reserve ahead where known).")]
        public string[] androidPackages = new string[0];

        public static CrossPromoBuildSettings Find()
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(CrossPromoBuildSettings).Name);
            if (guids.Length == 0) return null;
            if (guids.Length > 1) Debug.LogWarning("[CrossPromo] more than one CrossPromoBuildSettings asset; using the first.");
            return AssetDatabase.LoadAssetAtPath<CrossPromoBuildSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
