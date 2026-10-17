# EXPO CONVERGENCE: PROTOCOL BREACH

**A cinematic third-person stealth game for the Festival of Passports**, built in Unity 6 (URP) with Blender-made landmarks and an original procedural soundtrack.

![Title screen](Docs/Screenshots/title.png)

> Opening night at Expo City Dubai is minutes away. A rogue automated security grid has corrupted every international credential, locked down the exhibition districts and blacked out three landmark displays.
> You are **Tariq**, an elite consular courier carrying the **Sovereign Tri-Drive**: three diplomatic keys, **UAE (gold)**, **France (blue)** and **United Kingdom (emerald)**. Slip through three security-controlled districts, restore the landmarks and reach the **Al Wasl Dome** before the opening ceremony begins.

---

## Play

| Action | Key |
| --- | --- |
| Move | **W A S D** |
| Look | **Mouse** |
| Sprint (uses stamina) | **Left Shift** |
| Dive-slide under barriers | **Space** (while running) |
| Select passport | **1** UAE · **2** France · **3** United Kingdom |
| Use passport ability | **Right-click** |
| Interact with terminals | **Hold E** |
| Pause | **Esc** |

A full run takes about 8 to 10 minutes. The countdown starts at 12:00; restoring a landmark adds 30 seconds, being caught costs 15.

## The three passports

Every district is built around one passport, and the final walk needs all three.

| Passport | Ability | What it does |
| --- | --- | --- |
| **UAE** (gold) | **Mirage Cloak** | 4 s of thermal-signature disruption with golden shimmer, sand and holographic afterimages. Thermal drones, thermal scanners and gold turnstiles cannot see you. Walls still stop you, and optical cameras still see you. |
| **France** (blue) | **Prism Pulse** | A photonic bolt fired from the wrist module. Aim assist locks the camera or scanner nearest the crosshair; the impact freezes it, and every device within 5 m, for 4 s. |
| **United Kingdom** (emerald) | **Chrono Lock** | A Big Ben time-field, announced by a synthesized bell strike, that slows lasers, tempo gates, scanners and sentries around you to 14 % speed. |

Consular terminals only accept the matching passport. Presenting the wrong one is rejected and flagged to the grid.

![Gameplay](Docs/Screenshots/uae-courtyard.png)

## The districts

The route loops around the lagoon and the Al Wasl Dome, which stays in view the whole time and lights a third of its projection shell for every landmark you restore.

1. **Arrival Concourse**: sprint and slide tutorial under the arrival portal.
2. **UAE Pavilion District**: sandstone wind towers and gold mashrabiya screens. Thermal Falcon sentries, thermal scanners and gold turnstiles. Restore the **Burj Khalifa** display.
3. **France Pavilion District**: a Parisian limestone arcade with blue glass. Sweeping optical cameras, a prism scanner guarding the consulate lock, and a mixed thermal/optical grid. Restore the **Eiffel Tower** display.
4. **United Kingdom Pavilion District**: gothic pilasters and emerald clocks. A sweeping laser gantry to slide under, rotating clock-hand lasers and tempo gates. Restore the **Big Ben** display.
5. **Convergence Walk**: every passport counts: cloak, pulse and bend time.
6. **Al Wasl Dome**: insert the Tri-Drive and bring the opening ceremony to light.

![Landmark restoration](Docs/Screenshots/burj-restored.png)

## Stealth systems

- **Detection meter**: sensors fill a shared meter faster at close range; when it fills, the grid goes on **ALERT**. Staying in sight fills the lockdown bar, and a sentry closing in intercepts you. Break line of sight and the grid stands down.
- **Readable threats**: every sensor shows a vision cone and a coloured **ground fan** (idle, suspicious, alert, frozen, slowed), plus HUD arrows pointing to whoever is watching you.
- **Hearing**: sprinting near a sentry makes noise it can hear even from behind.
- **Falcon sentries** patrol, turn towards anything suspicious, chase during alerts, search the last known position and return.
- Optical **cameras** sweep and track; **prism scanners** rotate a narrow scan blade; **laser grids** come static, pulsing, sweeping and rotating; **tempo gates** slam shut; **thermal turnstiles** block anyone not cloaked.
- **Objective waypoint**: a marker with live distance leads to the next terminal or landmark display, and clamps to the screen edge with an arrow when the target is off-screen.
- Checkpoints, respawn, rank on completion (S / A / B / C).

## Built with

- **Unity 6.3 LTS (6000.3.2f1)**, Universal Render Pipeline, Input System, uGUI + TextMeshPro.
- **Everything is assembled from code at runtime**: the world builder lays out Expo City, the courier is a procedural articulated stickman with procedural run, sprint, slide, interact and ability animation, and the HUD and menus are built in script.
- **Blender** (through Blender MCP): the Falcon sentry drone and the three landmark towers, with glow geometry split into height bands so their light climbs from base to spire during restoration.
- **Custom URP shaders**: hologram, energy field, vision cone, beam, night sky and additive particles.
- **Procedural textures** (floor tiles, stone blocks, mashrabiya lattice, city windows, clock face, water normals) generated by the editor setup.
- **Original audio** from an offline synthesizer in `Tools/AudioSynth`: three layered music loops in D minor with a Phrygian-dominant colour (explore, tension, finale), city ambience and 30 sound effects.
- **Built for integrated graphics**: tuned for Intel Iris Xe at 1080p/60 with quality presets and an automatic step-down.

## Open and run

1. Install **Unity 6000.3.2f1** (Unity 6.3 LTS).
2. Open this folder as a project.
3. Run **Expo > 1. Setup Project** once. It creates layers, tunes URP, imports TextMeshPro, generates textures and materials, and creates the scene.
4. Open `Assets/Scenes/ExpoConvergence.unity` and press **Play**.
5. To ship, run **Expo > 2. Build Windows Player**. The build lands in `Builds/Windows/ExpoConvergence.exe`.

To regenerate the soundtrack, run `node Tools/AudioSynth/synth.js` (Node 18+).

## Project layout

```
Assets/ExpoConvergence/
  Scripts/Core        game flow, countdown, checkpoints, progression, bootstrap
  Scripts/Player      courier controller, procedural stickman rig, input
  Scripts/Camera      third-person camera, cinematic director
  Scripts/Passports   passport manager and the three abilities
  Scripts/Security    detection, sentries, cameras, scanners, lasers, gates
  Scripts/World       world builder, landmarks, dome, terminals, mesh generator
  Scripts/Rendering   materials, post-processing, VFX, quality tuner
  Scripts/UI          HUD and menus
  Scripts/Audio       music, effects and voice playback
  Shaders/            custom URP shaders
  Editor/             one-click project setup and build
Assets/Resources/     materials, data assets, audio and Blender models
Tools/AudioSynth/     soundtrack and effects synthesizer
```

---

Made for the **Festival of Passports** student game competition.
