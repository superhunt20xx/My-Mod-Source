using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Superhunt.ChairMayhem
{
    /// <summary>
    /// Classroom chairs become throwable weapons. A battered character turns
    /// glitchy and keeps haunting the player through its signature behaviors.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "superhunt.bbpp.chairmayhem";
        public const string PluginName = "Chair Mayhem";
        public const string PluginVersion = "1.3.0";

        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> HitsToKill;
        internal static ConfigEntry<float> StunSeconds;
        internal static ConfigEntry<float> ChaseSpeed;
        internal static ConfigEntry<bool> BreakWindows;
        internal static ConfigEntry<float> GlitchSeconds;
        internal static ConfigEntry<float> PlaytimeInterval;
        internal static ConfigEntry<float> PrincipalInterval;
        internal static ConfigEntry<float> PrincipalLockSeconds;
        internal static ConfigEntry<float> BaldiInterval;

        internal static ItemObject ChairItemObject;
        internal static GameObject ChairItemPrefab;
        internal static GameObject ChairTemplate;
        internal static AudioClip NullHitClip;
        internal static AudioClip BaldiHearThatClip;
        internal static Playtime PlaytimePrefab;

        internal static readonly HashSet<Character> CorruptedCharacters = new HashSet<Character>();

        private AudioSource _voiceSource;
        private float _nextPlaytime;
        private float _nextPrincipal;
        private float _nextBaldi;
        private bool _principalLockActive;
        private bool _playtimeActive;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Enabled = Config.Bind(
                "Chair",
                "Enabled",
                true,
                "Enable classroom chairs as throwable weapons.");

            HitsToKill = Config.Bind(
                "Combat",
                "HitsToKill",
                10,
                "How many chair hits it takes for a character to become a persistent corrupted harasser.");

            StunSeconds = Config.Bind(
                "Combat",
                "StunSeconds",
                3f,
                "How long a hit character is frozen after each chair hit.");

            ChaseSpeed = Config.Bind(
                "Combat",
                "ChaseSpeed",
                16f,
                "Movement speed of a character after it starts chasing the player.");

            BreakWindows = Config.Bind(
                "Combat",
                "BreakWindows",
                true,
                "Allow hit characters to break windows while chasing.");

            GlitchSeconds = Config.Bind(
                "Corruption",
                "GlitchSeconds",
                3f,
                "How long the random-colour glitch lasts after each hit.");

            PlaytimeInterval = Config.Bind(
                "Harassment",
                "PlaytimeIntervalSeconds",
                40f,
                "Once PlayTime is corrupted, how often she forces the player to jump rope.");

            PrincipalInterval = Config.Bind(
                "Harassment",
                "PrincipalIntervalSeconds",
                60f,
                "Once the Principal is corrupted, how often he confines the player.");

            PrincipalLockSeconds = Config.Bind(
                "Harassment",
                "PrincipalLockSeconds",
                10f,
                "How long the Principal's in-place confinement lasts.");

            BaldiInterval = Config.Bind(
                "Harassment",
                "BaldiIntervalSeconds",
                30f,
                "Once Baldi is corrupted, how often he says the tutorial 'I hear that' line.");

            NullHitClip = LoadEmbeddedClip("NullHit.wav");
            BaldiHearThatClip = LoadEmbeddedClip("BaldiHearThat.wav");

            _voiceSource = gameObject.AddComponent<AudioSource>();
            _voiceSource.playOnAwake = false;
            _voiceSource.spatialBlend = 0f;
            _voiceSource.volume = 0.9f;

            CreateChairItem();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(EnvironmentStartPatch));
            _harmony.PatchAll(typeof(ReturnToMenuPatch));

            if (NullHitClip != null)
            {
                Logger.LogInfo("Loaded Classic Remastered NullHit sound.");
            }
            else
            {
                Logger.LogWarning("Could not load the embedded NullHit sound; hits will be silent.");
            }

            if (BaldiHearThatClip != null)
            {
                Logger.LogInfo("Loaded the tutorial 'I hear that' audio.");
            }

            bool startPatched = HasPatch(typeof(EnvironmentController), "Start", postfix: true);
            bool menuPatched = HasPatch(
                typeof(CoreGameManager),
                nameof(CoreGameManager.ReturnToMenu),
                postfix: true);

            if (startPatched && menuPatched)
            {
                Logger.LogInfo("Patch verified: classroom chair setup and run reset.");
            }
            else
            {
                Logger.LogError(
                    $"Patch verification failed. Start={startPatched}, ReturnToMenu={menuPatched}.");
            }

            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded.");
        }

        private bool HasPatch(Type type, string methodName, bool postfix)
        {
            MethodInfo method = AccessTools.Method(type, methodName);
            if (method == null)
            {
                return false;
            }

            Patches patches = Harmony.GetPatchInfo(method);
            if (patches == null)
            {
                return false;
            }

            var list = postfix ? patches.Postfixes : patches.Prefixes;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].owner == PluginGuid)
                {
                    return true;
                }
            }

            return false;
        }

        internal static void ResetRun()
        {
            CorruptedCharacters.Clear();
            PlaytimePrefab = null;

            if (Instance != null)
            {
                Instance._nextPlaytime = 0f;
                Instance._nextPrincipal = 0f;
                Instance._nextBaldi = 0f;
                Instance._principalLockActive = false;
                Instance._playtimeActive = false;
            }
        }

        private void Update()
        {
            if (CorruptedCharacters.Count == 0)
            {
                return;
            }

            CoreGameManager core = Singleton<CoreGameManager>.Instance;
            if (core == null)
            {
                return;
            }

            PlayerManager player = core.GetPlayer(0);
            if (player == null || player.ec == null)
            {
                return;
            }

            if (CorruptedCharacters.Contains(Character.Playtime) &&
                !_playtimeActive &&
                Time.time >= _nextPlaytime)
            {
                _nextPlaytime = Time.time + Mathf.Max(5f, PlaytimeInterval.Value);
                StartCoroutine(PlaytimeHarassment(player));
            }

            if (CorruptedCharacters.Contains(Character.Principal) &&
                !_principalLockActive &&
                Time.time >= _nextPrincipal)
            {
                _nextPrincipal = Time.time + Mathf.Max(5f, PrincipalInterval.Value);
                StartCoroutine(PrincipalHarassment(player));
            }

            if (CorruptedCharacters.Contains(Character.Baldi) &&
                Time.time >= _nextBaldi)
            {
                float interval = Mathf.Max(5f, BaldiInterval.Value);
                _nextBaldi = Time.time + interval * UnityEngine.Random.Range(0.8f, 1.2f);
                PlayBaldiHearThat();
            }
        }

        internal static void CorruptCharacter(Character character, NPC source)
        {
            if (character == Character.Null || CorruptedCharacters.Contains(character))
            {
                return;
            }

            CorruptedCharacters.Add(character);
            float now = Time.time;

            if (Instance != null)
            {
                if (character == Character.Playtime)
                {
                    Instance._nextPlaytime = now + Mathf.Max(5f, PlaytimeInterval.Value);

                    if (PlaytimePrefab == null && source != null)
                    {
                        PlaytimePrefab = FindPlaytimePrefab(source.ec, source);
                    }
                }
                else if (character == Character.Principal)
                {
                    Instance._nextPrincipal = now + Mathf.Max(5f, PrincipalInterval.Value);
                }
                else if (character == Character.Baldi)
                {
                    Instance._nextBaldi =
                        now + Mathf.Max(5f, BaldiInterval.Value) * UnityEngine.Random.Range(0.8f, 1.2f);
                }
            }

            Log.LogInfo($"{character} is corrupted; it will keep interfering for the rest of this run.");
        }

        private static Playtime FindPlaytimePrefab(EnvironmentController ec, NPC source)
        {
            if (ec == null)
            {
                return null;
            }

            for (int i = 0; i < ec.npcsToSpawn.Count; i++)
            {
                NPC npc = ec.npcsToSpawn[i];
                if (npc != null && npc.Character == Character.Playtime)
                {
                    Playtime prefab = npc.GetComponent<Playtime>();
                    if (prefab != null)
                    {
                        return prefab;
                    }
                }
            }

            for (int i = 0; i < ec.npcsLeftToSpawn.Count; i++)
            {
                NPC npc = ec.npcsLeftToSpawn[i];
                if (npc != null && npc.Character == Character.Playtime)
                {
                    Playtime prefab = npc.GetComponent<Playtime>();
                    if (prefab != null)
                    {
                        return prefab;
                    }
                }
            }

            Playtime[] all = Resources.FindObjectsOfTypeAll<Playtime>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].gameObject.scene.name == null)
                {
                    return all[i];
                }
            }

            if (source != null)
            {
                // Fallback: clone the live PlayTime into an inactive template.
                // SpawnNPC reinitializes it with the new environment on use.
                GameObject template = UnityEngine.Object.Instantiate(source.gameObject);
                template.name = "ChairMayhem_PlaytimeTemplate";
                template.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(template);

                ChairAggro[] aggros = template.GetComponentsInChildren<ChairAggro>(true);
                for (int i = 0; i < aggros.Length; i++)
                {
                    UnityEngine.Object.Destroy(aggros[i]);
                }

                Playtime cloned = template.GetComponent<Playtime>();
                if (cloned != null)
                {
                    Log.LogInfo("Created a PlayTime template for future jump-rope harassment.");
                    return cloned;
                }
            }

            return null;
        }

        private static Playtime FindLivePlaytime(EnvironmentController ec)
        {
            if (ec == null)
            {
                return null;
            }

            for (int i = 0; i < ec.Npcs.Count; i++)
            {
                NPC npc = ec.Npcs[i];
                if (npc != null && npc.Character == Character.Playtime)
                {
                    Playtime playtime = npc.GetComponent<Playtime>();
                    if (playtime != null)
                    {
                        return playtime;
                    }
                }
            }

            return null;
        }

        private static Principal FindPrincipal(EnvironmentController ec)
        {
            if (ec == null)
            {
                return null;
            }

            for (int i = 0; i < ec.Npcs.Count; i++)
            {
                NPC npc = ec.Npcs[i];
                if (npc != null && npc.Character == Character.Principal)
                {
                    Principal principal = npc.GetComponent<Principal>();
                    if (principal != null)
                    {
                        return principal;
                    }
                }
            }

            return null;
        }

        private IEnumerator PlaytimeHarassment(PlayerManager player)
        {
            _playtimeActive = true;
            bool temporary = false;
            Playtime playtime = FindLivePlaytime(player.ec);

            try
            {
                if (player.jumpropes.Count > 0)
                {
                    yield break;
                }

                if (playtime == null)
                {
                    if (PlaytimePrefab == null)
                    {
                        Log.LogWarning("PlayTime is corrupted, but no PlayTime prefab is available.");
                        yield break;
                    }

                    Cell cell = player.ec.CellFromPosition(player.transform.position);
                    if (cell == null || cell.Null)
                    {
                        yield break;
                    }

                    NPC spawned = player.ec.SpawnNPC(PlaytimePrefab, cell.position);
                    playtime = spawned as Playtime;
                    temporary = true;

                    if (playtime == null)
                    {
                        yield break;
                    }
                }

                playtime.StartJumprope(player);
                Log.LogInfo("PlayTime forces the player to jump rope.");

                if (!temporary)
                {
                    yield break;
                }

                yield return new WaitForSeconds(1f);

                while (player != null && player.jumpropes.Count > 0)
                {
                    yield return null;
                }

                yield return new WaitForSeconds(1f);

                if (playtime != null)
                {
                    playtime.Despawn();
                }
            }
            finally
            {
                _playtimeActive = false;
            }
        }

        private IEnumerator PrincipalHarassment(PlayerManager player)
        {
            _principalLockActive = true;
            MovementModifier lockModifier = new MovementModifier(Vector3.zero, 0f, 1000);

            try
            {
                if (player.plm != null && player.plm.am != null)
                {
                    player.plm.am.moveMods.Add(lockModifier);
                }

                Principal principal = FindPrincipal(player.ec);
                if (principal != null)
                {
                    principal.Scold("Running");
                }

                Log.LogInfo($"The Principal confines the player for {PrincipalLockSeconds.Value:0.#} seconds.");

                float end = Time.time + Mathf.Max(1f, PrincipalLockSeconds.Value);
                while (player != null && Time.time < end)
                {
                    yield return null;
                }
            }
            finally
            {
                if (player != null && player.plm != null && player.plm.am != null)
                {
                    player.plm.am.moveMods.Remove(lockModifier);
                }

                _principalLockActive = false;
            }
        }

        private void PlayBaldiHearThat()
        {
            if (BaldiHearThatClip == null || _voiceSource == null)
            {
                return;
            }

            _voiceSource.PlayOneShot(BaldiHearThatClip);
            Log.LogInfo("Corrupted Baldi says: I hear that!");
        }

        internal static bool TryThrowChair(PlayerManager player)
        {
            if (!Enabled.Value || player == null || player.ec == null || ChairTemplate == null)
            {
                return false;
            }

            return ThrownChair.Spawn(ChairTemplate, player) != null;
        }

        internal IEnumerator AttachChairsDelayed(EnvironmentController ec)
        {
            // Rooms and decorations can still finish building during the first
            // few frames of a level, so scan more than once.
            for (int pass = 0; pass < 4; pass++)
            {
                yield return new WaitForSecondsRealtime(0.4f);

                if (ec == null)
                {
                    yield break;
                }

                AttachChairs(ec);
            }
        }

        private static void AttachChairs(EnvironmentController ec)
        {
            if (ec == null || ec.cells == null)
            {
                return;
            }

            int added = 0;
            Transform[] transforms = UnityEngine.Object.FindObjectsOfType<Transform>();

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform chair = transforms[i];
                if (chair == null || !chair.gameObject.activeInHierarchy)
                {
                    continue;
                }

                string lowerName = chair.name.ToLowerInvariant();
                if (!lowerName.Contains("chair"))
                {
                    continue;
                }

                // Round tables with chairs are furniture clusters, not chairs.
                if (lowerName.Contains("table") || lowerName.Contains("wheel"))
                {
                    continue;
                }

                if (chair.gameObject.scene != ec.gameObject.scene)
                {
                    continue;
                }

                if (chair.GetComponentInChildren<ChairPickup>(true) != null)
                {
                    continue;
                }

                Transform parent = chair.parent;
                if (parent != null && parent.name.ToLowerInvariant().Contains("chair"))
                {
                    continue;
                }

                Cell cell = ec.CellFromPosition(chair.position);
                if (cell == null || cell.Null || cell.room == null ||
                    cell.room.category != RoomCategory.Class)
                {
                    continue;
                }

                if (chair.GetComponentInChildren<MeshRenderer>(true) == null)
                {
                    continue;
                }

                CreateChairMarker(chair.gameObject);
                added++;

                if (ChairTemplate == null)
                {
                    ChairTemplate = chair.gameObject;
                }
            }

            if (added > 0)
            {
                Log.LogInfo($"Added chair pickup interaction to {added} classroom chair(s).");
            }
        }

        private static void CreateChairMarker(GameObject chair)
        {
            Vector3 center = Vector3.zero;
            Vector3 size = new Vector3(8f, 8f, 8f);

            BoxCollider sourceCollider = chair.GetComponent<BoxCollider>();
            if (sourceCollider != null)
            {
                center = sourceCollider.center;
                size = sourceCollider.size;
            }
            else
            {
                Renderer renderer = chair.GetComponentInChildren<Renderer>(true);
                if (renderer != null)
                {
                    center = chair.transform.InverseTransformPoint(renderer.bounds.center);
                    size = renderer.bounds.size;
                }
            }

            GameObject marker = new GameObject("ChairMayhem_Pickup");
            marker.layer = 0;
            marker.transform.SetParent(chair.transform, false);
            marker.transform.localPosition = center;
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = Vector3.one;

            BoxCollider collider = marker.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = size * 1.12f;
            collider.isTrigger = true;

            ChairPickup pickup = marker.AddComponent<ChairPickup>();
            pickup.Initialize(chair, ChairItemObject);
        }

        private void CreateChairItem()
        {
            Sprite icon = CreateChairIcon();

            GameObject prefab = new GameObject("ChairMayhem_Item");
            prefab.SetActive(false);
            prefab.AddComponent<ChairItem>();
            UnityEngine.Object.DontDestroyOnLoad(prefab);

            ChairItemObject = ScriptableObject.CreateInstance<ItemObject>();
            ChairItemObject.name = "Chair";
            ChairItemObject.item = prefab.GetComponent<ChairItem>();
            ChairItemObject.itemType = (Items)1000;
            ChairItemObject.itemSpriteSmall = icon;
            ChairItemObject.itemSpriteLarge = icon;
            ChairItemObject.nameKey = "Chair";
            ChairItemObject.descKey = "Chair";
            ChairItemObject.value = 0;
            ChairItemObject.price = 0;
            ChairItemObject.addToInventory = true;

            ChairItemPrefab = prefab;
        }

        private static Sprite CreateChairIcon()
        {
            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 outline = new Color32(48, 31, 19, 255);
            Color32 darkWood = new Color32(112, 67, 32, 255);
            Color32 wood = new Color32(161, 99, 47, 255);
            Color32 lightWood = new Color32(198, 132, 70, 255);

            Color32[] pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            FillRect(pixels, size, 7, 12, 18, 6, outline);
            FillRect(pixels, size, 8, 13, 16, 4, wood);
            FillRect(pixels, size, 8, 18, 4, 10, outline);
            FillRect(pixels, size, 9, 19, 2, 8, lightWood);
            FillRect(pixels, size, 9, 25, 14, 3, darkWood);
            FillRect(pixels, size, 8, 3, 4, 10, outline);
            FillRect(pixels, size, 9, 4, 2, 8, darkWood);
            FillRect(pixels, size, 21, 3, 4, 10, outline);
            FillRect(pixels, size, 22, 4, 2, 8, darkWood);
            FillRect(pixels, size, 12, 6, 3, 7, outline);
            FillRect(pixels, size, 18, 6, 3, 7, outline);

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static void FillRect(
            Color32[] pixels,
            int width,
            int x,
            int y,
            int rectWidth,
            int rectHeight,
            Color32 color)
        {
            for (int py = y; py < y + rectHeight; py++)
            {
                for (int px = x; px < x + rectWidth; px++)
                {
                    if (px >= 0 && px < width && py >= 0 && py < width)
                    {
                        pixels[py * width + px] = color;
                    }
                }
            }
        }

        private static AudioClip LoadEmbeddedClip(string fileName)
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                string[] names = assembly.GetManifestResourceNames();
                string resourceName = null;

                for (int i = 0; i < names.Length; i++)
                {
                    if (names[i].EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        resourceName = names[i];
                        break;
                    }
                }

                if (resourceName == null)
                {
                    Log.LogWarning($"Embedded resource {fileName} not found.");
                    return null;
                }

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        return null;
                    }

                    using (MemoryStream memory = new MemoryStream())
                    {
                        stream.CopyTo(memory);
                        return ParseWav(memory.ToArray(), fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to load embedded audio {fileName}: {ex}");
                return null;
            }
        }

        private static AudioClip ParseWav(byte[] data, string clipName)
        {
            if (data.Length < 44 ||
                Encoding.ASCII.GetString(data, 0, 4) != "RIFF" ||
                Encoding.ASCII.GetString(data, 8, 4) != "WAVE")
            {
                return null;
            }

            int channels = 1;
            int sampleRate = 8000;
            int bitsPerSample = 16;
            int dataOffset = -1;
            int dataLength = 0;
            int position = 12;

            while (position + 8 <= data.Length)
            {
                string chunkId = Encoding.ASCII.GetString(data, position, 4);
                int chunkSize = BitConverter.ToInt32(data, position + 4);
                int chunkData = position + 8;

                if (chunkSize < 0 || chunkData + chunkSize > data.Length)
                {
                    break;
                }

                if (chunkId == "fmt ")
                {
                    channels = BitConverter.ToInt16(data, chunkData + 2);
                    sampleRate = BitConverter.ToInt32(data, chunkData + 4);
                    bitsPerSample = BitConverter.ToInt16(data, chunkData + 14);
                }
                else if (chunkId == "data")
                {
                    dataOffset = chunkData;
                    dataLength = chunkSize;
                    break;
                }

                position = chunkData + chunkSize + (chunkSize & 1);
            }

            if (dataOffset < 0 || dataLength <= 0 || bitsPerSample != 16 || channels < 1)
            {
                return null;
            }

            int frameCount = dataLength / (2 * channels);
            if (frameCount <= 0)
            {
                return null;
            }

            float[] samples = new float[frameCount * channels];
            for (int i = 0; i < samples.Length; i++)
            {
                short sample = BitConverter.ToInt16(data, dataOffset + i * 2);
                samples[i] = sample / 32768f;
            }

            AudioClip clip = AudioClip.Create(clipName, frameCount, channels, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }

    [HarmonyPatch(typeof(EnvironmentController), "Start")]
    internal static class EnvironmentStartPatch
    {
        private static void Postfix(EnvironmentController __instance)
        {
            ChairDamage.ResetForLevel();

            if (Plugin.Instance != null)
            {
                Plugin.Instance.StartCoroutine(Plugin.Instance.AttachChairsDelayed(__instance));
            }
        }
    }

    [HarmonyPatch(typeof(CoreGameManager), nameof(CoreGameManager.ReturnToMenu))]
    internal static class ReturnToMenuPatch
    {
        private static void Postfix()
        {
            // Corruption is intentionally not reset between floors. It is only
            // cleared when this run actually ends and the game returns to the
            // main menu, so every subsequent floor keeps the same harassment.
            Plugin.ResetRun();
        }
    }
}
