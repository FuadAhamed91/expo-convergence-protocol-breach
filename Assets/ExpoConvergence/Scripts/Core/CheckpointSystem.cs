using System.Collections.Generic;
using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>
    /// Ordered respawn points. Each checkpoint is a box zone checked against the courier's position,
    /// so it works with the CharacterController without relying on trigger callbacks.
    /// </summary>
    public class CheckpointSystem : MonoBehaviour
    {
        public struct Zone
        {
            public int order;
            public string label;
            public Bounds bounds;
            public Vector3 spawn;
            public float yaw;
            public int sector;
        }

        readonly List<Zone> zones = new List<Zone>();
        int currentOrder = -1;
        Vector3 spawnPosition;
        float spawnYaw;

        public string CurrentLabel { get; private set; } = "ARRIVAL";

        void Awake() => Game.Checkpoints = this;

        public void SetStart(Vector3 position, float yaw)
        {
            spawnPosition = position;
            spawnYaw = yaw;
        }

        public void AddZone(int order, string label, Bounds bounds, Vector3 spawn, float yaw, int sector)
        {
            zones.Add(new Zone { order = order, label = label, bounds = bounds, spawn = spawn, yaw = yaw, sector = sector });
        }

        void Update()
        {
            var player = Game.Player;
            if (player == null || Game.State == null || Game.State.Current != GameState.Playing) return;
            Vector3 p = player.transform.position + Vector3.up * 0.5f;
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (z.order <= currentOrder || !z.bounds.Contains(p)) continue;
                currentOrder = z.order;
                spawnPosition = z.spawn;
                spawnYaw = z.yaw;
                CurrentLabel = z.label;
                if (z.sector >= 0) Game.Progress?.EnterSector(z.sector);
                GameEvents.RaiseToast("CHECKPOINT  ·  " + z.label, ExpoPalette.Cyan, 2f);
                Game.Audio?.PlayUI("checkpoint");
            }
        }

        public void ForceCheckpoint(int order, string label, Vector3 spawn, float yaw)
        {
            if (order <= currentOrder) return;
            currentOrder = order;
            spawnPosition = spawn;
            spawnYaw = yaw;
            CurrentLabel = label;
        }

        public void RespawnPlayer(bool resetSecurity)
        {
            var player = Game.Player;
            if (player == null) return;
            player.Teleport(spawnPosition, spawnYaw);
            Game.CameraRig?.SnapBehindPlayer();
            if (resetSecurity)
            {
                Game.Detection?.ResetAll();
                SecurityRegistry.ResetAllSensors();
                Game.Passports?.ResetAbilities();
                GameEvents.RaisePlayerRespawned();
            }
        }
    }
}
