using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoyalFortress.GateOfDead
{
    /// <summary>Replaces the former main-portal lane with temporary spawns on the existing roads.</summary>
    public sealed class GatePortalReinforcements : MonoBehaviour
    {
        public WaveGameController game;
        public int[] portalWaves = { 3, 5, 7 };
        [Min(0)] public int leftRoadIndex = 5, rightRoadIndex = 7;
        public Sprite[] portalFrames;
        public Vector2 portalScale = new Vector2(.40f, .32f);
        private SpriteRenderer leftPortal, rightPortal;

        private void Awake()
        {
            if (!game || !game.routeA || !game.routeB || !game.routeC || game.waves == null)
            { Debug.LogError("Portal reinforcement configuration is incomplete.", this); enabled = false; return; }
            var oldBranch = game.routeC;
            var left = Tail(game.routeA, leftRoadIndex, "Left portal route");
            var right = Tail(game.routeB, rightRoadIndex, "Right portal route");
            if (!left || !right) { enabled = false; return; }
            leftPortal = MakePortal("Left temporary portal", left.points[0].position);
            rightPortal = MakePortal("Right temporary portal", right.points[0].position);
            for (int i = 0; i < game.waves.Length; i++)
            {
                var wave = game.waves[i];
                if (wave == null || wave.groups == null) continue;
                var groups = new List<WaveGameController.SpawnGroup>();
                foreach (var group in wave.groups)
                {
                    if (group == null || group.route != oldBranch) { groups.Add(group); continue; }
                    bool portalWave = IsPortalWave(i + 1);
                    int leftCount = (group.count + 1) / 2, rightCount = group.count / 2;
                    if (leftCount > 0) groups.Add(Copy(group, portalWave ? left : game.routeA, leftCount, "左路增援"));
                    if (rightCount > 0) groups.Add(Copy(group, portalWave ? right : game.routeB, rightCount, "右路增援"));
                }
                wave.groups = groups.ToArray();
            }
            oldBranch.gameObject.SetActive(false);
            game.routeC = null;
        }
        private WaypointPath Tail(WaypointPath source, int index, string name)
        {
            if (source.points == null || index < 0 || index >= source.points.Length - 1)
            { Debug.LogError("Portal path start is outside the road.", this); return null; }
            var path = new GameObject(name).AddComponent<WaypointPath>();
            path.transform.SetParent(transform, false);
            path.counterRoute = source;
            path.points = new Transform[source.points.Length - index];
            Array.Copy(source.points, index, path.points, 0, path.points.Length);
            return path;
        }
        private SpriteRenderer MakePortal(string name, Vector3 position)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            obj.transform.position = position;
            obj.transform.localScale = new Vector3(portalScale.x, portalScale.y, 1);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 8;
            renderer.enabled = false;
            return renderer;
        }
        private static WaveGameController.SpawnGroup Copy(WaveGameController.SpawnGroup original, WaypointPath path, int count, string label)
        {
            return new WaveGameController.SpawnGroup {
                label = label, route = path, prefab = original.prefab, count = count,
                spawnInterval = original.spawnInterval, startDelay = original.startDelay, requiresBranch = false
            };
        }
        private bool IsPortalWave(int number) => portalWaves != null && Array.IndexOf(portalWaves, number) >= 0;
        private void Update()
        {
            bool visible = game && game.IsSpawning && IsPortalWave(game.CurrentWave) && portalFrames != null && portalFrames.Length > 0;
            if (leftPortal) leftPortal.enabled = visible;
            if (rightPortal) rightPortal.enabled = visible;
            if (!visible) return;
            var frame = portalFrames[Mathf.FloorToInt(Time.time * 8) % portalFrames.Length];
            leftPortal.sprite = rightPortal.sprite = frame;
        }
    }
}
