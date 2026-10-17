using System.Collections.Generic;
using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>Something the France Prism Pulse can freeze.</summary>
    public interface IPrismTarget
    {
        Vector3 TargetPoint { get; }
        bool IsPrismDisableable { get; }
        bool IsDisabled { get; }
        string DisplayName { get; }
        void PrismDisable(float seconds);
    }

    /// <summary>Something the UK Chrono Lock field slows down.</summary>
    public interface IChronoAffectable
    {
        Vector3 ChronoPosition { get; }
        void SetChronoScale(float scale);
    }

    /// <summary>Anything that must return to its initial state when the courier respawns.</summary>
    public interface IResettable
    {
        void ResetState();
    }

    public static class SecurityRegistry
    {
        public static readonly List<SecuritySensor> Sensors = new List<SecuritySensor>();
        public static readonly List<IPrismTarget> PrismTargets = new List<IPrismTarget>();
        public static readonly List<IChronoAffectable> ChronoTargets = new List<IChronoAffectable>();
        public static readonly List<IResettable> Resettables = new List<IResettable>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Sensors.Clear(); PrismTargets.Clear(); ChronoTargets.Clear(); Resettables.Clear();
        }

        public static void Register(object o)
        {
            if (o is SecuritySensor s && !Sensors.Contains(s)) Sensors.Add(s);
            if (o is IPrismTarget p && !PrismTargets.Contains(p)) PrismTargets.Add(p);
            if (o is IChronoAffectable c && !ChronoTargets.Contains(c)) ChronoTargets.Add(c);
            if (o is IResettable r && !Resettables.Contains(r)) Resettables.Add(r);
        }

        public static void Unregister(object o)
        {
            if (o is SecuritySensor s) Sensors.Remove(s);
            if (o is IPrismTarget p) PrismTargets.Remove(p);
            if (o is IChronoAffectable c) ChronoTargets.Remove(c);
            if (o is IResettable r) Resettables.Remove(r);
        }

        public static void ResetAllSensors()
        {
            for (int i = 0; i < Resettables.Count; i++) Resettables[i]?.ResetState();
        }
    }
}
