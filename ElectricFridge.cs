using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Electric Fridge", "RFC1920", "1.0.1")]
    [Description("Is your refrigerator running?")]

    class ElectricFridge : RustPlugin
    {
        private ConfigData configData;
        public static ElectricFridge Instance = null;
        const string FRBTN = "fridge.status";
        private DateTime lastUpdate;

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));
        #endregion

        void OnServerInitialized()
        {
            Instance = this;
            LoadConfigValues();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "off", "OFF" },
                { "on", "ON" }
            }, this);
        }

        private void Unload()
        {
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, FRBTN);
            }
        }

        void OnEntitySpawned(BaseEntity fridge)
        {
            if (fridge.ShortPrefabName.Equals("fridge.deployed"))
            {
                string prefabname = "assets/prefabs/deployable/playerioents/electricheater/electrical.heater.prefab";
                var go = GameManager.server.CreatePrefab(prefabname);
                go.SetActive(true);
                var ent = go.GetComponent<BaseEntity>();

                ent.transform.localEulerAngles = new Vector3(270, 270, 270);
                ent.transform.localPosition = new Vector3(-0.38f, 0.65f, 0);
                ent.SetParent(fridge);
                fridge.gameObject.AddComponent<FoodDecay>();

                BasePlayer player = BasePlayer.FindByID(fridge.OwnerID);
                Message(player.IPlayer, fridge.name);
            }
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            var electrified = container.GetComponentInChildren<ElectricalHeater>() ?? null;
            if(electrified == null) return null;

            var status = Lang("off");
            if(electrified.IsPowered()) status = Lang("on");

            PowerGUI(player, status);

            return null;
        }

        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            if(entity == null) return;
            CuiHelper.DestroyUi(player, FRBTN);
        }

        private void PowerGUI(BasePlayer player, string onoff = "")
        {
            CuiHelper.DestroyUi(player, FRBTN);

            string label = configData.Settings.branding + ": " + onoff;
            CuiElementContainer container = UI.Container(FRBTN, UI.Color("FFF5E0", 0.16f), "0.85 0.62", "0.946 0.65", true, "Overlay");
            UI.Label(ref container, FRBTN, UI.Color("#ffffff", 1f), label, 12, "0 0", "1 1");

            CuiHelper.AddUi(player, container);
        }

        public static class UI
        {
            public static CuiElementContainer Container(string panel, string color, string min, string max, bool useCursor = false, string parent = "Overlay")
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = color },
                            RectTransform = {AnchorMin = min, AnchorMax = max},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panel
                    }
                };
                return container;
            }
            public static void Panel(ref CuiElementContainer container, string panel, string color, string min, string max, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = min, AnchorMax = max },
                    CursorEnabled = cursor
                },
                panel);
            }
            public static void Label(ref CuiElementContainer container, string panel, string color, string text, int size, string min, string max, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = min, AnchorMax = max }
                },
                panel);

            }
            public static string Color(string hexColor, float alpha)
            {
                if(hexColor.StartsWith("#"))
                {
                    hexColor = hexColor.Substring(1);
                }
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }

        private class ConfigData
        {
            public Settings Settings = new Settings();
            public VersionNumber Version;
        }

        private class Settings
        {
            public string branding;
            public bool decay;
            public float foodDecay;
            public float timespan;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("New configuration file created.");
            configData = new ConfigData
            {
                Settings = new Settings()
                {
                    decay = false,
                    branding = "Frigidaire",
                    foodDecay = 0.98f, // 2% loss per timespan, rounded down.
                    timespan = 600f // 10 minutes
                },
                Version = Version
            };
            SaveConfig(configData);
        }

        private void LoadConfigValues()
        {
            configData = Config.ReadObject<ConfigData>();
            configData.Version = Version;

            SaveConfig(configData);
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }

        class FoodDecay : MonoBehaviour
        {
            private StorageContainer box;
            private ElectricalHeater heater;

            public void Awake()
            {
                box = GetComponent<StorageContainer>();
                heater = GetComponentInChildren<ElectricalHeater>();
                if(Instance.configData.Settings.decay) InvokeRepeating("ProcessContents", 0, Instance.configData.Settings.timespan);
            }

            public void OnDisable()
            {
                CancelInvoke("ProcessContents");
            }

            void ProcessContents()
            {
                if (Instance.configData.Settings.foodDecay <= 0) return;
                if (heater.IsPowered()) return;

                foreach (var item in box.inventory.itemList)
                {
                    if (item.amount < 1) continue;
                    var oldamt = item.amount.ToString();
                    item.amount = (int)Mathf.Floor(item.amount * Instance.configData.Settings.foodDecay);
                }
                box.UpdateNetworkGroup();
                box.SendNetworkUpdateImmediate();
            }
        }
    }
}
