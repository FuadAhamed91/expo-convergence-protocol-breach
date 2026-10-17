using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>
    /// Tracks which landmark displays are restored and which keys are authenticated.
    /// Sector order: 0 arrival, 1 UAE, 2 France, 3 UK, 4 convergence walk, 5 Al Wasl Dome.
    /// </summary>
    public class SectorProgressionManager : MonoBehaviour
    {
        public static readonly string[] SectorTitles =
        {
            "ARRIVAL CONCOURSE",
            "SECTOR 01  ·  UAE PAVILION DISTRICT",
            "SECTOR 02  ·  FRANCE PAVILION DISTRICT",
            "SECTOR 03  ·  UNITED KINGDOM PAVILION DISTRICT",
            "SECTOR 04  ·  CONVERGENCE WALK",
            "AL WASL DOME",
        };

        public static readonly string[] SectorObjectives =
        {
            "Follow the gold guide lights to the UAE consular gate",
            "Slip past the thermal grid and reach the Burj Display",
            "Blind the optical grid and reach the Eiffel Display",
            "Bend time through the laser grid and reach the Big Ben Display",
            "Return to Al Wasl Plaza. All three seals are open",
            "Insert the Sovereign Tri-Drive into the authentication pedestal",
        };

        readonly bool[] restored = new bool[3];
        public int CurrentSector { get; private set; } = -1;
        public int RestoredCount { get; private set; }
        public bool IsRestored(int index) => index >= 0 && index < 3 && restored[index];
        public bool AllRestored => RestoredCount >= 3;

        void Awake() => Game.Progress = this;

        public void EnterSector(int sector)
        {
            if (sector <= CurrentSector) return;
            CurrentSector = sector;
            GameEvents.RaiseObjective(SectorTitles[sector], SectorObjectives[sector]);
            if (sector >= 1 && sector <= 3) Game.Audio?.PlayUI("sector");
        }

        public void SetObjective(string objective)
        {
            int s = Mathf.Max(0, CurrentSector);
            GameEvents.RaiseObjective(SectorTitles[s], objective);
        }

        /// <summary>Called by a landmark display after its activation cinematic.</summary>
        public void RestoreLandmark(int index)
        {
            if (index < 0 || index > 2 || restored[index]) return;
            restored[index] = true;
            RestoredCount++;
            GameEvents.RaiseLandmarkRestored(index);
            Game.World?.OnLandmarkRestored(index);
            Game.Countdown?.Bonus(30f);

            if (index < 2)
            {
                var next = (PassportType)(index + 1);
                Game.Passports?.Unlock(next);
                GameEvents.RaiseToast($"{ExpoPalette.CountryName(next)} KEY DECRYPTED  ·  {ExpoPalette.AbilityName(next)} ONLINE", ExpoPalette.ForPassport(next), 4.5f);
                // Unlock() equips the new passport straight away, so the hint confirms rather than instructs.
                GameEvents.RaiseHint($"{ExpoPalette.CountryName(next)} passport equipped  ·  RIGHT-CLICK for {ExpoPalette.AbilityName(next)}  ·  switch anytime with 1 / 2 / 3", 8f);
                SetObjective(index == 0 ? "Head east through the arcade into the France district" : "Head south into the United Kingdom district");
            }
            else
            {
                GameEvents.RaiseToast("ALL THREE KEYS AUTHENTICATED  ·  AL WASL SEAL OPEN", Color.white, 4.5f);
                EnterSector(4);
            }
        }
    }
}
