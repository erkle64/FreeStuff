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
            VERSION = "0.2.1";

        public static LogSource log;

        private static TypedConfigEntry<int> configCreativeChestRate;
        private static TypedConfigEntry<float> configCreativeTankRate;
        private static TypedConfigEntry<int> configVoidChestRate;
        private static TypedConfigEntry<float> configVoidTankRate;

        private static Sprite _creativeChestIcon;
        private static Sprite _creativeTankIcon;
        private static Sprite _voidChestIcon;
        private static Sprite _voidTankIcon;

        private static bool hasRun_craftingTags;
        private static bool hasLoaded_craftingTags;
        private static bool hasRun_recipes;
        private static bool hasLoaded_recipes;

        private static readonly string[] _creativeChestRecipeTags = new string[] { "creative_chest" };
        private static readonly string[] _voidChestRecipeTags = new string[] { "void_chest" };
        private static readonly string[] _creativeTankRecipeTags = new string[] { "creative_tank" };
        private static readonly string[] _voidTankRecipeTags = new string[] { "void_tank" };

        private const string creativeChestIdentifier = "_erkle64_creative_chest";
        private const string voidChestIdentifier = "_erkle64_void_chest";
        private const string creativeTankIdentifier = "_erkle64_creative_tank";
        private const string voidTankIdentifier = "_erkle64_void_tank";

        public Plugin()
        {
            log = new LogSource(MODNAME);

            new Config(GUID)
                .Group("Creative Chest")
                    .Entry(out configCreativeChestRate, "Rate", 1200, "Items per minute output for creative chest.")
                .EndGroup()
                .Group("Void Chest")
                    .Entry(out configVoidChestRate, "Rate", 1200, "Items per minute output for void chest.")
                .EndGroup()
                .Group("Creative Tank")
                    .Entry(out configCreativeTankRate, "Rate", 100000.0f, "Litres per minute output for creative tank.")
                .EndGroup()
                .Group("Void Tank")
                    .Entry(out configVoidTankRate, "Rate", 200000.0f, "Litres per minute output for void tank.")
                .EndGroup()
                .Load()
                .Save();
        }

        public override void Load(Mod mod)
        {
            log.Log($"Loading {MODNAME}");

            var assets = typeof(Mod).GetField("assets", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mod) as Dictionary<string, UnityEngine.Object>;
            _creativeChestIcon = assets.LoadAsset<Sprite>("freestuff_creative_chest");
            _voidChestIcon = assets.LoadAsset<Sprite>("freestuff_void_chest");
            _creativeTankIcon = assets.LoadAsset<Sprite>("freestuff_creative_tank");
            _voidTankIcon = assets.LoadAsset<Sprite>("freestuff_void_tank");

            var items = AssetManager.getAllAssetsOfType<ItemTemplate>();
            var buildings = AssetManager.getAllAssetsOfType<BuildableObjectTemplate>();

            #region Creative Chest
            var chestItem = items[ItemTemplate.generateStringHash("_base_logistic_container_i")];
            var chest = buildings[BuildableObjectTemplate.generateStringHash("_base_logistic_container_i")];
            var assembler = buildings[BuildableObjectTemplate.generateStringHash("_base_assembler_i")];

            var assemblerGO = assembler.prefabOnDisk.GetComponent<ProducerGO>();
            var assemblerPanel = assemblerGO.ScreenPanelManager.screenPanels[0];
            var assemblerPanelProfile = assemblerGO.ScreenPanelManager.sp_profiles[0];

            var creativeChest = Object.Instantiate(assembler);
            creativeChest.identifier = creativeChestIdentifier;
            creativeChest.size = new Vector3Int(2, 2, 2);
            creativeChest.energyConsumptionKW_str = "0";
            creativeChest.producer_audioClip_active = null;
            creativeChest.producer_audioClip_customItemFinishSound = null;

            var creativeChestPrefab = Object.Instantiate(chest.prefabOnDisk);
            Object.DontDestroyOnLoad(creativeChestPrefab);
            creativeChestPrefab.SetActive(false);
            creativeChest.prefabOnDisk = creativeChestPrefab;
            creativeChest.producer_recipeType_tags = _creativeChestRecipeTags;

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

            var creativeChestProducerGO = creativeChestPrefab.AddComponent<ProducerGO>();
            creativeChestProducerGO._screenPanelManager = new ScreenPanelManager()
            {
                screenPanels = new ScreenPanel[] { creativeChestPanel },
                sp_profiles = new GameObject[] { assemblerPanelProfile }
            };

            var creativeChestItem = Object.Instantiate(chestItem);
            creativeChestItem.identifier = creativeChestIdentifier;
            creativeChestItem.name = "Creative Chest";
            creativeChestItem.buildableObjectIdentifer = creativeChestIdentifier;

            AssetManager.registerAsset(creativeChest, false);
            AssetManager.registerAsset(creativeChestItem, false);
            #endregion

            #region Void Chest
            var incinerator = buildings[BuildableObjectTemplate.generateStringHash("_base_incinerator")];

            var incineratorGO = incinerator.prefabOnDisk.GetComponent<DissolverGO>();
            var incineratorPanel = incineratorGO.ScreenPanelManager.screenPanels[0];
            var incineratorPanelProfile = incineratorGO.ScreenPanelManager.sp_profiles[0];

            var voidChest = Object.Instantiate(incinerator);
            voidChest.identifier = voidChestIdentifier;
            voidChest.size = new Vector3Int(2, 2, 2);
            voidChest.energyConsumptionKW_str = "0";
            voidChest.dissolver_solidsPerSecond_str = (60.0f / configVoidChestRate.Get()).ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture);

            var voidChestPrefab = Object.Instantiate(chest.prefabOnDisk);
            Object.DontDestroyOnLoad(voidChestPrefab);
            voidChestPrefab.SetActive(false);
            voidChest.prefabOnDisk = voidChestPrefab;

            var creativeChestMeshObject = voidChestPrefab.transform.Find("Logistic_container_01_v2").gameObject;
            var creativeChestMeshRenderer = creativeChestMeshObject.GetComponent<MeshRenderer>();
            creativeChestMeshRenderer.material.SetColor("_Color", new Color(0.0f, 0.0f, 0.6f, 1.0f));

            var voidChestPanel = Object.Instantiate(incineratorPanel.gameObject).GetComponent<ScreenPanel>();
            voidChestPanel.transform.SetParent(voidChestPrefab.transform, false);
            voidChestPanel.transform.localRotation = new Quaternion(0.0f, -0.99785894f, 0.065403156f, 0.0f);
            voidChestPanel.transform.localPosition = new Vector3(0.0f, 1.668f, 0.85f);

            foreach (var panel in voidChestPrefab.GetComponentsInChildren<StoragePanelGO>().ToArray()) Object.DestroyImmediate(panel.gameObject);
            foreach (var interactable in voidChestPrefab.GetComponentsInChildren<InteractableObject>().ToArray()) Object.DestroyImmediate(interactable.gameObject);
            Object.DestroyImmediate(voidChestPrefab.GetComponent<ChestGO>());

            var voidChestDissolverGO = voidChestPrefab.AddComponent<DissolverGO>();
            voidChestDissolverGO._screenPanelManager = new ScreenPanelManager()
            {
                screenPanels = new ScreenPanel[] { voidChestPanel },
                sp_profiles = new GameObject[] { incineratorPanelProfile }
            };

            var voidChestFBM = voidChestDissolverGO._fluidBoxManager = new FluidBoxManager()
            {
                ioFluidBoxFilterControlPanelsContainer = new FluidBoxManager.IOFluidBoxContainer[0],
                regularFluidBoxFilterControlPanels = new FluidBoxManager.RegularFluidBoxContainer[0],
                meshFilter_highlighterBody = null
            };

            voidChestDissolverGO._particleSystemManager = new ParticleSystemManager()
            {
                particleSystems = new ParticleSystem[0] { }
            };

            voidChestDissolverGO._audioSourceFaderManager = new AudioSourceFaderManager()
            {
                ac_array = new AudioClip[0],
                as_array = new AudioSource[0]
            };

            var voidChestItem = Object.Instantiate(chestItem);
            voidChestItem.identifier = voidChestIdentifier;
            voidChestItem.name = "Void Chest";
            voidChestItem.buildableObjectIdentifer = voidChestIdentifier;

            AssetManager.registerAsset(voidChest, false);
            AssetManager.registerAsset(voidChestItem, false);
            #endregion

            #region Creative Tank
            var tankItem = items[ItemTemplate.generateStringHash("_base_tank_i")];
            var tank = buildings[BuildableObjectTemplate.generateStringHash("_base_tank_i")];
            var chemicalProcessor = buildings[BuildableObjectTemplate.generateStringHash("_base_chemical_processor_i")];

            var chemicalProcessorGO = chemicalProcessor.prefabOnDisk.GetComponent<ProducerWithFBMGO>();
            var chemicalProcessorPanel = chemicalProcessorGO.ScreenPanelManager.screenPanels[0];
            var chemicalProcessorPanelProfile = chemicalProcessorGO.ScreenPanelManager.sp_profiles[0];

            var creativeTank = Object.Instantiate(chemicalProcessor);
            creativeTank.identifier = creativeTankIdentifier;
            creativeTank.size = new Vector3Int(7, 4, 3);
            creativeTank.energyConsumptionKW_str = "0";

            var creativeTankPrefab = Object.Instantiate(tank.prefabOnDisk);
            Object.DontDestroyOnLoad(creativeTankPrefab);
            creativeTankPrefab.SetActive(false);
            creativeTank.prefabOnDisk = creativeTankPrefab;
            creativeTank.producer_recipeType_tags = _creativeTankRecipeTags;
            creativeTank.fbm_fluidBoxes = new BuildableObjectTemplate.FluidBoxData[0];
            creativeTank.fbm_ioFluidBoxes = new BuildableObjectTemplate.IOFluidBoxData[] {
                new BuildableObjectTemplate.IOFluidBoxData()
                {
                    capacity_l = 100000.0f,
                    isInput = false,
                    type = BuildableObjectTemplate.IOFluidBoxData.IOFBType.Generic,
                    transferRatePerSecond_l = configCreativeTankRate.Get(),
                    transferSpeedType = BuildableObjectTemplate.IOFluidBoxData.IOFBTransferSpeedType.AlwaysMax,
                    fixedElementTemplateIdentifier = string.Empty,
                    connectors = new BuildableObjectTemplate.IOFluidBoxData.IOFluidBoxConnectionData[]
                    {
                        new BuildableObjectTemplate.IOFluidBoxData.IOFluidBoxConnectionData()
                        {
                            groupIdentifier = "_base_pipe",
                            localOffset_origin = new Vector3Int(0, 0, 1),
                            localOffset_target = new Vector3Int(-1, 0, 1)
                        },
                        new BuildableObjectTemplate.IOFluidBoxData.IOFluidBoxConnectionData()
                        {
                            groupIdentifier = "_base_pipe",
                            localOffset_origin = new Vector3Int(6, 0, 1),
                            localOffset_target = new Vector3Int(7, 0, 1)
                        },
                        new BuildableObjectTemplate.IOFluidBoxData.IOFluidBoxConnectionData()
                        {
                            groupIdentifier = "_base_pipe",
                            localOffset_origin = new Vector3Int(3, 0, 0),
                            localOffset_target = new Vector3Int(3, 0, -1)
                        },
                        new BuildableObjectTemplate.IOFluidBoxData.IOFluidBoxConnectionData()
                        {
                            groupIdentifier = "_base_pipe",
                            localOffset_origin = new Vector3Int(3, 0, 2),
                            localOffset_target = new Vector3Int(3, 0, 3)
                        },
                    }
                }
            };

            var creativeTankMeshObject = creativeTankPrefab.transform.Find("UpperPart").gameObject;
            var creativeTankMeshRenderer = creativeTankMeshObject.GetComponent<MeshRenderer>();
            creativeTankMeshRenderer.material.SetColor("_Color", Color.magenta);

            var creativeTankPanel = Object.Instantiate(chemicalProcessorPanel.gameObject).GetComponent<ScreenPanel>();
            creativeTankPanel.transform.SetParent(creativeTankPrefab.transform, false);
            creativeTankPanel.transform.localRotation = new Quaternion(0.0f, 0.0f, 0.0f, 1.0f);
            creativeTankPanel.transform.localPosition = new Vector3(0.0f, 1.8f, -1.39f);

            foreach (var panel in creativeTankPrefab.GetComponentsInChildren<StoragePanelGO>().ToArray()) Object.DestroyImmediate(panel.gameObject);
            Object.DestroyImmediate(creativeTankPrefab.GetComponent<TankGO>());

            var creativeTankProducerGO = creativeTankPrefab.AddComponent<ProducerWithFBMGO>();
            creativeTankProducerGO._screenPanelManager = new ScreenPanelManager()
            {
                screenPanels = new ScreenPanel[] { creativeTankPanel },
                sp_profiles = new GameObject[] { chemicalProcessorPanelProfile }
            };

            var creativeTankFBM = creativeTankProducerGO._fluidBoxManager = new FluidBoxManager()
            {
                ioFluidBoxFilterControlPanelsContainer = new FluidBoxManager.IOFluidBoxContainer[0],
                regularFluidBoxFilterControlPanels = new FluidBoxManager.RegularFluidBoxContainer[0],
                meshFilter_highlighterBody = creativeTankPrefab.GetComponent<MeshFilter>()
            };

            var creativeTankItem = Object.Instantiate(tankItem);
            creativeTankItem.identifier = creativeTankIdentifier;
            creativeTankItem.name = "Creative Tank";
            creativeTankItem.buildableObjectIdentifer = creativeTankIdentifier;

            AssetManager.registerAsset(creativeTank, false);
            AssetManager.registerAsset(creativeTankItem, false);
            #endregion

            #region Void Tank
            var flareStack = buildings[BuildableObjectTemplate.generateStringHash("_base_flare_stack")];

            var flareStackGO = flareStack.prefabOnDisk.GetComponent<DissolverGO>();
            var flareStackPanel = flareStackGO.ScreenPanelManager.screenPanels[0];
            var flareStackPanelProfile = flareStackGO.ScreenPanelManager.sp_profiles[0];

            var voidTank = Object.Instantiate(flareStack);
            voidTank.identifier = voidTankIdentifier;
            voidTank.size = new Vector3Int(7, 4, 3);
            voidTank.energyConsumptionKW_str = "0";
            voidTank.dissolver_elementalConsumptionPerSecond = configVoidTankRate.Get() / 60.0f;

            var voidTankPrefab = Object.Instantiate(tank.prefabOnDisk);
            Object.DontDestroyOnLoad(voidTankPrefab);
            voidTankPrefab.SetActive(false);
            voidTank.prefabOnDisk = voidTankPrefab;
            voidTank.producer_recipeType_tags = _voidTankRecipeTags;
            voidTank.fbm_fluidBoxes = new BuildableObjectTemplate.FluidBoxData[0];
            voidTank.fbm_ioFluidBoxes = new BuildableObjectTemplate.IOFluidBoxData[] {
                new BuildableObjectTemplate.IOFluidBoxData()
                {
                    capacity_l = 100000.0f,
                    isInput = true,
                    type = BuildableObjectTemplate.IOFluidBoxData.IOFBType.Generic,
                    transferRatePerSecond_l = configVoidTankRate.Get() / 30.0f,
                    transferSpeedType = BuildableObjectTemplate.IOFluidBoxData.IOFBTransferSpeedType.AlwaysMax,
                    fixedElementTemplateIdentifier = string.Empty,
                    connectors = new BuildableObjectTemplate.IOFluidBoxData.IOFluidBoxConnectionData[]
                    {
                        new BuildableObjectTemplate.IOFluidBoxData.IOFluidBoxConnectionData()
                        {
                            groupIdentifier = "_base_pipe",
                            localOffset_origin = new Vector3Int(6, 0, 1),
                            localOffset_target = new Vector3Int(7, 0, 1)
                        }/*, Dissolver seems to only allow one connection.
                        new BuildableObjectTemplate.IOFluidBoxData.IOFluidBoxConnectionData()
                        {
                            groupIdentifier = "_base_pipe",
                            localOffset_origin = new Vector3Int(0, 0, 1),
                            localOffset_target = new Vector3Int(-1, 0, 1)
                        },
                        new BuildableObjectTemplate.IOFluidBoxData.IOFluidBoxConnectionData()
                        {
                            groupIdentifier = "_base_pipe",
                            localOffset_origin = new Vector3Int(3, 0, 0),
                            localOffset_target = new Vector3Int(3, 0, -1)
                        },
                        new BuildableObjectTemplate.IOFluidBoxData.IOFluidBoxConnectionData()
                        {
                            groupIdentifier = "_base_pipe",
                            localOffset_origin = new Vector3Int(3, 0, 2),
                            localOffset_target = new Vector3Int(3, 0, 3)
                        }*/
                    }
                }
            };

            var voidTankMeshObject = voidTankPrefab.transform.Find("UpperPart").gameObject;
            var voidTankMeshRenderer = voidTankMeshObject.GetComponent<MeshRenderer>();
            voidTankMeshRenderer.material.SetColor("_Color", new Color(0.0f, 0.0f, 0.6f, 1.0f));

            var voidTankPanel = Object.Instantiate(flareStackPanel.gameObject).GetComponent<ScreenPanel>();
            voidTankPanel.transform.SetParent(voidTankPrefab.transform, false);
            voidTankPanel.transform.localRotation = new Quaternion(0.0f, 0.0f, 0.0f, 1.0f);
            voidTankPanel.transform.localPosition = new Vector3(0.0f, 1.8f, -1.39f);

            foreach (var panel in voidTankPrefab.GetComponentsInChildren<StoragePanelGO>().ToArray()) Object.DestroyImmediate(panel.gameObject);
            Object.DestroyImmediate(voidTankPrefab.GetComponent<TankGO>());

            var voidTankDissolverGO = voidTankPrefab.AddComponent<DissolverGO>();
            voidTankDissolverGO._screenPanelManager = new ScreenPanelManager()
            {
                screenPanels = new ScreenPanel[] { voidTankPanel },
                sp_profiles = new GameObject[] { flareStackPanelProfile }
            };

            var voidTankFBM = voidTankDissolverGO._fluidBoxManager = new FluidBoxManager()
            {
                ioFluidBoxFilterControlPanelsContainer = new FluidBoxManager.IOFluidBoxContainer[0],
                regularFluidBoxFilterControlPanels = new FluidBoxManager.RegularFluidBoxContainer[0],
                meshFilter_highlighterBody = null
            };

            voidTankDissolverGO._particleSystemManager = new ParticleSystemManager()
            {
                particleSystems = new ParticleSystem[0] { }
            };

            voidTankDissolverGO._audioSourceFaderManager = new AudioSourceFaderManager()
            {
                ac_array = new AudioClip[0],
                as_array = new AudioSource[0]
            };

            var voidTankItem = Object.Instantiate(tankItem);
            voidTankItem.identifier = voidTankIdentifier;
            voidTankItem.name = "Void Tank";
            voidTankItem.buildableObjectIdentifer = voidTankIdentifier;

            AssetManager.registerAsset(voidTank, false);
            AssetManager.registerAsset(voidTankItem, false);
            #endregion
        }

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

                    if (!hasRun_recipes && hasLoaded_recipes)
                    {
                        hasRun_recipes = true;

                        var creativeRecipes = new List<CraftingRecipe>();
                        var createdRecipesForItem = new HashSet<string>();

                        var items = AssetManager.getAllAssetsOfType<ItemTemplate>();
                        foreach (var kv in AssetManager.getAllAssetsOfType<CraftingRecipe>())
                        {
                            var recipe = kv.Value;
                            if (recipe.outputElemental_data.Length == 0 && recipe.output_data.Length == 1 && !recipe.isHiddenInCharacterCraftingFrame)
                            {
                                if (items.TryGetValue(ItemTemplate.generateStringHash(recipe.output_data[0].identifier), out var item))
                                {
                                    if (createdRecipesForItem.Contains(item.identifier)) continue;
                                    createdRecipesForItem.Add(item.identifier);

                                    AddCreativeItemRecipe(recipe, item);
                                }
                            }
                        }

                        AddCreativeItemRecipe(
                            AssetManager.getAsset<CraftingRecipe>(CraftingRecipe.generateStringHash("_base_firmarlite_sheet_t1")),
                            AssetManager.getAsset<ItemTemplate>(ItemTemplate.generateStringHash("_base_firmarlite_bar"))
                            );

                        AddCreativeItemRecipe(
                            AssetManager.getAsset<CraftingRecipe>(CraftingRecipe.generateStringHash("_base_ore_xenoferrite")),
                            AssetManager.getAsset<ItemTemplate>(ItemTemplate.generateStringHash("_base_rubble_xenoferrite"))
                            );

                        AddCreativeItemRecipe(
                            AssetManager.getAsset<CraftingRecipe>(CraftingRecipe.generateStringHash("_base_ore_technum")),
                            AssetManager.getAsset<ItemTemplate>(ItemTemplate.generateStringHash("_base_rubble_technum"))
                            );

                        AddCreativeItemRecipe(
                            AssetManager.getAsset<CraftingRecipe>(CraftingRecipe.generateStringHash("_base_ore_ignium")),
                            AssetManager.getAsset<ItemTemplate>(ItemTemplate.generateStringHash("_base_rubble_ignium"))
                            );

                        AddCreativeItemRecipe(
                            AssetManager.getAsset<CraftingRecipe>(CraftingRecipe.generateStringHash("_base_concrete")),
                            AssetManager.getAsset<ItemTemplate>(ItemTemplate.generateStringHash("_base_ore_mineral_rock"))
                            );

                        AddCreativeItemRecipe(
                            AssetManager.getAsset<CraftingRecipe>(CraftingRecipe.generateStringHash("_base_ore_telluxite")),
                            AssetManager.getAsset<ItemTemplate>(ItemTemplate.generateStringHash("_base_rubble_telluxite"))
                            );

                        var baseElementRecipe = AssetManager.getAsset<CraftingRecipe>(CraftingRecipe.generateStringHash("_base_olumic_acid"));
                        foreach (var element in ItemTemplateManager.getAllElementTemplates())
                        {
                            AddCreativeElementRecipe(baseElementRecipe, element.Value);
                        }

                        foreach (var recipe in creativeRecipes)
                        {
                            AssetManager.registerAsset(recipe, false);
                            recipe.onLoad();
                            recipe.isHiddenInCharacterCraftingFrame = true;
                        }

                        void AddCreativeItemRecipe(CraftingRecipe recipe, ItemTemplate item)
                        {
                            var creativeRecipe = Object.Instantiate(recipe);
                            creativeRecipe.modIdentifier = "_erkle64_freestuff";
                            creativeRecipe.identifier = $"_freestuff_{item.identifier}";
                            creativeRecipe.name = item.name;
                            creativeRecipe.icon_identifier = item.icon_identifier;
                            creativeRecipe.timeMs = 1000;
                            creativeRecipe.tags = _creativeChestRecipeTags;
                            creativeRecipe.input_data = new CraftingRecipe.CraftingRecipeItemInput[0];
                            creativeRecipe.inputElemental_data = new CraftingRecipe.CraftingRecipeElementalInput[0];
                            creativeRecipe.output_data = new CraftingRecipe.CraftingRecipeItemInput[] {
                                new CraftingRecipe.CraftingRecipeItemInput()
                                {
                                    identifier = item.identifier,
                                    amount = Mathf.CeilToInt(configCreativeChestRate.Get() / 60.0f),
                                    percentage_str = "1"
                                }
                            };

                            creativeRecipes.Add(creativeRecipe);
                        }

                        void AddCreativeElementRecipe(CraftingRecipe recipe, ElementTemplate element)
                        {
                            var creativeRecipe = Object.Instantiate(recipe);
                            creativeRecipe.modIdentifier = "_erkle64_freestuff";
                            creativeRecipe.identifier = $"_freestuff_{element.identifier}";
                            creativeRecipe.name = element.name;
                            creativeRecipe.icon_identifier = element.icon_identifier;
                            creativeRecipe.timeMs = 1000;
                            creativeRecipe.tags = _creativeTankRecipeTags;
                            creativeRecipe.input_data = new CraftingRecipe.CraftingRecipeItemInput[0];
                            creativeRecipe.inputElemental_data = new CraftingRecipe.CraftingRecipeElementalInput[0];
                            creativeRecipe.outputElemental_data = new CraftingRecipe.CraftingRecipeElementalInput[] {
                                new CraftingRecipe.CraftingRecipeElementalInput()
                                {
                                    identifier = element.identifier,
                                    amount_str = (configCreativeTankRate.Get() / 60.0f).ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture)
                                }
                            };

                            creativeRecipes.Add(creativeRecipe);

                            //var voidRecipe = Object.Instantiate(recipe);
                            //voidRecipe.modIdentifier = "_erkle64_freestuff";
                            //voidRecipe.identifier = $"_freestuff_void_{element.identifier}";
                            //voidRecipe.name = element.name;
                            //voidRecipe.icon_identifier = element.icon_identifier;
                            //voidRecipe.timeMs = 1000;
                            //voidRecipe.tags = _voidTankRecipeTags;
                            //voidRecipe.input_data = new CraftingRecipe.CraftingRecipeItemInput[0];
                            //voidRecipe.output_data = new CraftingRecipe.CraftingRecipeItemInput[0];
                            //voidRecipe.outputElemental_data = new CraftingRecipe.CraftingRecipeElementalInput[0];
                            //voidRecipe.inputElemental_data = new CraftingRecipe.CraftingRecipeElementalInput[] {
                            //    new CraftingRecipe.CraftingRecipeElementalInput()
                            //    {
                            //        identifier = element.identifier,
                            //        amount_str = (configCreativeTankRate.Get() / 60.0f).ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture)
                            //    }
                            //};

                            //creativeRecipes.Add(voidRecipe);
                        }
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
                botIdToTextureArray[BuildableObjectTemplate.generateStringHash(voidChestIdentifier)] = new Texture2D[] { };
                botIdToTextureArray[BuildableObjectTemplate.generateStringHash(creativeTankIdentifier)] = new Texture2D[] { };
                botIdToTextureArray[BuildableObjectTemplate.generateStringHash(voidTankIdentifier)] = new Texture2D[] { };
            }

            [HarmonyPatch(typeof(CraftingRecipe), nameof(CraftingRecipe.onLoad))]
            [HarmonyPostfix]
            public static void CraftingRecipe_onLoad(CraftingRecipe __instance)
            {
                hasLoaded_recipes = true;
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
                else if (__instance.identifier == voidChestIdentifier)
                {
                    __instance.icon = __instance.icon_64 = __instance.icon_256 = _voidChestIcon;
                }
                else if (__instance.identifier == creativeTankIdentifier)
                {
                    __instance.icon = __instance.icon_64 = __instance.icon_256 = _creativeTankIcon;
                }
                else if (__instance.identifier == voidTankIdentifier)
                {
                    __instance.icon = __instance.icon_64 = __instance.icon_256 = _voidTankIcon;
                }
            }

            [HarmonyPatch(typeof(ItemTemplateManager), nameof(ItemTemplateManager.InitOnApplicationStart))]
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            static void onItemTemplateManagerInitOnApplicationStart(ref IEnumerator __result)
            {
                var myEnumerator = new InitOnApplicationStartEnumerator(__result);
                __result = myEnumerator.GetEnumerator();
            }
        }
    }
}
