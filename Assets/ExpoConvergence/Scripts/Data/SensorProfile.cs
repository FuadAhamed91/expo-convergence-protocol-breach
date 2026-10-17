using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>Tuning for one class of security device.</summary>
    [CreateAssetMenu(menuName = "Expo Convergence/Sensor Profile", fileName = "Sensor_")]
    public class SensorProfile : ScriptableObject
    {
        public SensorKind kind = SensorKind.Thermal;
        public float range = 12f;
        [Range(5f, 170f)] public float fieldOfView = 55f;
        [Tooltip("Detection added per second at point-blank range.")]
        public float detectionRate = 1.25f;
        [Tooltip("Hears a sprinting courier within this radius, even from behind.")]
        public float hearingRadius = 5.5f;
        public float moveSpeed = 3f;
        public float chaseSpeed = 6.2f;

        public static SensorProfile Create(SensorKind kind, float range, float fov, float rate, float hearing = 0f, float move = 3f, float chase = 6.2f)
        {
            var p = CreateInstance<SensorProfile>();
            p.kind = kind; p.range = range; p.fieldOfView = fov; p.detectionRate = rate;
            p.hearingRadius = hearing; p.moveSpeed = move; p.chaseSpeed = chase;
            return p;
        }
    }
}
