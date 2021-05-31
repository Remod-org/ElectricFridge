#region License (GPL v3)
/*
    Electric Fridge
    Copyright (c) 2021 RFC1920 <desolationoutpostpve@gmail.com>

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/
#endregion
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Electric Fridge", "RFC1920", "1.0.5")]
    [Description("Is your refrigerator running?")]

    class ElectricFridge : RustPlugin
    {
        private ConfigData configData;
        public static ElectricFridge Instance = null;
        const string FRBTN = "fridge.status";
        private DateTime lastUpdate;
        private bool debug = false;
        private bool enabled = false;

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));

        private List<string> orDefault = new List<string>();

        private void DoLog(string message)
        {
            if (debug) Puts(message);
        }

        void OnServerInitialized()
        {
            Instance = this;
            LoadConfigValues();
            enabled = true;

            AddCovalenceCommand("fr", "EnableDisable");
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "off", "OFF" },
                { "on", "ON" },
                { "enabled", "Electric fridge enabled" },
                { "disabled", "Electric fridge disabled" }
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
            if (!enabled) return;
            if (fridge == null) return;
            if (string.IsNullOrEmpty(fridge.ShortPrefabName)) return;
            if (fridge.ShortPrefabName.Equals("fridge.deployed"))
            {
                string ownerid = fridge.OwnerID.ToString();
                if (configData.Settings.defaultEnabled && orDefault.Contains(ownerid))
                {
                    return;
                }
                else if (!configData.Settings.defaultEnabled && !orDefault.Contains(ownerid))
                {
                    return;
                }

                var go = GameManager.server.CreatePrefab("assets/prefabs/deployable/playerioents/electricheater/electrical.heater.prefab");
                if (go == null) return;

                go.SetActive(true);
                var ent = go.GetComponent<BaseEntity>();
                if (ent != null)
                {
                    ent.transform.localEulerAngles = new Vector3(270, 270, 270);
                    ent.transform.localPosition = new Vector3(-0.38f, 0.65f, 0);
                    ent.SetParent(fridge);
                    //ent.SetFlag(BaseEntity.Flags.Locked, true);
                    DoLog("Adding FoodDecay object");
                    fridge.gameObject.AddComponent<FoodDecay>();

                    //BasePlayer player = BasePlayer.FindByID(fridge.OwnerID);
                    //if (player != null) Message(player.IPlayer, fridge.name);
                }
            }
        }

        private object CanPickupEntity(BasePlayer player, BaseCombatEntity fridge)
        {
            if (player == null || fridge == null) return null;
            if (fridge.ShortPrefabName.Equals("electrical.heater"))
            {
                var f = fridge.GetParentEntity();
                if (f != null)
                {
                    // Block pickup of heater parented by fridge.  True would allow other plugins to override, or so it seems...
                    if (f.ShortPrefabName == "fridge.deployed")
                    {
                        DoLog("Blocked pickup of heater attached to fridge.");
                        return false;
                    }
                }
            }
            else if (fridge.ShortPrefabName.Equals("fridge.deployed"))
            {
                var electrified = fridge.GetComponentInChildren<ElectricalHeater>() ?? null;
                if (electrified == null)
                {
                    return null;
                }
                if (electrified.IsPowered())
                {
                    if (configData.Settings.blockPickup)
                    {
                        // Block pickup when powered, because danger or something.
                        return true;
                    }
                }
            }
            return null;
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            var fridge = container.GetComponentInParent<BaseEntity>();
            if (fridge.ShortPrefabName.Equals("fridge.deployed"))
            {
                var electrified = container.GetComponentInChildren<ElectricalHeater>() ?? null;
                if (electrified == null) return null;
                if (!electrified.IsPowered() && configData.Settings.blockLooting)
                {
                    return false;
                }

                var status = Lang("off");
                if (electrified.IsPowered()) status = Lang("on");

                PowerGUI(player, status);
            }
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

        [Command("fr")]
        private void EnableDisable(IPlayer iplayer, string command, string[] args)
        {
            bool en = configData.Settings.defaultEnabled;
            if (orDefault.Contains(iplayer.Id))
            {
                orDefault.Remove(iplayer.Id);
            }
            else
            {
                orDefault.Add(iplayer.Id);
                en = !en;
            }
            switch (en)
            {
                case true:
                    Message(iplayer, "enabled");
                    break;
                case false:
                    Message(iplayer, "disabled");
                    break;
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
            public bool blockPickup;
            public bool blockLooting;
            public bool defaultEnabled = true;
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
                    timespan = 600f, // 10 minutes
                    blockPickup = true,
                    blockLooting = false,
                    defaultEnabled = true
                },
                Version = Version
            };
            SaveConfig(configData);
        }

        private void LoadConfigValues()
        {
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < new VersionNumber(1, 0, 2))
            {
                configData.Settings.blockPickup = true;
                configData.Settings.blockLooting = false;
            }

            configData.Version = Version;
            SaveConfig(configData);
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
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

        class FoodDecay : MonoBehaviour
        {
            private StorageContainer box;
            private ElectricalHeater heater;

            public void Awake()
            {
                box = GetComponent<StorageContainer>() ?? null;
                Instance.DoLog("Found box");
                //heater = GetComponentInChildren<ElectricalHeater>() ?? null;
                heater = GetComponent<ElectricalHeater>() ?? null;
                Instance.DoLog("Found heater");
                if (heater != null)
                {
                    if (Instance.configData.Settings.decay) InvokeRepeating("ProcessContents", 1, Instance.configData.Settings.timespan);
                }
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
