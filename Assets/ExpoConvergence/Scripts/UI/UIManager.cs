using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ExpoConvergence
{
    /// <summary>
    /// All screens and the HUD, built at runtime: title, briefing, HUD (countdown, detection, passports,
    /// stamina, reticle, threat arrows, prompts, toasts, hints), pause, caught, failure and victory.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        RectTransform root;
        CanvasGroup hud, title, briefing, pause, caught, failed, victory, fade;

        // HUD
        TextMeshProUGUI timerText, sectorText, objectiveText, toastText, hintText, interactText, detectionLabel, keysText, fpsText, lockText;
        Image timerBar, detectionFill, lockdownFill, lockdownBack, staminaFill, interactFill, alertVignette, reticleDot, reticleRing, eyeIcon;
        RectTransform interactPanel, hintPanel, lockdownGroup, letterTop, letterBottom;
        readonly Image[] keyDiamonds = new Image[3];
        readonly Image[] arrows = new Image[6];
        CanvasGroup toastGroup, hintGroup, interactGroup;
        float toastTimer, hintTimer, letterbox, letterboxTarget;

        // Objective waypoint.
        RectTransform waypoint;
        CanvasGroup waypointGroup;
        Image waypointIcon, waypointArrow;
        TextMeshProUGUI waypointDistance, waypointLabel;

        class Card
        {
            public RectTransform rt;
            public Image bg, bar, cooldown, glow, locked;
            public TextMeshProUGUI key, country, ability, status;
        }
        readonly Card[] cards = new Card[3];

        // Briefing
        readonly List<TextMeshProUGUI> briefLines = new List<TextMeshProUGUI>();
        Coroutine briefingRoutine;

        // Overlays
        TextMeshProUGUI caughtReason, victoryStats, victoryRank, victoryRankTitle, pauseQuality, titlePrompt;
        Image caughtFlash;
        Coroutine fadeRoutine;

        void Awake() => Game.UI = this;

        void OnEnable()
        {
            GameEvents.Toast += OnToast;
            GameEvents.Hint += OnHint;
            GameEvents.ObjectiveChanged += OnObjective;
            GameEvents.StateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            GameEvents.Toast -= OnToast;
            GameEvents.Hint -= OnHint;
            GameEvents.ObjectiveChanged -= OnObjective;
            GameEvents.StateChanged -= OnStateChanged;
        }

        // ================================================================ build

        public void Build()
        {
            var canvasGo = new GameObject("HUDCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            root = (RectTransform)canvasGo.transform;

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.transform.SetParent(transform, false);
                var module = es.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }

            BuildHud();
            BuildTitle();
            BuildBriefing();
            BuildPause();
            BuildCaught();
            BuildFailed();
            BuildVictory();
            BuildLetterboxAndFade();

            SetGroup(hud, false); SetGroup(title, false); SetGroup(briefing, false); SetGroup(pause, false);
            SetGroup(caught, false); SetGroup(failed, false); SetGroup(victory, false);
        }

        static CanvasGroup Group(Transform parent, string name)
        {
            var rt = UIKit.Stretch(parent, name);
            return rt.gameObject.AddComponent<CanvasGroup>();
        }

        static void SetGroup(CanvasGroup g, bool on)
        {
            if (g == null) return;
            g.alpha = on ? 1f : 0f;
            g.interactable = on;
            g.blocksRaycasts = on;
        }

        void BuildHud()
        {
            hud = Group(root, "HUD");
            var t = hud.transform;
            var panel = new Color(0.02f, 0.035f, 0.07f, 0.62f);

            alertVignette = UIKit.Image(t, "AlertVignette", UISprites.Vignette, new Color(1f, 0.1f, 0.15f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920, 1080));
            ((RectTransform)alertVignette.transform).anchorMin = Vector2.zero;
            ((RectTransform)alertVignette.transform).anchorMax = Vector2.one;
            ((RectTransform)alertVignette.transform).sizeDelta = Vector2.zero;
            // Soft shade along the top edge so the objective and counters read over bright signage.
            UIKit.Image(t, "TopShade", UISprites.VerticalGradient, new Color(0f, 0.005f, 0.02f, 0.5f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(4000, 230));

            // Countdown, top centre.
            var timer = UIKit.Anchored(t, "Timer", new Vector2(0.5f, 1f), new Vector2(0, -24f), new Vector2(420, 120));
            UIKit.Image(timer, "Back", UISprites.Rounded, panel, new Vector2(0.5f, 1f), new Vector2(0, 0), new Vector2(420, 104), true);
            UIKit.Text(timer, "Label", "OPENING CEREMONY IN", 15f, ExpoPalette.TextDim, new Vector2(0.5f, 1f), new Vector2(0, -16f), new Vector2(400, 24), TextAlignmentOptions.Center, false, 9f);
            timerText = UIKit.Text(timer, "Time", "12:00", 52f, Color.white, new Vector2(0.5f, 1f), new Vector2(0, -52f), new Vector2(400, 60), TextAlignmentOptions.Center, true, 6f);
            UIKit.Image(timer, "BarBack", UISprites.White, new Color(1, 1, 1, 0.08f), new Vector2(0.5f, 1f), new Vector2(0, -90f), new Vector2(360, 4));
            timerBar = UIKit.Image(timer, "Bar", UISprites.White, ExpoPalette.Cyan, new Vector2(0.5f, 1f), new Vector2(0, -90f), new Vector2(360, 4));
            timerBar.type = Image.Type.Filled;
            timerBar.fillMethod = Image.FillMethod.Horizontal;

            // Detection meter under the timer.
            var det = UIKit.Anchored(t, "Detection", new Vector2(0.5f, 1f), new Vector2(0, -136f), new Vector2(420, 60));
            eyeIcon = UIKit.Image(det, "Eye", UISprites.Eye, ExpoPalette.TextDim, new Vector2(0.5f, 1f), new Vector2(-172f, -14f), new Vector2(40, 40));
            UIKit.Image(det, "MeterBack", UISprites.Rounded, new Color(1, 1, 1, 0.08f), new Vector2(0.5f, 1f), new Vector2(18f, -10f), new Vector2(300, 10), true);
            detectionFill = UIKit.Image(det, "Meter", UISprites.White, ExpoPalette.Suspicious, new Vector2(0.5f, 1f), new Vector2(18f, -10f), new Vector2(296, 6));
            detectionFill.type = Image.Type.Filled;
            detectionFill.fillMethod = Image.FillMethod.Horizontal;
            detectionLabel = UIKit.Text(det, "State", "UNDETECTED", 15f, ExpoPalette.TextDim, new Vector2(0.5f, 1f), new Vector2(18f, -32f), new Vector2(300, 22), TextAlignmentOptions.Center, true, 10f);
            lockdownGroup = UIKit.Anchored(det, "Lockdown", new Vector2(0.5f, 1f), new Vector2(18f, -54f), new Vector2(300, 14));
            lockdownBack = UIKit.Image(lockdownGroup, "Back", UISprites.White, new Color(1f, 0.1f, 0.1f, 0.15f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 5));
            lockdownFill = UIKit.Image(lockdownGroup, "Fill", UISprites.White, ExpoPalette.Alert, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 5));
            lockdownFill.type = Image.Type.Filled;
            lockdownFill.fillMethod = Image.FillMethod.Horizontal;

            // Sector & objective, top left.
            var obj = UIKit.Anchored(t, "Objective", new Vector2(0f, 1f), new Vector2(40f, -36f), new Vector2(640, 100));
            UIKit.Image(obj, "Accent", UISprites.White, ExpoPalette.Gold, new Vector2(0f, 1f), new Vector2(2f, -36f), new Vector2(4, 64));
            sectorText = UIKit.Text(obj, "Sector", "ARRIVAL CONCOURSE", 17f, ExpoPalette.Gold, new Vector2(0f, 1f), new Vector2(330f, -16f), new Vector2(640, 26), TextAlignmentOptions.Left, true, 7f);
            objectiveText = UIKit.Text(obj, "Text", "", 23f, Color.white, new Vector2(0f, 1f), new Vector2(330f, -50f), new Vector2(640, 60), TextAlignmentOptions.TopLeft, false, 1f);

            // Keys status, top right.
            var keys = UIKit.Anchored(t, "Keys", new Vector2(1f, 1f), new Vector2(-40f, -36f), new Vector2(360, 80));
            keysText = UIKit.Text(keys, "Label", "LANDMARKS RESTORED  0/3", 16f, ExpoPalette.TextDim, new Vector2(1f, 1f), new Vector2(-180f, -14f), new Vector2(360, 24), TextAlignmentOptions.Right, true, 6f);
            for (int i = 0; i < 3; i++)
                keyDiamonds[i] = UIKit.Image(keys, "Key" + i, UISprites.Diamond, ExpoPalette.ForPassport((PassportType)i).WithAlpha(0.25f), new Vector2(1f, 1f), new Vector2(-106f + i * 34f, -52f), new Vector2(26, 26));
            fpsText = UIKit.Text(keys, "Fps", "", 13f, ExpoPalette.TextDim, new Vector2(1f, 1f), new Vector2(-180f, -82f), new Vector2(360, 20), TextAlignmentOptions.Right, false, 3f);

            // Passport cards, bottom centre.
            for (int i = 0; i < 3; i++) cards[i] = BuildCard(t, i);

            // Stamina, bottom left.
            var stam = UIKit.Anchored(t, "Stamina", new Vector2(0f, 0f), new Vector2(40f, 46f), new Vector2(300, 40));
            UIKit.Text(stam, "Label", "STAMINA", 14f, ExpoPalette.TextDim, new Vector2(0f, 0f), new Vector2(150f, 30f), new Vector2(300, 20), TextAlignmentOptions.Left, true, 8f);
            UIKit.Image(stam, "Back", UISprites.Rounded, new Color(1, 1, 1, 0.08f), new Vector2(0f, 0f), new Vector2(140f, 10f), new Vector2(280, 9), true);
            staminaFill = UIKit.Image(stam, "Fill", UISprites.White, new Color(0.75f, 0.92f, 1f), new Vector2(0f, 0f), new Vector2(140f, 10f), new Vector2(276, 5));
            staminaFill.type = Image.Type.Filled;
            staminaFill.fillMethod = Image.FillMethod.Horizontal;

            // Reticle and threat arrows.
            reticleDot = UIKit.Image(t, "Reticle", UISprites.Circle, new Color(1, 1, 1, 0.8f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7, 7));
            reticleRing = UIKit.Image(t, "ReticleRing", UISprites.Ring, ExpoPalette.Blue.WithAlpha(0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54, 54));
            lockText = UIKit.Text(t, "LockText", "", 13f, ExpoPalette.Blue, new Vector2(0.5f, 0.5f), new Vector2(0, 46f), new Vector2(520, 22), TextAlignmentOptions.Center, true, 5f);
            for (int i = 0; i < arrows.Length; i++)
                arrows[i] = UIKit.Image(t, "Threat" + i, UISprites.Arrow, ExpoPalette.Suspicious.WithAlpha(0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34));

            // Objective waypoint (diamond + distance, clamped to the screen edge when off-screen).
            waypoint = UIKit.Anchored(t, "Waypoint", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
            waypointGroup = waypoint.gameObject.AddComponent<CanvasGroup>();
            waypointIcon = UIKit.Image(waypoint, "Icon", UISprites.Diamond, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            UIKit.Image(waypoint, "Glow", UISprites.Glow, new Color(1, 1, 1, 0.25f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 70)).transform.SetAsFirstSibling();
            waypointArrow = UIKit.Image(waypoint, "Arrow", UISprites.Arrow, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 30f), new Vector2(22, 22));
            waypointDistance = UIKit.Text(waypoint, "Distance", "", 15f, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, -28f), new Vector2(160, 20), TextAlignmentOptions.Center, true, 3f);
            waypointLabel = UIKit.Text(waypoint, "Label", "", 12f, ExpoPalette.TextDim, new Vector2(0.5f, 0.5f), new Vector2(0, -46f), new Vector2(320, 18), TextAlignmentOptions.Center, true, 4f);
            waypointGroup.alpha = 0f;

            // Interaction prompt.
            interactPanel = UIKit.Anchored(t, "Interact", new Vector2(0.5f, 0.5f), new Vector2(0, -150f), new Vector2(620, 64));
            interactGroup = interactPanel.gameObject.AddComponent<CanvasGroup>();
            UIKit.Image(interactPanel, "Back", UISprites.Rounded, panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 64), true);
            interactText = UIKit.Text(interactPanel, "Text", "", 21f, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 6f), new Vector2(600, 30), TextAlignmentOptions.Center, true, 5f);
            interactFill = UIKit.Image(interactPanel, "Progress", UISprites.White, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, -20f), new Vector2(560, 4));
            interactFill.type = Image.Type.Filled;
            interactFill.fillMethod = Image.FillMethod.Horizontal;
            interactGroup.alpha = 0f;

            // Toast and hint.
            toastText = UIKit.Text(t, "Toast", "", 30f, Color.white, new Vector2(0.5f, 1f), new Vector2(0, -250f), new Vector2(1400, 44), TextAlignmentOptions.Center, true, 8f);
            toastGroup = toastText.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            hintPanel = UIKit.Anchored(t, "Hint", new Vector2(0.5f, 0f), new Vector2(0, 238f), new Vector2(1100, 48));
            hintGroup = hintPanel.gameObject.AddComponent<CanvasGroup>();
            UIKit.Image(hintPanel, "Back", UISprites.Rounded, new Color(0.02f, 0.035f, 0.07f, 0.72f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 48), true);
            hintText = UIKit.Text(hintPanel, "Text", "", 20f, new Color(0.88f, 0.95f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060, 40), TextAlignmentOptions.Center, false, 2f);
            hintGroup.alpha = 0f;
        }

        Card BuildCard(Transform parent, int i)
        {
            var p = (PassportType)i;
            Color c = ExpoPalette.ForPassport(p);
            var card = new Card();
            card.rt = UIKit.Anchored(parent, "Passport" + i, new Vector2(0.5f, 0f), new Vector2((i - 1) * 256f, 70f), new Vector2(220, 112));
            card.glow = UIKit.Image(card.rt, "Glow", UISprites.Glow, c.WithAlpha(0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 190));
            card.bg = UIKit.Image(card.rt, "Back", UISprites.Rounded, new Color(0.03f, 0.05f, 0.09f, 0.82f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 112), true);
            card.bar = UIKit.Image(card.rt, "Bar", UISprites.White, c, new Vector2(0.5f, 1f), new Vector2(0, -3f), new Vector2(200, 4));
            // Anchored() pivots at the anchor, so these positions are each label's top-left corner.
            card.key = UIKit.Text(card.rt, "Key", (i + 1).ToString(), 30f, c, new Vector2(0f, 1f), new Vector2(12f, -30f), new Vector2(40, 48), TextAlignmentOptions.Center, true, 0f);
            card.country = UIKit.Text(card.rt, "Country", ExpoPalette.CountryName(p), 15f, Color.white, new Vector2(0f, 1f), new Vector2(58f, -26f), new Vector2(150, 22), TextAlignmentOptions.Left, true, 5f);
            card.ability = UIKit.Text(card.rt, "Ability", ExpoPalette.AbilityName(p), 17f, c, new Vector2(0f, 1f), new Vector2(58f, -50f), new Vector2(150, 26), TextAlignmentOptions.Left, true, 2f);
            card.status = UIKit.Text(card.rt, "Status", "READY", 13f, ExpoPalette.TextDim, new Vector2(0.5f, 0f), new Vector2(0, 18f), new Vector2(200, 20), TextAlignmentOptions.Center, true, 8f);
            foreach (var label in new[] { card.country, card.ability, card.status })
            {
                // Shrink long names (e.g. MIRAGE CLOAK, UNITED KINGDOM) to fit the card instead of spilling over.
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.enableAutoSizing = true;
                label.fontSizeMax = label.fontSize;
                label.fontSizeMin = 10f;
            }
            card.cooldown = UIKit.Image(card.rt, "Cooldown", UISprites.Rounded, new Color(0f, 0f, 0f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 112), false);
            card.cooldown.type = Image.Type.Filled;
            card.cooldown.fillMethod = Image.FillMethod.Horizontal;
            card.cooldown.fillOrigin = 1;
            card.locked = UIKit.Image(card.rt, "Locked", UISprites.Rounded, new Color(0.01f, 0.015f, 0.03f, 0.86f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 112), true);
            UIKit.Text(card.locked.transform, "LockLabel", "ENCRYPTED", 16f, ExpoPalette.TextDim, new Vector2(0.5f, 0.5f), new Vector2(0, -8f), new Vector2(200, 24), TextAlignmentOptions.Center, true, 10f);
            UIKit.Text(card.locked.transform, "LockKey", $"{i + 1}  ·  {ExpoPalette.CountryName(p)}", 13f, c.WithAlpha(0.6f), new Vector2(0.5f, 0.5f), new Vector2(0, 18f), new Vector2(200, 20), TextAlignmentOptions.Center, true, 4f);
            return card;
        }

        void BuildTitle()
        {
            title = Group(root, "Title");
            var t = title.transform;
            // The title block lives in the night sky above the dome; the prompt sits low, clear of the lattice.
            // The gradient sprite is opaque at the top; the bottom shade is flipped about its centre.
            UIKit.Image(t, "TopShade", UISprites.VerticalGradient, new Color(0f, 0.005f, 0.02f, 0.86f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(4000, 640));
            var bottomShade = UIKit.Image(t, "BottomShade", UISprites.VerticalGradient, new Color(0, 0, 0, 0.9f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(4000, 400));
            bottomShade.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            bottomShade.rectTransform.anchoredPosition = new Vector2(0, 200f);
            bottomShade.rectTransform.localScale = new Vector3(1, -1, 1);
            // Soft dark glow behind the title so passing landmark beams never wash it out.
            UIKit.Image(t, "TitleBand", UISprites.Glow, new Color(0f, 0.01f, 0.03f, 0.6f), new Vector2(0.5f, 1f), new Vector2(0, -60f), new Vector2(1900, 480));

            var main = UIKit.Text(t, "Title", UIKit.Gradient("EXPO CONVERGENCE", ExpoPalette.Gold, Color.white, ExpoPalette.Blue, Color.white, ExpoPalette.Emerald), 104f, Color.white,
                new Vector2(0.5f, 1f), new Vector2(0, -150f), new Vector2(1800, 130), TextAlignmentOptions.Center, true, 14f);
            main.richText = true;
            UIKit.Text(t, "Subtitle", "PROTOCOL  BREACH", 40f, new Color(0.8f, 0.93f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -282f), new Vector2(1200, 56), TextAlignmentOptions.Center, false, 44f);
            for (int i = 0; i < 3; i++)
            {
                var c = ExpoPalette.ForPassport((PassportType)i);
                UIKit.Image(t, "Chip" + i, UISprites.Rounded, c.WithAlpha(0.9f), new Vector2(0.5f, 1f), new Vector2((i - 1) * 190f, -356f), new Vector2(170, 6), true);
                UIKit.Text(t, "ChipLabel" + i, $"{ExpoPalette.CountryName((PassportType)i)}\n<size=11><alpha=#B0>{ExpoPalette.AbilityName((PassportType)i)}</size>", 13f, c, new Vector2(0.5f, 1f), new Vector2((i - 1) * 190f, -368f), new Vector2(190, 40), TextAlignmentOptions.Center, true, 3f);
            }
            UIKit.Text(t, "Tagline", "Opening night at Expo City Dubai. A rogue security grid has locked down the districts.\nCarry three diplomatic keys through the dark and bring the Festival of Passports back to light.",
                21f, new Color(0.78f, 0.84f, 0.92f), new Vector2(0.5f, 1f), new Vector2(0, -420f), new Vector2(1200, 70), TextAlignmentOptions.Center, false, 1f);
            // A dark pill keeps the prompt readable over the dome's bright base rings.
            var pill = UIKit.Image(t, "PromptPill", UISprites.Rounded, new Color(0.01f, 0.02f, 0.05f, 0.78f), new Vector2(0.5f, 0f), new Vector2(0, 110f), new Vector2(620, 52), true);
            UIKit.Image(pill.transform, "PromptEdge", UISprites.Rounded, ExpoPalette.Cyan.WithAlpha(0.35f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(620, 2), true);
            titlePrompt = UIKit.Text(t, "Prompt", "PRESS  ENTER  OR  CLICK  TO  BEGIN", 24f, Color.white, new Vector2(0.5f, 0f), new Vector2(0, 116f), new Vector2(1200, 40), TextAlignmentOptions.Center, true, 10f);
            UIKit.Text(t, "Controls", "WASD MOVE   ·   MOUSE LOOK   ·   SHIFT SPRINT   ·   SPACE SLIDE   ·   1 / 2 / 3 PASSPORTS   ·   RIGHT-CLICK ABILITY   ·   E INTERACT   ·   ESC PAUSE",
                15f, ExpoPalette.TextDim, new Vector2(0.5f, 0f), new Vector2(0, 70f), new Vector2(1800, 24), TextAlignmentOptions.Center, false, 3f);
            UIKit.Text(t, "Credit", "FESTIVAL OF PASSPORTS  ·  STUDENT GAME COMPETITION  ·  BUILT IN UNITY 6 WITH BLENDER", 13f, new Color(0.5f, 0.58f, 0.7f), new Vector2(0.5f, 0f), new Vector2(0, 34f), new Vector2(1400, 22), TextAlignmentOptions.Center, false, 6f);
        }

        void BuildBriefing()
        {
            briefing = Group(root, "Briefing");
            var t = briefing.transform;
            UIKit.Image(t, "Shade", UISprites.Vignette, new Color(0, 0, 0, 0.8f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2200, 1300));
            UIKit.Text(t, "Header", "OPENING NIGHT  ·  EXPO CITY DUBAI  ·  T-MINUS 12:00", 18f, ExpoPalette.Cyan, new Vector2(0.5f, 0.5f), new Vector2(0, 210f), new Vector2(1400, 30), TextAlignmentOptions.Center, true, 10f);
            string[] lines =
            {
                "A rogue security grid has corrupted every international credential.",
                "The UAE, France and United Kingdom landmark displays have gone dark.",
                "You are <b>TARIQ</b>, consular courier. Your Sovereign Tri-Drive carries three diplomatic keys.",
                "Restore the three landmarks. Reach the <b>Al Wasl Dome</b> before the ceremony begins.",
            };
            for (int i = 0; i < lines.Length; i++)
            {
                var l = UIKit.Text(t, "Line" + i, lines[i], 30f, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 130f - i * 64f), new Vector2(1500, 50), TextAlignmentOptions.Center, false, 1f);
                l.richText = true;
                l.maxVisibleCharacters = 0;
                briefLines.Add(l);
            }
            UIKit.Text(t, "Skip", "ENTER  /  CLICK  TO  SKIP", 15f, ExpoPalette.TextDim, new Vector2(0.5f, 0f), new Vector2(0, 150f), new Vector2(800, 24), TextAlignmentOptions.Center, true, 10f);
        }

        void BuildPause()
        {
            pause = Group(root, "Pause");
            var t = pause.transform;
            UIKit.Image(t, "Dim", UISprites.White, new Color(0.01f, 0.02f, 0.04f, 0.72f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 3000));
            var panel = UIKit.Anchored(t, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 640));
            UIKit.Image(panel, "Back", UISprites.Rounded, new Color(0.03f, 0.05f, 0.09f, 0.95f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 640), true);
            UIKit.Text(panel, "Title", "PAUSED", 46f, Color.white, new Vector2(0.5f, 1f), new Vector2(0, -60f), new Vector2(560, 60), TextAlignmentOptions.Center, true, 20f);
            UIKit.Button(panel, "RESUME", new Vector2(0.5f, 1f), new Vector2(0, -140f), new Vector2(480, 58), ExpoPalette.Cyan, () => Game.State?.Resume());
            UIKit.Button(panel, "RESTART CHECKPOINT", new Vector2(0.5f, 1f), new Vector2(0, -210f), new Vector2(480, 58), ExpoPalette.Gold, () => Game.State?.RestartFromCheckpointFromPause());
            var q = UIKit.Button(panel, "QUALITY", new Vector2(0.5f, 1f), new Vector2(0, -280f), new Vector2(480, 58), ExpoPalette.Emerald, () =>
            {
                QualityAutoTuner.Instance?.Cycle();
                RefreshPauseQuality();
            });
            pauseQuality = q.GetComponentInChildren<TextMeshProUGUI>();
            UIKit.Button(panel, "RESTART MISSION", new Vector2(0.5f, 1f), new Vector2(0, -350f), new Vector2(480, 58), ExpoPalette.Blue, () => Game.State?.RestartMission());
            UIKit.Button(panel, "QUIT", new Vector2(0.5f, 1f), new Vector2(0, -420f), new Vector2(480, 58), ExpoPalette.Alert, () => Game.State?.QuitGame());
            UIKit.Text(panel, "Controls", "WASD move  ·  MOUSE look  ·  SHIFT sprint  ·  SPACE slide\n1 UAE · 2 FRANCE · 3 UK  ·  RIGHT-CLICK ability  ·  E interact",
                16f, ExpoPalette.TextDim, new Vector2(0.5f, 0f), new Vector2(0, 74f), new Vector2(580, 60), TextAlignmentOptions.Center, false, 1f);
        }

        void RefreshPauseQuality()
        {
            if (pauseQuality != null && QualityAutoTuner.Instance != null)
                pauseQuality.text = "QUALITY  ·  " + QualityAutoTuner.PresetNames[(int)QualityAutoTuner.Instance.Current];
        }

        void BuildCaught()
        {
            caught = Group(root, "Caught");
            var t = caught.transform;
            caughtFlash = UIKit.Image(t, "Flash", UISprites.Vignette, new Color(1f, 0.05f, 0.1f, 0.9f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2200, 1300));
            UIKit.Text(t, "Title", "DETECTED", 96f, new Color(1f, 0.3f, 0.32f), new Vector2(0.5f, 0.5f), new Vector2(0, 40f), new Vector2(1400, 120), TextAlignmentOptions.Center, true, 30f);
            caughtReason = UIKit.Text(t, "Reason", "", 24f, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, -40f), new Vector2(1400, 36), TextAlignmentOptions.Center, true, 6f);
            UIKit.Text(t, "Sub", "RETURNING TO LAST CHECKPOINT  ·  15 SECOND PENALTY", 16f, ExpoPalette.TextDim, new Vector2(0.5f, 0.5f), new Vector2(0, -84f), new Vector2(1400, 26), TextAlignmentOptions.Center, false, 8f);
        }

        void BuildFailed()
        {
            failed = Group(root, "Failed");
            var t = failed.transform;
            UIKit.Image(t, "Dim", UISprites.White, new Color(0.02f, 0.0f, 0.02f, 0.8f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 3000));
            UIKit.Text(t, "Title", "CEREMONY COMPROMISED", 70f, ExpoPalette.Alert, new Vector2(0.5f, 0.5f), new Vector2(0, 150f), new Vector2(1600, 90), TextAlignmentOptions.Center, true, 14f);
            UIKit.Text(t, "Sub", "The countdown expired before the Al Wasl Dome was authenticated.", 24f, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 80f), new Vector2(1400, 36), TextAlignmentOptions.Center, false, 1f);
            UIKit.Button(t, "RETRY FROM CHECKPOINT  +2:00", new Vector2(0.5f, 0.5f), new Vector2(0, -20f), new Vector2(560, 60), ExpoPalette.Gold, () => Game.State?.RetryFromCheckpoint());
            UIKit.Button(t, "RESTART MISSION", new Vector2(0.5f, 0.5f), new Vector2(0, -95f), new Vector2(560, 60), ExpoPalette.Blue, () => Game.State?.RestartMission());
            UIKit.Button(t, "QUIT", new Vector2(0.5f, 0.5f), new Vector2(0, -170f), new Vector2(560, 60), ExpoPalette.Alert, () => Game.State?.QuitGame());
        }

        void BuildVictory()
        {
            victory = Group(root, "Victory");
            var t = victory.transform;
            UIKit.Image(t, "Shade", UISprites.Vignette, new Color(0, 0, 0, 0.75f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2200, 1300));
            var head = UIKit.Text(t, "Title", UIKit.Gradient("PROTOCOL RESTORED", ExpoPalette.Gold, Color.white, ExpoPalette.Blue, Color.white, ExpoPalette.Emerald), 92f, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0, 250f), new Vector2(1800, 120), TextAlignmentOptions.Center, true, 14f);
            head.richText = true;
            UIKit.Text(t, "Sub", "THE OPENING CEREMONY IS LIVE  ·  THE FESTIVAL OF PASSPORTS BEGINS", 22f, new Color(0.85f, 0.94f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, 175f), new Vector2(1600, 34), TextAlignmentOptions.Center, false, 8f);
            UIKit.Image(t, "StatsPanel", UISprites.Rounded, new Color(0.02f, 0.035f, 0.07f, 0.86f), new Vector2(0.5f, 0.5f), new Vector2(0, 0f), new Vector2(1060, 330), true);
            victoryRank = UIKit.Text(t, "Rank", "S", 140f, ExpoPalette.Gold, new Vector2(0.5f, 0.5f), new Vector2(-300f, 20f), new Vector2(300, 170), TextAlignmentOptions.Center, true, 0f);
            victoryRankTitle = UIKit.Text(t, "RankTitle", "GHOST DIPLOMAT", 22f, Color.white, new Vector2(0.5f, 0.5f), new Vector2(-300f, -78f), new Vector2(420, 30), TextAlignmentOptions.Center, true, 8f);
            victoryStats = UIKit.Text(t, "Stats", "", 26f, Color.white, new Vector2(0.5f, 0.5f), new Vector2(160f, 10f), new Vector2(620, 240), TextAlignmentOptions.Left, false, 2f);
            victoryStats.richText = true;
            victoryStats.lineSpacing = 18f;
            UIKit.Button(t, "PLAY AGAIN", new Vector2(0.5f, 0.5f), new Vector2(-150f, -240f), new Vector2(280, 60), ExpoPalette.Emerald, () => Game.State?.RestartMission());
            UIKit.Button(t, "QUIT", new Vector2(0.5f, 0.5f), new Vector2(150f, -240f), new Vector2(280, 60), ExpoPalette.Alert, () => Game.State?.QuitGame());
        }

        void BuildLetterboxAndFade()
        {
            letterTop = UIKit.Rect(root, "LetterTop", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0, 0));
            letterTop.gameObject.AddComponent<Image>().color = Color.black;
            letterBottom = UIKit.Rect(root, "LetterBottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0, 0));
            letterBottom.gameObject.AddComponent<Image>().color = Color.black;
            fade = Group(root, "Fade");
            var img = fade.gameObject.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
            fade.alpha = 1f;
            fade.blocksRaycasts = false;
            Fade(0f, 1.6f);
        }

        // ================================================================ API

        public void ShowBriefing(bool show)
        {
            SetGroup(title, false);
            SetGroup(briefing, show);
            if (show) { if (briefingRoutine != null) StopCoroutine(briefingRoutine); briefingRoutine = StartCoroutine(TypeBriefing()); }
        }

        IEnumerator TypeBriefing()
        {
            foreach (var l in briefLines) l.maxVisibleCharacters = 0;
            yield return new WaitForSeconds(0.6f);
            foreach (var l in briefLines)
            {
                l.ForceMeshUpdate();
                int total = l.textInfo.characterCount;
                for (int c = 0; c <= total; c++)
                {
                    l.maxVisibleCharacters = c;
                    if (c % 3 == 0) Game.Audio?.PlayUI("type");
                    yield return new WaitForSeconds(0.018f);
                }
                yield return new WaitForSeconds(0.55f);
            }
        }

        public void ShowCaught(string reason)
        {
            caughtReason.text = reason;
            SetGroup(caught, true);
        }

        public void HideCaught() => SetGroup(caught, false);

        public void ShowFailed()
        {
            SetGroup(hud, false);
            SetGroup(failed, true);
        }

        public void HideFailed() { SetGroup(failed, false); SetGroup(hud, true); }

        public void ShowVictory()
        {
            SetGroup(hud, false);
            var gs = Game.State;
            float remaining = Game.Countdown != null ? Game.Countdown.Remaining : 0f;
            int caughtCount = gs != null ? gs.TimesCaught : 0;
            int alerts = gs != null ? gs.AlertsTriggered : 0;
            string rank, rankTitle;
            Color rankColor;
            if (caughtCount == 0 && alerts == 0) { rank = "S"; rankTitle = "GHOST DIPLOMAT"; rankColor = ExpoPalette.Gold; }
            else if (caughtCount == 0 && alerts <= 2) { rank = "A"; rankTitle = "SILENT ENVOY"; rankColor = ExpoPalette.Emerald; }
            else if (caughtCount <= 2) { rank = "B"; rankTitle = "TRUSTED COURIER"; rankColor = ExpoPalette.Blue; }
            else { rank = "C"; rankTitle = "ROOKIE COURIER"; rankColor = Color.white; }
            victoryRank.text = rank;
            victoryRank.color = rankColor;
            victoryRankTitle.text = rankTitle;
            string dim = "#8CA3C7";
            victoryStats.text =
                $"<color={dim}>CEREMONY TIME LEFT</color>   {CountdownManager.Format(remaining)}\n" +
                $"<color={dim}>MISSION TIME</color>   {CountdownManager.Format(gs != null ? gs.MissionElapsed : 0f)}\n" +
                $"<color={dim}>TIMES CAUGHT</color>   {caughtCount}\n" +
                $"<color={dim}>ALERTS TRIGGERED</color>   {alerts}\n" +
                $"<color={dim}>LANDMARKS RESTORED</color>   3 / 3";
            SetGroup(victory, true);
        }

        public void SetLetterbox(bool on) => letterboxTarget = on ? 1f : 0f;

        public void Fade(float alpha, float duration)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeTo(alpha, duration));
        }

        IEnumerator FadeTo(float target, float duration)
        {
            float start = fade.alpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                fade.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            fade.alpha = target;
        }

        // ================================================================ events

        void OnStateChanged(GameState from, GameState to)
        {
            SetGroup(title, to == GameState.Title);
            SetGroup(pause, to == GameState.Paused);
            if (to == GameState.Paused) RefreshPauseQuality();
            bool showHud = to == GameState.Playing || to == GameState.Paused || to == GameState.Caught || to == GameState.Cinematic;
            SetGroup(hud, showHud);
            if (to == GameState.Cinematic && hud != null) hud.alpha = 0f;
        }

        void OnToast(string message, Color color, float seconds)
        {
            if (toastText == null) return;
            toastText.text = message;
            toastText.color = Color.Lerp(color, Color.white, 0.15f);
            toastTimer = seconds;
        }

        void OnHint(string text, float seconds)
        {
            if (hintText == null) return;
            hintText.text = text;
            hintTimer = seconds;
        }

        void OnObjective(string sector, string objective)
        {
            if (sectorText == null) return;
            sectorText.text = sector;
            objectiveText.text = objective;
            Color c = Color.white;
            if (sector.Contains("UAE")) c = ExpoPalette.Gold;
            else if (sector.Contains("FRANCE")) c = ExpoPalette.Blue;
            else if (sector.Contains("KINGDOM")) c = ExpoPalette.Emerald;
            else if (sector.Contains("ARRIVAL")) c = ExpoPalette.Cyan;
            sectorText.color = c;
            var accent = sectorText.transform.parent.Find("Accent");
            if (accent != null) accent.GetComponent<Image>().color = c;
        }

        // ================================================================ per-frame HUD

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var state = Game.State != null ? Game.State.Current : GameState.Boot;

            if (state == GameState.Title)
            {
                if (titlePrompt != null) titlePrompt.alpha = 0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * 3f);
                if (PlayerInput.AnyConfirmPressed) Game.State.BeginBriefing();
            }
            if (state == GameState.Cinematic && hud != null) hud.alpha = Mathf.MoveTowards(hud.alpha, 0f, dt * 4f);
            else if (state == GameState.Playing && hud != null) hud.alpha = Mathf.MoveTowards(hud.alpha, 1f, dt * 3f);

            letterbox = Mathf.MoveTowards(letterbox, letterboxTarget, dt * 2.2f);
            float barH = Mathf.SmoothStep(0f, 1f, letterbox) * 120f;
            if (letterTop != null) { letterTop.sizeDelta = new Vector2(0, barH); letterBottom.sizeDelta = new Vector2(0, barH); }

            toastTimer -= dt;
            if (toastGroup != null) toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, toastTimer > 0f ? 1f : 0f, dt * 4f);
            if (state == GameState.Playing) hintTimer -= dt;
            if (hintGroup != null) hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, hintTimer > 0f ? 1f : 0f, dt * 3f);

            if (state == GameState.Caught && caughtFlash != null)
                caughtFlash.color = new Color(1f, 0.05f, 0.1f, 0.55f + 0.35f * Mathf.Sin(Time.unscaledTime * 10f));

            if (hud == null || hud.alpha <= 0.01f) return;
            UpdateTimer();
            UpdateDetection(dt);
            UpdatePassports();
            UpdateInteraction(dt);
            UpdateReticle();
            UpdateThreatArrows();
            UpdateWaypoint(dt);

            if (Game.Player != null && staminaFill != null)
            {
                staminaFill.fillAmount = Game.Player.StaminaFraction;
                staminaFill.color = Game.Player.Exhausted ? ExpoPalette.Alert : new Color(0.75f, 0.92f, 1f);
            }
            if (Game.Progress != null)
            {
                keysText.text = $"LANDMARKS RESTORED  {Game.Progress.RestoredCount}/3";
                for (int i = 0; i < 3; i++)
                    keyDiamonds[i].color = ExpoPalette.ForPassport((PassportType)i).WithAlpha(Game.Progress.IsRestored(i) ? 1f : 0.22f);
            }
            if (fpsText != null && QualityAutoTuner.Instance != null)
                fpsText.text = $"{QualityAutoTuner.Instance.SmoothedFps:0} FPS  ·  {QualityAutoTuner.PresetNames[(int)QualityAutoTuner.Instance.Current]}";
        }

        void UpdateTimer()
        {
            var cd = Game.Countdown;
            if (cd == null) return;
            timerText.text = CountdownManager.Format(cd.Remaining);
            timerBar.fillAmount = cd.Fraction;
            bool urgent = cd.Remaining < 60f;
            Color c = urgent ? Color.Lerp(ExpoPalette.Alert, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 8f)) : Color.white;
            timerText.color = c;
            timerBar.color = urgent ? ExpoPalette.Alert : ExpoPalette.Cyan;
        }

        void UpdateDetection(float dt)
        {
            var d = Game.Detection;
            if (d == null) return;
            detectionFill.fillAmount = Mathf.Lerp(detectionFill.fillAmount, d.Meter, 1f - Mathf.Exp(-12f * dt));
            string label;
            Color c;
            switch (d.Level)
            {
                case AlertLevel.Alert: label = "ALERT  ·  BREAK LINE OF SIGHT"; c = ExpoPalette.Alert; break;
                case AlertLevel.Searching: label = "SEARCHING"; c = new Color(1f, 0.55f, 0.3f); break;
                case AlertLevel.Suspicious: label = "SUSPICIOUS"; c = ExpoPalette.Suspicious; break;
                default: label = Game.Player != null && Game.Player.IsCloaked ? "MIRAGE CLOAK ACTIVE" : "UNDETECTED"; c = Game.Player != null && Game.Player.IsCloaked ? ExpoPalette.Gold : ExpoPalette.TextDim; break;
            }
            detectionLabel.text = label;
            detectionLabel.color = c;
            detectionFill.color = d.Level == AlertLevel.Alert ? ExpoPalette.Alert : Color.Lerp(ExpoPalette.Suspicious, ExpoPalette.Alert, d.Meter);
            eyeIcon.color = d.SeenThisFrame ? Color.Lerp(ExpoPalette.Suspicious, ExpoPalette.Alert, d.Meter) : c.WithAlpha(0.8f);
            float pulse = d.SeenThisFrame ? 1f + 0.15f * Mathf.Sin(Time.time * 20f) : 1f;
            eyeIcon.transform.localScale = Vector3.one * pulse;
            bool alert = d.Level == AlertLevel.Alert;
            lockdownGroup.gameObject.SetActive(alert);
            lockdownFill.fillAmount = d.Lockdown;
            float vig = alert ? 0.32f + 0.12f * Mathf.Sin(Time.time * 6f) : d.Meter * 0.18f;
            alertVignette.color = new Color(1f, 0.08f, 0.12f, Mathf.Lerp(alertVignette.color.a, vig, 1f - Mathf.Exp(-6f * dt)));
        }

        void UpdatePassports()
        {
            var pm = Game.Passports;
            if (pm == null) return;
            for (int i = 0; i < 3; i++)
            {
                var p = (PassportType)i;
                var card = cards[i];
                bool unlocked = pm.IsUnlocked(p);
                bool active = pm.Active == p;
                card.locked.gameObject.SetActive(!unlocked);
                card.key.enabled = unlocked;
                card.country.enabled = unlocked;
                card.ability.enabled = unlocked;
                card.status.enabled = unlocked;
                float targetScale = active ? 1.08f : 0.94f;
                card.rt.localScale = Vector3.Lerp(card.rt.localScale, Vector3.one * targetScale, 0.2f);
                Color c = ExpoPalette.ForPassport(p);
                card.glow.color = c.WithAlpha(active ? 0.32f : 0f);
                card.bar.color = active ? c : c.WithAlpha(0.45f);
                var ability = pm.Ability(p);
                float cd = pm.CooldownFraction(p);
                card.cooldown.fillAmount = ability.IsRunning ? 0f : cd;
                if (!unlocked) card.status.text = "";
                else if (ability.IsRunning) { card.status.text = "ACTIVE"; card.status.color = c; }
                else if (cd > 0f) { card.status.text = $"RECHARGING  {pm.CooldownRemaining(p):0.0}s"; card.status.color = ExpoPalette.TextDim; }
                else { card.status.text = active ? "READY  ·  RIGHT-CLICK" : "READY"; card.status.color = active ? Color.white : ExpoPalette.TextDim; }
            }
        }

        void UpdateInteraction(float dt)
        {
            var term = ConsularGateTerminal.Focused;
            bool show = term != null && Game.State != null && Game.State.Current == GameState.Playing;
            interactGroup.alpha = Mathf.MoveTowards(interactGroup.alpha, show ? 1f : 0f, dt * 6f);
            if (!show) return;
            bool correct = term.anyPassport || (Game.Passports != null && Game.Passports.Active == term.required);
            interactText.text = correct ? term.PromptText : $"PRESENT {ExpoPalette.CountryName(term.required)} PASSPORT  ·  PRESS {(int)term.required + 1}";
            interactText.color = correct ? Color.white : ExpoPalette.Suspicious;
            interactFill.fillAmount = term.Progress;
            interactFill.color = term.AccentColor;
        }

        void UpdateReticle()
        {
            var pm = Game.Passports;
            bool france = pm != null && pm.Active == PassportType.France && pm.IsUnlocked(PassportType.France);
            var prism = pm != null ? pm.Ability(PassportType.France) as PrismPulseAbility : null;
            var target = france && prism != null ? prism.LockTarget : null;
            Color ringColor = target != null ? ExpoPalette.Blue : Color.white;
            reticleRing.color = Color.Lerp(reticleRing.color, ringColor.WithAlpha(france ? (target != null ? 0.95f : 0.35f) : 0f), 0.25f);
            float s = target != null ? 1f + 0.08f * Mathf.Sin(Time.time * 10f) : 1f;
            reticleRing.transform.localScale = Vector3.one * s;
            if (target != null && Game.CameraRig != null)
            {
                Vector3 sp = Game.CameraRig.Cam.WorldToScreenPoint(target.TargetPoint);
                var canvasScale = root.localScale.x;
                Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Vector2 offset = ((Vector2)sp - center) / Mathf.Max(0.0001f, canvasScale);
                ((RectTransform)reticleRing.transform).anchoredPosition = Vector2.Lerp(((RectTransform)reticleRing.transform).anchoredPosition, offset, 0.35f);
                lockText.text = "LOCKED  ·  " + target.DisplayName;
            }
            else
            {
                ((RectTransform)reticleRing.transform).anchoredPosition = Vector2.Lerp(((RectTransform)reticleRing.transform).anchoredPosition, Vector2.zero, 0.35f);
                lockText.text = france ? "AIM AT A CAMERA OR SCANNER" : "";
            }
            lockText.color = target != null ? ExpoPalette.Blue : ExpoPalette.TextDim;
        }

        void UpdateWaypoint(float dt)
        {
            var target = Game.World != null ? Game.World.CurrentObjective : null;
            var rig = Game.CameraRig;
            bool show = target != null && rig != null && Game.Player != null && Game.State != null && Game.State.Current == GameState.Playing;
            float dist = show ? Vector3.Distance(Game.Player.transform.position, target.transform.position) : 0f;
            if (show && dist < 3.5f) show = false;   // the interaction prompt takes over up close
            waypointGroup.alpha = Mathf.MoveTowards(waypointGroup.alpha, show ? 1f : 0f, dt * 4f);
            if (!show) return;

            Color c = target.AccentColor;
            waypointIcon.color = c;
            waypointArrow.color = c;
            waypointDistance.text = $"{dist:0} m";
            waypointLabel.text = target.title;

            Vector3 world = target.transform.position + Vector3.up * 2.8f;
            Vector3 sp = rig.Cam.WorldToScreenPoint(world);
            bool behind = sp.z < 0f;
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 offset = ((Vector2)sp - center) / Mathf.Max(0.0001f, root.localScale.x);
            if (behind) offset = -offset;
            Vector2 limit = root.rect.size * 0.5f - new Vector2(80f, 90f);
            bool clamped = behind || Mathf.Abs(offset.x) > limit.x || Mathf.Abs(offset.y) > limit.y;
            if (clamped)
            {
                if (offset.sqrMagnitude < 1f) offset = Vector2.down;
                float scale = Mathf.Min(limit.x / Mathf.Max(0.001f, Mathf.Abs(offset.x)), limit.y / Mathf.Max(0.001f, Mathf.Abs(offset.y)));
                offset *= scale;
            }
            waypoint.anchoredPosition = Vector2.Lerp(waypoint.anchoredPosition, offset, 1f - Mathf.Exp(-14f * dt));
            // Near an on-screen objective its own holo title is readable, so the label bows out.
            waypointLabel.alpha = clamped ? 1f : Mathf.Clamp01((dist - 12f) / 8f);
            waypointArrow.enabled = clamped;
            if (clamped)
            {
                float ang = Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg;
                waypointArrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -ang);
                waypointArrow.rectTransform.anchoredPosition = offset.normalized * 30f;
            }
        }

        void UpdateThreatArrows()
        {
            var d = Game.Detection;
            var cam = Game.CameraRig != null ? Game.CameraRig.transform : null;
            int used = 0;
            if (d != null && cam != null && Game.Player != null)
            {
                foreach (var s in d.Watchers)
                {
                    if (s == null || used >= arrows.Length) break;
                    Vector3 to = s.transform.position - Game.Player.transform.position;
                    Vector3 local = cam.InverseTransformDirection(to);
                    float ang = Mathf.Atan2(local.x, local.z);
                    var rt = (RectTransform)arrows[used].transform;
                    rt.anchoredPosition = new Vector2(Mathf.Sin(ang), Mathf.Cos(ang)) * 150f;
                    rt.localRotation = Quaternion.Euler(0, 0, -ang * Mathf.Rad2Deg);
                    Color c = d.Level == AlertLevel.Alert ? ExpoPalette.Alert : ExpoPalette.Suspicious;
                    arrows[used].color = c.WithAlpha(0.9f);
                    used++;
                }
            }
            for (int i = used; i < arrows.Length; i++)
                arrows[i].color = arrows[i].color.WithAlpha(Mathf.MoveTowards(arrows[i].color.a, 0f, 0.08f));
        }
    }
}
