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
    [Info("Electric Fridge", "RFC1920", "1.0.6")]
    [Description("Is your refrigerator running?")]

    class ElectricFridge : RustPlugin
    {
        private ConfigData configData;
        public static ElectricFridge Instance = null;
        const string FRBTN = "fridge.status";
        private readonly DateTime lastUpdate;

        private List<uint> fridges = new List<uint>();

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));

        private List<string> orDefault = new List<string>();

        private void DoLog(string message)
        {
            if (configData.Settings.debug) Puts(message);
        }

        void Init()
        {
            Instance = this;
            LoadConfigValues();
            AddCovalenceCommand("fr", "EnableDisable");
        }

        void OnServerInitialized()
        {
            LoadData();

            List<uint> toremove = new List<uint>();
            foreach (uint pid in fridges)
            {
                DoLog("Setting up old fridge");
                BaseNetworkable fridge = BaseNetworkable.serverEntities.Find(pid);
                if (fridge == null)
                {
                    toremove.Add(pid);
                    continue;
                }

                DoLog("Adding FoodDecay");
                (fridge as BaseEntity).gameObject.AddComponent<FoodDecay>();
            }
            foreach (uint tr in toremove)
            {
                fridges.Remove(tr);
            }
            SaveData();
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, FRBTN);
            }

            FoodDecay[] decays = Resources.FindObjectsOfTypeAll<FoodDecay>();
            foreach (FoodDecay decay in decays)
            {
                UnityEngine.Object.Destroy(decay);
            }
        }

        private void LoadData()
        {
            fridges = Interface.Oxide.DataFileSystem.ReadObject<List<uint>>(Name + "/fridges");
        }
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name + "/fridges", fridges);
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

        void OnEntitySpawned(BaseEntity fridge)
        {
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
                    Connect(heater, branch);
                }
                fridges.Add(fridge.net.ID);
                SaveData();
            }
        }

        private void Connect(ElectricalHeater heater, ElectricalBranch branch)
        {
            int inputSlot = 0;
            int outputSlot = 1;
            branch.branchAmount = 5;

            IOEntity branchIO = branch as IOEntity;
            IOEntity heaterIO = heater as IOEntity;
            IOEntity.IOSlot branchOutput = branchIO.outputs[outputSlot];
            IOEntity.IOSlot heaterInput = heaterIO.inputs[inputSlot];

            heaterInput.connectedTo = new IOEntity.IORef();
            heaterInput.connectedTo.Set(branch);
            heaterInput.connectedToSlot = outputSlot;
            heaterInput.connectedTo.Init();
            heaterInput.connectedTo.ioEnt._limitedNetworking = true;
            //DoLog($"Heater input slot {inputSlot.ToString()}:{heaterInput.niceName} connected to {branchIO.ShortPrefabName}:{branchOutput.niceName}");

            branchOutput.connectedTo = new IOEntity.IORef();
            branchOutput.connectedTo.Set(heater);
            branchOutput.connectedToSlot = inputSlot;
            branchOutput.connectedTo.Init();
            branchOutput.connectedTo.ioEnt._limitedNetworking = true;
            branch.MarkDirtyForceUpdateOutputs();
            branch.SendNetworkUpdate();
            //DoLog($"Branch output slot {outputSlot.ToString()}:{branchOutput.niceName} connected to {heaterIO.ShortPrefabName}:{heaterInput.niceName}");
        }

        private void OnEntityKill(ElectricalBranch branch)
        {
            BaseEntity f = branch.GetParentEntity();
            if (f != null)
            {
                if (f.ShortPrefabName == "fridge.deployed")
                {
                    if (fridges.Contains(f.net.ID))
                    {
                        fridges.Remove(f.net.ID);
                        SaveData();
                    }
                    f.Kill();
                }
            }
        }
        private void OnEntityKill(ElectricalHeater heater)
        {
            BaseEntity f = heater.GetParentEntity();
            if (f != null)
            {
                if (f.ShortPrefabName == "fridge.deployed")
                {
                    if (fridges.Contains(f.net.ID))
                    {
                        fridges.Remove(f.net.ID);
                        SaveData();
                    }
                    f.Kill();
                }
            }
        }
        private object CanPickupEntity(BasePlayer player, ElectricalHeater heater)
        {
            if (player == null || heater == null) return null;
            BaseEntity f = heater.GetParentEntity();

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
            BaseEntity f = branch.GetParentEntity();

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
                ElectricalHeater electrified = fridge.GetComponentInChildren<ElectricalHeater>() ?? null;
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
                    if (fridges.Contains(fridge.net.ID))
                    {
                        fridges.Remove(fridge.net.ID);
                        SaveData();
                    }
                }
            }
            return null;
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            BaseEntity fridge = container.GetComponentInParent<BaseEntity>();
            if (fridge.ShortPrefabName.Equals("fridge.deployed"))
            {
                ElectricalHeater electrified = container.GetComponentInChildren<ElectricalHeater>() ?? null;
                if (electrified == null) return null;
                if (!electrified.IsPowered() && configData.Settings.blockLooting)
                {
                    return false;
                }

                string status = Lang("off");
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

            private readonly Dictionary<string, string> spoil = new Dictionary<string, string>()
            {
                { "meat.bear.cooked.item", "bearmeat.burned" },
                { "meat.bear.raw.item", "bearmeat.burned" },
                { "meat.bear.burned.item", null },
                { "meat.deer.cooked.item", "deermeat.burned" },
                { "meat.deer.raw.item", "deermeat.burned" },
                { "meat.deer.burned.item", null },
                { "meat.pork.cooked.item", "meat.pork.burned" },
                { "meat.pork.raw.item", "meat.pork.burned" },
                { "meat.pork.burned.item", null },
                { "meat.horse.cooked.item", "horsemeat.burned" },
                { "meat.horse.raw.item", "horsemeat.burned" },
                { "meat.horse.burned.item", null },

                { "meat.wolf.cooked.item", "wolfmeat.burned" },
                { "meat.wolf.burned.item", "wolfmeat.spoiled" },
                { "meat.wolf.raw.item", "wolfmeat.spoiled" },
                { "meat.wolf.spoiled.item", null },
                { "humanmeat_cooked.item", "humanmeat.burned" },
                { "humanmeat_burned.item", "humanmeat.spoiled" },
                { "humanmeat_raw.item", "humanmeat.spoiled" },
                { "humanmeat_spoiled.item", null },
                { "apple.item", "apple.spoiled" },
                { "apple.spoiled.item", null }
            };

            public void Awake()
            {
                box = GetComponent<StorageContainer>() ?? null;
                Instance.DoLog("Found box");
                heater = GetComponentInChildren<ElectricalHeater>() ?? null;
                if (heater != null && Instance.configData.Settings.decay)
                {
                    Instance.DoLog("Found heater");
                    InvokeRepeating("ProcessContents", 0, Instance.configData.Settings.timespan);
                }
            }

            public void OnDisable()
            {
                CancelInvoke("ProcessContents");
                Destroy(this);
            }

            private void ProcessContents()
            {
                if (Instance.configData.Settings.foodDecay <= 0) return;
                if (heater.IsPowered())
                {
                    Instance.DoLog("Fridge has power. Skipping...");
                    return;
                }

                foreach (Item item in box.inventory.itemList.ToArray())
                {
                    int oldamt = item.amount;
                    Instance.DoLog($"Found {oldamt.ToString()} of {item.info.name}");

                    if (item.amount >= 1)
                    {
                        if (item.contents != null)
                        {
                            if (item.contents.allowedContents == ItemContainer.ContentsType.Liquid)
                            {
                                Instance.DoLog("Water container found");
                                if (item.contents.IsEmpty())
                                {
                                    Instance.DoLog($"Removing empty {item.info.name}");
                                    item.Remove(0);
                                }
                                else
                                {
                                    int oldwater = item.contents.GetAmount(-1779180711, true);
                                    int newwater =  (int)Mathf.Floor(oldwater * Instance.configData.Settings.foodDecay);
                                    Instance.DoLog($"Removing {(oldwater-newwater).ToString()} from {item.info.name}");
                                    item.contents.Take(null, -1779180711, oldwater - newwater);
                                }
                            }
                        }
                        else if (!spoil.ContainsKey(item.info.name) || !Instance.configData.Settings.spoilFood || box.inventory.IsFull())
                        {
                            item.amount = (int)Mathf.Floor(item.amount * Instance.configData.Settings.foodDecay);
                            if (item.amount < 1)
                            {
                                Instance.DoLog($"Removing last {item.info.name} in stack");
                                item.Remove(0);
                            }
                            Instance.DoLog($"..old amount: {oldamt.ToString()}, new amount {item.amount.ToString()}");
                        }
                        else if (Instance.configData.Settings.spoilFood)
                        {
                            Instance.DoLog($"Spoiling/burning food");
                            string newitemname = null;
                            if (spoil.ContainsKey(item.info.name))
                            {
                                Instance.DoLog($"Found spoilable item, {item.info.name}");
                                newitemname = spoil[item.info.name];
                                Instance.DoLog($"..will replace with {newitemname}");
                            }

                            if (newitemname == null)
                            {
                                Instance.DoLog($"Removing last {item.info.name} in stack");
                                item.Remove(0);
                            }
                            else
                            {
                                item.amount = (int)Mathf.Floor(item.amount * Instance.configData.Settings.foodDecay);
                                if (item.amount < 1)
                                {
                                    Instance.DoLog($"Removing last {item.info.name} in stack");
                                    item.Remove(0);
                                }
                                Item newitem = ItemManager.CreateByName(newitemname);
                                newitem.MoveToContainer(box.inventory);
                                box.inventory.MarkDirty();
                                Instance.DoLog($"..old amount: {oldamt.ToString()}, new amount {item.amount.ToString()}");
                            }
                        }
                    }
                }

                box.UpdateNetworkGroup();
                box.SendNetworkUpdateImmediate();
            }
        }
    }
}
