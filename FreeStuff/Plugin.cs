using C3;
using C3.ModKit;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unfoundry;
using UnityEngine;

namespace FreeStuff
{
    [UnfoundryMod(GUID)]
    public class Plugin : UnfoundryPlugin
    {
        public const string
            MODNAME = "FreeStuff",
            AUTHOR = "erkle64",
            GUID = AUTHOR + "." + MODNAME,
            VERSION = "0.1.0";

        public static LogSource log;

        private static TypedConfigEntry<int> configCreativeChestRate;

        private static Sprite _creativeChestIcon;

        private const string creativeChestIdentifier = "_erkle64_creative_chest";

        public Plugin()
        {
            log = new LogSource(MODNAME);

            new Config(GUID)
                .Group("Creative Chest")
                    .Entry(out configCreativeChestRate, "Rate", 640, "Items per minute output for creative chest.")
                .EndGroup()
                .Load()
                .Save();
        }

        public override void Load(Mod mod)
        {
            log.Log($"Loading {MODNAME}");

            var assets = typeof(Mod).GetField("assets", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mod) as Dictionary<string, UnityEngine.Object>;
            _creativeChestIcon = assets.LoadAsset<Sprite>("freestuff_creative_chest");

            var items = AssetManager.getAllAssetsOfType<ItemTemplate>();
            var buildings = AssetManager.getAllAssetsOfType<BuildableObjectTemplate>();

            var chestItem = items[ItemTemplate.generateStringHash("_base_logistic_container_i")];
            var chest = buildings[BuildableObjectTemplate.generateStringHash("_base_logistic_container_i")];
            var assembler = buildings[BuildableObjectTemplate.generateStringHash("_base_assembler_i")];

            var assemblerGO = assembler.prefabOnDisk.GetComponent<ProducerGO>();
            var assemblerPanel = assemblerGO.ScreenPanelManager.screenPanels[0];
            var assemblerPanelProfile = assemblerGO.ScreenPanelManager.sp_profiles[0];

            var creativeChest = Object.Instantiate(assembler);
            creativeChest.identifier = creativeChestIdentifier;
            creativeChest.size = new Vector3Int(2, 2, 2);

            var creativeChestPrefab = Object.Instantiate(chest.prefabOnDisk);
            Object.DontDestroyOnLoad(creativeChestPrefab);
            creativeChestPrefab.SetActive(false);
            creativeChest.prefabOnDisk = creativeChestPrefab;
            creativeChest.producer_recipeType_tags = new string[] { "creative_chest" };

            var mainMeshObject = creativeChestPrefab.transform.Find("Logistic_container_01_v2").gameObject;
            var mainMeshRenderer = mainMeshObject.GetComponent<MeshRenderer>();
            mainMeshRenderer.material.SetColor("_Color", Color.magenta);

            var creativeChestPanel = Object.Instantiate(assemblerPanel.gameObject).GetComponent<ScreenPanel>();
            creativeChestPanel.transform.SetParent(creativeChestPrefab.transform, false);
            creativeChestPanel.transform.localRotation = new Quaternion(0.0f, -0.99785894f, 0.065403156f, 0.0f);
            creativeChestPanel.transform.localPosition = new Vector3(0.0f, 1.668f, 0.85f);

            foreach (var panel in creativeChestPrefab.GetComponentsInChildren<StoragePanelGO>().ToArray()) Object.DestroyImmediate(panel.gameObject);
            foreach (var interactable in creativeChestPrefab.GetComponentsInChildren<InteractableObject>().ToArray()) Object.DestroyImmediate(interactable.gameObject);
            Object.DestroyImmediate(creativeChestPrefab.GetComponent<ChestGO>());

            var chestProducerGO = creativeChestPrefab.AddComponent<ProducerGO>();
            chestProducerGO._screenPanelManager = new ScreenPanelManager();
            chestProducerGO.ScreenPanelManager.screenPanels = new ScreenPanel[] { creativeChestPanel };
            chestProducerGO.ScreenPanelManager.sp_profiles = new GameObject[] { assemblerPanelProfile };

            var creativeChestItem = Object.Instantiate(chestItem);
            creativeChestItem.identifier = creativeChestIdentifier;
            creativeChestItem.name = "Creative Chest";
            creativeChestItem.buildableObjectIdentifer = creativeChestIdentifier;

            AssetManager.registerAsset(creativeChest, false);
            AssetManager.registerAsset(creativeChestItem, false);

            var recipeTags = new string[] { "creative_chest" };
            var done = new HashSet<string>();
            var creativeRecipes = new List<CraftingRecipe>();
            foreach (var kv in AssetManager.getAllAssetsOfType<CraftingRecipe>())
            {
                var recipe = kv.Value;
                if (recipe.outputElemental_data.Length == 0 && recipe.output_data.Length == 1)
                {
                    if (items.TryGetValue(ItemTemplate.generateStringHash(recipe.output_data[0].identifier), out var item))
                    {
                        if (done.Contains(item.identifier)) continue;
                        done.Add(item.identifier);

                        var creativeRecipe = Object.Instantiate(recipe);
                        creativeRecipe.modIdentifier = "_erkle64_freestuff";
                        creativeRecipe.identifier = $"_freestuff_{item.identifier}";
                        creativeRecipe.name = item.name;
                        creativeRecipe.icon_identifier = item.icon_identifier;
                        creativeRecipe.timeMs = 100;
                        creativeRecipe.tags = recipeTags;
                        creativeRecipe.input_data = new CraftingRecipe.CraftingRecipeItemInput[0];
                        creativeRecipe.inputElemental_data = new CraftingRecipe.CraftingRecipeElementalInput[0];
                        creativeRecipe.output_data = new CraftingRecipe.CraftingRecipeItemInput[]
                        {
                            new CraftingRecipe.CraftingRecipeItemInput()
                            {
                                identifier = item.identifier,
                                amount = configCreativeChestRate.Get() / 10,
                                percentage_str = "1"
                            }
                        };

                        creativeRecipes.Add(creativeRecipe);
                    }
                }
            }

            foreach (var recipe in creativeRecipes) AssetManager.registerAsset(recipe, false);
        }

        private static bool hasRun_craftingTags;
        private static bool hasLoaded_craftingTags;

        class InitOnApplicationStartEnumerator : IEnumerable
        {
            private IEnumerator _enumerator;

            public InitOnApplicationStartEnumerator(IEnumerator enumerator)
            {
                _enumerator = enumerator;
            }

            IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

            public IEnumerator GetEnumerator()
            {
                while (_enumerator.MoveNext())
                {
                    var enumerated = _enumerator.Current;

                    if (!hasRun_craftingTags && hasLoaded_craftingTags)
                    {
                        hasRun_craftingTags = true;

                        var creativeChestTag = ScriptableObject.CreateInstance<CraftingTag>();
                        creativeChestTag.modIdentifier = "_erkle64_freestuff";
                        creativeChestTag.identifier = "creative_chest";
                        creativeChestTag.name = "Creative Chest";
                        creativeChestTag.inventorySlotBgSprite = ResourceDB.sprite_iconClock;
                        creativeChestTag.slotLockDescription = string.Empty;
                        creativeChestTag.onLoad();
                        ItemTemplateManager.getAllCraftingTags().Add(creativeChestTag.id, creativeChestTag);
                    }

                    yield return enumerated;
                }
            }
        }

        [HarmonyPatch]
        public static class Patch
        {
            [HarmonyPatch(typeof(TextureStreamingProcessor), nameof(TextureStreamingProcessor.OnAddedToManager))]
            [HarmonyPostfix]
            public static void TextureStreamingProcessorOnAddedToManager(TextureStreamingProcessor __instance)
            {
                var botIdToTextureArray = typeof(TextureStreamingProcessor).GetField("botIdToTextureArray", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance) as Dictionary<ulong, Texture2D[]>;
                botIdToTextureArray[BuildableObjectTemplate.generateStringHash(creativeChestIdentifier)] = new Texture2D[] { };
            }

            [HarmonyPatch(typeof(CraftingRecipe), nameof(CraftingRecipe.onLoad))]
            [HarmonyPostfix]
            public static void CraftingRecipe_onLoad(CraftingRecipe __instance)
            {
                if (__instance.modIdentifier == "_erkle64_freestuff")
                {
                    __instance.isHiddenInCharacterCraftingFrame = true;
                }
            }

            [HarmonyPatch(typeof(CraftingTag), nameof(CraftingTag.onLoad))]
            [HarmonyPostfix]
            public static void CraftingTag_onLoad(CraftingTag __instance)
            {
                hasLoaded_craftingTags = true;
            }

            [HarmonyPatch(typeof(ItemTemplate), nameof(ItemTemplate.onLoad))]
            [HarmonyPostfix]
            public static void ItemTemplate_onLoad(ItemTemplate __instance)
            {
                if (__instance.identifier == creativeChestIdentifier)
                {
                    __instance.icon = __instance.icon_64 = __instance.icon_256 = _creativeChestIcon;
                }
            }

            [HarmonyPatch(typeof(ItemTemplateManager), nameof(ItemTemplateManager.InitOnApplicationStart))]
            [HarmonyPostfix]
            static void onItemTemplateManagerInitOnApplicationStart(ref IEnumerator __result)
            {
                var myEnumerator = new InitOnApplicationStartEnumerator(__result);
                __result = myEnumerator.GetEnumerator();
            }
        }
    }
}
