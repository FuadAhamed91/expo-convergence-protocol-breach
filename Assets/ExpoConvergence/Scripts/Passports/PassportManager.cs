using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>
    /// The Sovereign Tri-Drive: which diplomatic keys are authenticated, which passport is presented,
    /// and each ability's cooldown. Keys 1/2/3 switch passports; right-click fires the selected ability.
    /// </summary>
    public class PassportManager : MonoBehaviour
    {
        public PassportDefinition[] definitions = new PassportDefinition[3];
        public readonly bool[] unlocked = { true, false, false };
        public PassportType Active { get; private set; } = PassportType.UAE;

        readonly float[] cooldown = new float[3];
        PassportAbilityBase[] abilities;
        StickmanRig rig;

        void Awake()
        {
            Game.Passports = this;
            for (int i = 0; i < 3; i++)
            {
                var type = (PassportType)i;
                if (definitions[i] == null) definitions[i] = Resources.Load<PassportDefinition>("ExpoData/Passport_" + type);
                if (definitions[i] == null) definitions[i] = PassportDefinition.CreateDefault(type);
            }
            abilities = new PassportAbilityBase[]
            {
                gameObject.AddComponent<MirageCloakAbility>(),
                gameObject.AddComponent<PrismPulseAbility>(),
                gameObject.AddComponent<ChronoLockAbility>(),
            };
            for (int i = 0; i < 3; i++) abilities[i].Init(definitions[i], this);
        }

        void Start()
        {
            rig = GetComponent<StickmanRig>();
            rig?.SetPassport(Active, unlocked);
        }

        public PassportDefinition Definition(PassportType t) => definitions[(int)t];
        public PassportAbilityBase Ability(PassportType t) => abilities[(int)t];
        public bool IsUnlocked(PassportType t) => unlocked[(int)t];
        public float CooldownRemaining(PassportType t) => cooldown[(int)t];
        public float CooldownFraction(PassportType t) => Mathf.Clamp01(cooldown[(int)t] / Mathf.Max(0.01f, definitions[(int)t].cooldown));

        void Update()
        {
            if (Game.State == null || Game.State.Current != GameState.Playing) return;
            for (int i = 0; i < 3; i++) if (cooldown[i] > 0f) cooldown[i] = Mathf.Max(0f, cooldown[i] - Time.deltaTime);

            int pressed = PlayerInput.PassportPressed;
            if (pressed >= 0) Select((PassportType)pressed);
            if (PlayerInput.AbilityPressed) TryActivate();
        }

        public void Select(PassportType t)
        {
            if (!unlocked[(int)t])
            {
                Game.Audio?.PlayUI("deny");
                string source = t == PassportType.France ? "Restore the Burj Display to decrypt it" : "Restore the Eiffel Display to decrypt it";
                GameEvents.RaiseToast($"{ExpoPalette.CountryName(t)} KEY ENCRYPTED  ·  {source}", ExpoPalette.TextDim, 2.4f);
                return;
            }
            if (t == Active) return;
            Active = t;
            Game.Audio?.PlayUI("passport_switch");
            rig?.SetPassport(Active, unlocked);
            VfxFactory.Ring(transform.position + Vector3.up * 0.05f, ExpoPalette.ForPassport(t), 1.6f, 0.45f);
            GameEvents.RaisePassportChanged(t);
        }

        public void TryActivate()
        {
            int i = (int)Active;
            var ability = abilities[i];
            if (cooldown[i] > 0f || ability.IsRunning)
            {
                Game.Audio?.PlayUI("deny");
                return;
            }
            if (!ability.Activate()) { Game.Audio?.PlayUI("deny"); return; }
            cooldown[i] = definitions[i].cooldown + (ability.IsRunning ? definitions[i].duration : 0f);
            GameEvents.RaiseAbilityActivated(Active);
        }

        public void Unlock(PassportType t)
        {
            if (unlocked[(int)t]) return;
            unlocked[(int)t] = true;
            GameEvents.RaisePassportUnlocked(t);
            Select(t);
            rig?.SetPassport(Active, unlocked);
        }

        /// <summary>Clears cooldowns and running effects (respawn).</summary>
        public void ResetAbilities()
        {
            for (int i = 0; i < 3; i++)
            {
                cooldown[i] = 0f;
                abilities[i].Cancel();
            }
        }
    }
}
