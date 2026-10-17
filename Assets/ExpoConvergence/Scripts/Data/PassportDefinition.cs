using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>Tuning for one diplomatic key and its passport ability.</summary>
    [CreateAssetMenu(menuName = "Expo Convergence/Passport Definition", fileName = "Passport_")]
    public class PassportDefinition : ScriptableObject
    {
        public PassportType type;
        public string countryName = "UAE";
        public string abilityName = "MIRAGE CLOAK";
        [TextArea] public string abilityDescription = "";
        [ColorUsage(false, true)] public Color color = Color.white;
        [Tooltip("Seconds the ability stays active (0 for instant abilities).")]
        public float duration = 3f;
        public float cooldown = 6f;
        [Tooltip("Range in meters (targeting range or field radius).")]
        public float range = 9f;

        public static PassportDefinition CreateDefault(PassportType type)
        {
            var d = CreateInstance<PassportDefinition>();
            d.type = type;
            d.name = "Passport_" + type;
            d.countryName = ExpoPalette.CountryName(type);
            d.abilityName = ExpoPalette.AbilityName(type);
            d.color = ExpoPalette.ForPassport(type);
            switch (type)
            {
                case PassportType.UAE:
                    d.duration = 4f; d.cooldown = 5f; d.range = 0f;
                    d.abilityDescription = "Disrupts your thermal signature. Thermal drones, scanners and gold turnstiles cannot see you.";
                    break;
                case PassportType.France:
                    d.duration = 4f; d.cooldown = 5f; d.range = 32f;
                    d.abilityDescription = "Fires a photonic prism bolt that freezes cameras and scanners near the impact.";
                    break;
                default:
                    d.duration = 4.5f; d.cooldown = 8f; d.range = 9f;
                    d.abilityDescription = "Projects a Big Ben time-field that slows lasers, gates and sentries around you.";
                    break;
            }
            return d;
        }
    }
}
