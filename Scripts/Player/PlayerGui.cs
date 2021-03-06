// Copyright (C) by Upvoid Studios
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Engine.Webserver;
using Engine.Gui;
using Engine.Input;
using Engine.Rendering;

namespace UpvoidMiner
{
    /// <summary>
    /// Provides the ingame information for all player-centric gui elements (like the inventory, minimap, character info, etc.)
    /// </summary>
    public class PlayerGui
    {
        /// <summary>
        /// Returns true if any form of UI is open (and mouse should be visible and movable).
        /// </summary>
        public bool IsInventoryOpen { get; set; }
        public bool IsUIOpen { get; set; }
		public bool IsMenuOpen { get; set; }

		Player player;

        JsonSerializer json = new JsonSerializer();
        WebSocketHandler updateSocket;

        // Container class for the data we send to the GUI client.
        [Serializable]
        class GuiInfo
        {
            [Serializable]
            public class GuiItem {

                public GuiItem() { }
                
                public GuiItem(Item item)
                {
                    icon = item.Icon;
                    id = item.Id;
                    identifier = item.Identifier;
                    name = item.Name;
                    quickAccessSlot = item.QuickAccessIndex;
                    quantity = 1.0f;
                    isVolumetric = false;

                    VolumeItem volumeItem = item as VolumeItem;
                    if(volumeItem != null)
                    {
                        isVolumetric = true;
                        quantity = volumeItem.Volume;
                    }

                    DiscreteItem discreteItem = item as DiscreteItem;
                    if(discreteItem != null)
                    {
                        quantity = discreteItem.StackSize;
                    }
                }

                public string icon = "";
                public long id = -1;
                public string name = "";
                public string identifier = "";
                public int quickAccessSlot = -1;
                public float quantity = 0;
                public bool isVolumetric = false;
                public bool hasDiscoveredCraftingRule = false;
                public bool canBeCrafted = false;
                public bool canBeDismantled = false;
                public List<GuiItem> craftingIngredients = new List<GuiItem>();

                public static Dictionary<string, GuiItem> FromItemCollection(IEnumerable<Item> items)
                {
                    Dictionary<string, GuiItem> guiItems = new Dictionary<string, GuiItem>();
                    foreach(Item item in items)
                    {
                        guiItems.Add(item.Identifier, new GuiItem(item));
                    }

                    return guiItems;
                }
            }

            public Dictionary<string, GuiItem> inventory = new Dictionary<string, GuiItem>();
            public List<string> quickAccess = new List<string>();
            public int selection;

			public bool playerIsFrozen;
        }

        private void toggleUI()
        {
            IsUIOpen = !IsUIOpen;
            updateSocket.SendMessage("ToggleUI");
        }

        private void toggleInventory()
        {
            IsInventoryOpen = !IsInventoryOpen;
            updateSocket.SendMessage("ToggleInventory"); 
        }

        private void toggleMenu()
        {
            IsMenuOpen = !IsMenuOpen;
            if(IsMenuOpen)
            {
                Gui.DefaultUI.LoadURL("http://localhost:" + Webserver.DefaultWebserver.Port + "/Mods/Upvoid/UpvoidMiner/0.0.1/MainMenu.html");
                IsInventoryOpen = true;
            }
            else
            {
                Gui.DefaultUI.LoadURL("http://localhost:" + Webserver.DefaultWebserver.Port + "/Mods/Upvoid/UpvoidMiner/0.0.1/IngameGui.html");
                IsInventoryOpen = false;
            }
        }

        public PlayerGui(Player player)
        {
            IsUIOpen = true;

            Engine.Windows.Windows.GetWindow(0).OnFocusLoss += delegate { if (!IsMenuOpen && !LocalScript.NoclipEnabled) toggleMenu(); };

            // The IngameGui.html in htdocs/ contains the actual player gui. It contains javascript functions that get the ingame information displayed.
            // These dynamic content handlers provide that information.
            this.player = player;
            Webserver.DefaultWebserver.RegisterDynamicContent(UpvoidMiner.ModDomain, "IngameGuiData", webInventory);
            Webserver.DefaultWebserver.RegisterDynamicContent(UpvoidMiner.ModDomain, "SelectQuickAccessSlot", webSelectQuickAccessSlot);
            Webserver.DefaultWebserver.RegisterDynamicContent(UpvoidMiner.ModDomain, "SelectItem", webSelectItem);
            Webserver.DefaultWebserver.RegisterDynamicContent(UpvoidMiner.ModDomain, "DropItem", webDropItem);
            updateSocket = new WebSocketHandler();
            Webserver.DefaultWebserver.RegisterWebSocketHandler(UpvoidMiner.ModDomain, "InventoryUpdate", updateSocket);

            // On all relevant changes in the inventory, we order the GUI client to update itself.
            player.Inventory.OnSelectionChanged += (arg1, arg2) => OnUpdate();
            player.Inventory.OnQuickAccessChanged += (arg1, arg2) => OnUpdate();
            player.Inventory.Items.OnAdd += arg1 => OnUpdate();
            player.Inventory.Items.OnRemove += arg1 => OnUpdate();
            player.Inventory.Items.OnQuantityChange += arg1 => OnUpdate();

            // Workaround for missing keyboard input in the Gui: Toggle the inventory from here
            Input.OnPressInput += (object sender, InputPressArgs e) => 
            { 
                if(e.Key == InputKey.I && e.PressType == InputPressArgs.KeyPressType.Down) 
                {
                    if ( !IsUIOpen )
                        toggleUI();
                    toggleInventory();
                }
                if(e.Key == InputKey.F4 && e.PressType == InputPressArgs.KeyPressType.Down) 
                {
                    toggleUI();
                    if ( !IsUIOpen && IsInventoryOpen )
                        toggleInventory();
                }
				if(e.Key == InputKey.Escape && e.PressType == InputPressArgs.KeyPressType.Down) 
				{
                    toggleMenu();
				}
            };
        }

        /// <summary>
        /// Is called when GUI changes for the player occurred.
        /// </summary>
        public void OnUpdate()
        {
            // This sends a message via our update socket to the GUI client, which will then update itself.
            updateSocket.SendMessage("Update");
        }

        void webInventory(WebRequest request, WebResponse response)
        {
            // Compile all relevant info for the gui into a GuiInfo instance and send it to the GUI client.
            GuiInfo info = new GuiInfo();

			info.playerIsFrozen = player.IsFrozen;

            info.inventory = GuiInfo.GuiItem.FromItemCollection(player.Inventory.Items);

            foreach (var item in player.Inventory.QuickAccessItems)
            {
                if (item != null)
                    info.quickAccess.Add(item.Identifier);
                else
                    info.quickAccess.Add("");
            }

            info.selection = player.Inventory.SelectionIndex;
            /*
            foreach (CraftingRule cr in player.Inventory.DiscoveredRules)
            {
                if (info.inventory.ContainsKey(cr.Result.Identifier))
                {
                    GuiInfo.GuiItem item = info.inventory[cr.Result.Identifier];
                    item.hasDiscoveredCraftingRule = true;
                    if (cr.CanBeDismantled)
                        item.canBeDismantled = true;
                    if (cr.IsCraftable(player.Inventory.Items.ItemFromIdentifier(item.identifier), player.Inventory.Items))
                    {
                        item.canBeCrafted = true;
                    }

                    item.craftingIngredients = new List<GuiInfo.GuiItem>(GuiInfo.GuiItem.FromItemCollection(cr.Ingredients).Values);
                }
                else
                {
                    GuiInfo.GuiItem virtualItem = new GuiInfo.GuiItem(cr.Result);
                    virtualItem.quantity = 0;
                    virtualItem.hasDiscoveredCraftingRule = true;

                    if (cr.IsCraftable(null, player.Inventory.Items))
                    {
                        virtualItem.canBeCrafted = true;
                    }

                    virtualItem.craftingIngredients = new List<GuiInfo.GuiItem>(GuiInfo.GuiItem.FromItemCollection(cr.Ingredients).Values);

                    info.inventory.Add(cr.Result.Identifier, virtualItem);
                }
            }
            */
            StringWriter writer = new StringWriter();
            JsonTextWriter jsonWriter = new JsonTextWriter(writer);
            json.Formatting = Formatting.Indented;
            json.Serialize(jsonWriter, info);
            response.AddHeader("Content-Type", "application/json");
            response.AppendBody(writer.GetStringBuilder().ToString());
        }

        void webSelectQuickAccessSlot(WebRequest request, WebResponse response)
        {
            // The GUI client calls this when a quick access slot is selected. Get the selected index and pass it to the player inventory.
            int selectedIndex = Convert.ToInt32(request.GetQuery("selectedIndex"));
            player.Inventory.Select(selectedIndex);
        }

        void webSelectItem(WebRequest request, WebResponse response)
        {
            // The GUI client calls this when a item in the inventory is selected.

            // Get the selected item
            int selectedItemId = Convert.ToInt32(request.GetQuery("itemId"));
            Item item = player.Inventory.Items.ItemById(selectedItemId);

            if (item == null)
                return;

            // Place the item in the quick access bar at position 9 (bound to key 0) and select it.
            player.Inventory.SetQuickAccess(item, 9);
            player.Inventory.Select(9);
        }

        void webDropItem(WebRequest request, WebResponse response)
        {
            // The GUI client calls this when an item is droppped from the inventory.

            int itemId = Convert.ToInt32(request.GetQuery("itemId"));
            Item item = player.Inventory.Items.ItemById(itemId);

            if (item == null)
                return;

            player.DropItem(item);
        }

        void webDismantleItem(WebRequest request, WebResponse response)
        {
            // The GUI client calls this when an item is droppped from the inventory.

            int itemId = Convert.ToInt32(request.GetQuery("itemId"));
            Item item = player.Inventory.Items.ItemById(itemId);

            if (item == null)
                return;

            // For dismantling, we need a crafting rule that results in the given item
            foreach (var cr in player.Inventory.DiscoveredRules) {
                if (cr.CouldBeDismantled(item))
                {
                    //TODO: perform dismantling
                    break;
                }
            }
        }

        void webCraftItem(WebRequest request, WebResponse response)
        {
            // The GUI client calls this when the player crafts an item.

            string itemIdentifier = request.GetQuery("itemIdentifier");
            Item item = player.Inventory.Items.ItemFromIdentifier(itemIdentifier);

            if (item == null)
                return;

            // For crafting, we need a crafting rule that results in the given item
            foreach (var cr in player.Inventory.DiscoveredRules) {
                if (cr.CouldBeCraftable(item))
                {
                    cr.Craft(item, player.Inventory.Items);
                    break;
                }
            }
        }
    }
}

