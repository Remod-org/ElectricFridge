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
        private readonly DateTime lastUpdate;
        private bool enabled = false;

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));

        private List<string> orDefault = new List<string>();

        private void DoLog(string message)
        {
            if (configData.Settings.debug) Puts(message);
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

        private void Loaded()
        {
            FoodDecay[] decays = UnityEngine.Object.FindObjectsOfType<FoodDecay>();
            List<uint> parents = new List<uint>();
            foreach (var decay in decays)
            {
                parents.Add(decay.GetComponentInParent<BaseEntity>().net.ID);
            }

            foreach (uint pid in parents)
            {
                var parent = BaseNetworkable.serverEntities.Find(pid);
                var oldfc = parent.GetComponentInChildren<FoodDecay>();
                UnityEngine.Object.Destroy(oldfc);
                parent.gameObject.AddComponent<FoodDecay>();
            }
        }
        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
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

                BaseEntity bent = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/gates/branch/electrical.branch.deployed.prefab", fridge.transform.position, fridge.transform.rotation, true);
                ElectricalBranch branch = bent as ElectricalBranch;
                if (bent != null)
                {
                    bent.transform.localEulerAngles = new Vector3(0, 270, 180);
                    bent.transform.localPosition = new Vector3(-0.49f, 0.65f, 0);
                    bent.OwnerID = fridge.OwnerID;
                    bent.SetParent(fridge);
                    UnityEngine.Object.Destroy(bent.GetComponent<DestroyOnGroundMissing>());
                    UnityEngine.Object.Destroy(bent.GetComponent<GroundWatch>());
                    bent.Spawn();
                }

                BaseEntity hent = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/electricheater/electrical.heater.prefab", fridge.transform.position, fridge.transform.rotation, true);
                ElectricalHeater heater = hent as ElectricalHeater;
                if (hent != null)
                {
                    hent.transform.localEulerAngles = new Vector3(90, 90, 270);
                    hent.transform.localPosition = new Vector3(0, 0.65f, 0);
                    hent.OwnerID = fridge.OwnerID;
                    hent.SetParent(fridge);
                    UnityEngine.Object.Destroy(hent.GetComponent<DestroyOnGroundMissing>());
                    UnityEngine.Object.Destroy(hent.GetComponent<GroundWatch>());
                    hent.Spawn();
                }
                DoLog("Adding FoodDecay object");
                fridge.gameObject.AddComponent<FoodDecay>();

                if (heater != null && branch != null)
                {
                    var inputSlot = 0;
                    var outputSlot = 1;
                    branch.branchAmount = 5;

                    var branchIO = branch as IOEntity;
                    var heaterIO = heater as IOEntity;
                    IOEntity.IOSlot branchOutput = branchIO.outputs[outputSlot];
                    IOEntity.IOSlot heaterInput = heaterIO.inputs[inputSlot];

                    heaterInput.connectedTo = new IOEntity.IORef();
                    heaterInput.connectedTo.Set(branch);
                    heaterInput.connectedToSlot = outputSlot;
                    heaterInput.connectedTo.Init();
                    heaterInput.connectedTo.ioEnt._limitedNetworking = true;
                    Puts($"Heater input slot {inputSlot.ToString()}:{heaterInput.niceName} connected to {branchIO.ShortPrefabName}:{branchOutput.niceName}");

                    branchOutput.connectedTo = new IOEntity.IORef();
                    branchOutput.connectedTo.Set(heater);
                    branchOutput.connectedToSlot = inputSlot;
                    branchOutput.connectedTo.Init();
                    branchOutput.connectedTo.ioEnt._limitedNetworking = true;
                    branch.MarkDirtyForceUpdateOutputs();
                    branch.SendNetworkUpdate();
                    Puts($"Branch output slot {outputSlot.ToString()}:{branchOutput.niceName} connected to {heaterIO.ShortPrefabName}:{heaterInput.niceName}");
                }
            }
        }

        private void OnEntityKill(ElectricalBranch branch)
        {
            var f = branch.GetParentEntity();
            if (f != null)
            {
                if (f.ShortPrefabName == "fridge.deployed")
                {
                    f.Kill();
                }
            }
        }
        private void OnEntityKill(ElectricalHeater heater)
        {
            var f = heater.GetParentEntity();
            if (f != null)
            {
                if (f.ShortPrefabName == "fridge.deployed")
                {
                    f.Kill();
                }
            }
        }
        private object CanPickupEntity(BasePlayer player, ElectricalHeater heater)
        {
            if (player == null || heater == null) return null;
            var f = heater.GetParentEntity();

            if (f != null)
            {
                if (f.ShortPrefabName.Equals("fridge.deployed"))
                {
                    return false;
                }
            }
            return null;
        }
        private object CanPickupEntity(BasePlayer player, ElectricalBranch branch)
        {
            if (player == null || branch == null) return null;
            var f = branch.GetParentEntity();

            if (f != null)
            {
                if (f.ShortPrefabName.Equals("fridge.deployed"))
                {
                    return false;
                }
            }
            return null;
        }
        private object CanPickupEntity(BasePlayer player, BaseCombatEntity fridge)
        {
            if (player == null || fridge == null) return null;
            if (fridge.ShortPrefabName.Equals("fridge.deployed"))
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
            if (entity == null) return;
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
            public bool debug;
            public bool decay;
            public float foodDecay;
            public bool spoilFood;
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
                    debug = false,
                    branding = "Frigidaire",
                    foodDecay = 0.98f, // 2% loss per timespan, rounded down.
                    spoilFood = false,
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
                if (hexColor.StartsWith("#"))
                {
                    hexColor = hexColor.Substring(1);
                }
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }

        private class FoodDecay : MonoBehaviour
        {
            private StorageContainer box;
            private ElectricalHeater heater;
            private List<string> skipitems = new List<string>() { "pumpkin", "can_beans", "can_tuna", "green_berry", "blue_berry", "white_berry", "yellow_berry", "blueberries", "raspberries" };
            private List<string> usitems = new List<string>() { "chicken", "humanmeat", "apple" };
            private List<string> dotitems = new List<string>() { "meat.wolf" };

            public void Awake()
            {
                box = GetComponent<StorageContainer>() ?? null;
                Instance.DoLog("Found box");
                heater = GetComponentInChildren<ElectricalHeater>() ?? null;
                //heater = GetComponent<ElectricalHeater>() ?? null;
                if (heater != null && Instance.configData.Settings.decay)
                {
                    Instance.DoLog("Found heater");
                    InvokeRepeating("ProcessContents", 0, Instance.configData.Settings.timespan);
                }
            }

            public void OnDisable()
            {
                CancelInvoke("ProcessContents");
            }

            private void ProcessContents()
            {
                if (Instance.configData.Settings.foodDecay <= 0) return;
                if (heater.IsPowered())
                {
                    Instance.DoLog("Fridge has power. Skipping...");
                    return;
                }

                if (Instance.configData.Settings.spoilFood)
                {
                    foreach (Item item in box.inventory.itemList)
                    {
                        Instance.DoLog($"Checking item name {item.name.ToString()}");
                        if (item.name.Contains("spoiled")) continue;
                        if (item.name.Contains("burned")) continue;
                        if (item.amount < 1) continue;

                        string oldamt = item.amount.ToString();
                        item.amount = (int)Mathf.Floor(item.amount * Instance.configData.Settings.foodDecay);
                        int diff = item.amount - int.Parse(oldamt);
                        if (diff >= 1)
                        {
                            string newitemname = null;
                            if (skipitems.Contains(item.name))
                            {
                                continue;
                            }
                            else if (usitems.Contains(item.name))
                            {
                                newitemname = item.name + "_spoiled";
                            }
                            else if (dotitems.Contains(item.name))
                            {
                                newitemname = item.name + ".spoiled";
                            }
                            else
                            {
                                newitemname = item.name + ".burned";
                            }
                            Item newitem = ItemManager.CreateByName(newitemname, diff);
                            newitem.MoveToContainer(box.inventory);
                        }
                    }
                }
                else
                {
                    foreach (Item item in box.inventory.itemList)
                    {
                        if (item.amount < 1) continue;
                        string oldamt = item.amount.ToString();
                        item.amount = (int)Mathf.Floor(item.amount * Instance.configData.Settings.foodDecay);
                    }
                }
                box.UpdateNetworkGroup();
                box.SendNetworkUpdateImmediate();
            }
        }
    }
}
