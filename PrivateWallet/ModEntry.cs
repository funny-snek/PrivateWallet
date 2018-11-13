using System;
using StardewModdingAPI;
using StardewValley;
using System.Collections.Generic;
using StardewValley.Menus;
using StardewModdingAPI.Events;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Netcode;
using StardewValley.Buildings;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Harmony;

namespace PrivateWallet
{
    /// <summary>
    /// Harmony Patches
    /// </summary>
    
    class GetMoney : Patch.Patch
    {
        protected override PatchDescriptor GetPatchDescriptor() => new PatchDescriptor(typeof(Farmer), "get_money");

        public static void Postfix(ref int __result)
        {
            //if game1.isclient || mastergame
            if (!WalletData.PlayerWallets.ContainsKey(Game1.player.uniqueMultiplayerID))
            {
                //need to do below but cant access a non-static dicitionary
                //WalletData.PlayerWallets[Game1.player.UniqueMultiplayerID] = WalletData.PlayerWalletsJSON[Game1.player.UniqueMultiplayerID]
                WalletData.PlayerWallets[Game1.player.UniqueMultiplayerID] = 501;
            }
            __result = WalletData.PlayerWallets[Game1.player.UniqueMultiplayerID];
        }
    }
    class SetMoney : Patch.Patch
    {
        protected override PatchDescriptor GetPatchDescriptor() => new PatchDescriptor(typeof(Farmer), "set_money");

        public static bool Prefix(int value, Farmer __instance)
        {
            //if game1.isclient || mastergame
            WalletData.PlayerWallets[Game1.player.UniqueMultiplayerID] = value;
            return false;
        }
    }
    class parseServerToClientsMessage_Patcher : Patch.Patch  //for receiving messages
    {
        protected override PatchDescriptor GetPatchDescriptor() => new PatchDescriptor(typeof(Multiplayer), "parseServerToClientsMessage");

        public static void Prefix(string message) 
        {
            if (!Game1.IsClient && message.Substring(0, 10) == "!GETWALLET")//money request from client >>> host
            {
                string cleanGetWalletMessage = message.Substring(10); 
                ModEntry.GetWalletMessages.Add(cleanGetWalletMessage);
            }
            // add received deposit messages to a list
            if (!Game1.IsClient && message.Substring(0, 8) == "!DEPOSIT") //send money from client >>> host
            {
                string cleanDepositMessage = message.Substring(8);
                ModEntry.DepositMessages.Add(cleanDepositMessage);
            }
            // add sentwallets to a list
            if (Game1.IsClient && message.Substring(0,11) == "!SENDWALLET") //send wallet from host >>> client
            {
                string cleanWithdrawalMessage = message.Substring(11);
                ModEntry.WithdrawalMessages.Add(cleanWithdrawalMessage);

            }
            //////////////////////////////////////////
            // to send messages
            //Game1.client.sendMessage((byte) 18, myStringMessage)
        }
    }
    // maybe patch this for server messages?
   /* class receiveChatMessage_Patcher : Patch.Patch
    {
        protected override PatchDescriptor GetPatchDescriptor() => new PatchDescriptor(typeof(Multiplayer), "receiveChatMessage");

        public static void Prefix(string message)
        {
           
        }

        //////////////////////////////////////////
        // to send messages
        //Game1.client.sendMessage(Game1.player,(byte) 10, en, myStringMessage)

    }*/
    public class WalletData
    {
        public static Dictionary<long, int> PlayerWallets { get; set; } = new Dictionary<long, int>();
        public Dictionary<long, int> PlayerWalletsJSON { get; set; } = new Dictionary<long, int>();
        public Dictionary<long, int> PreviousDayBackupJSON { get; set; } = new Dictionary<long, int>();
    }

    /// <summary>
    /// SMAPI Code
    /// </summary>
    public class ModEntry : Mod
    {

        public Dictionary<long, int> PlayerWalletsChecker { get; set; } = new Dictionary<long, int>();
        public static WalletData data;

        private bool initialWrite = false;
        private int hourCounter = 600;
        public readonly static List<string> GetWalletMessages = new List<string>();
        public readonly static List<string> DepositMessages = new List<string>();
        public readonly static List<string> WithdrawalMessages = new List<string>();

        public override void Entry(IModHelper helper)
        {
            MultiplayerEvents.BeforeMainSync += Sync; //used bc only thing that gets throug save window
            Patch.Patch.PatchAll("PrivateWallet.PrivateWallet");//harmony patch
            
        }

        private void Sync(object sender, EventArgs e)
        {
            if (Context.IsWorldReady)
            {
                if (initialWrite == false)
                {
                    if (Game1.IsMasterGame)
                    {
                        data = this.Helper.ReadJsonFile<WalletData>($"data/{Constants.SaveFolderName}.json") ?? new WalletData();
                        foreach (KeyValuePair<long, int> x in WalletData.PlayerWallets)
                        {
                            data.PlayerWalletsJSON[x.Key] = x.Value;
                        }
                        this.Helper.WriteJsonFile<WalletData>($"data/{Constants.SaveFolderName}.json", data);
                        initialWrite = true;

                    }
                }
                if (Game1.timeOfDay == hourCounter)
                {
                    var data = this.Helper.ReadJsonFile<WalletData>($"data/{Constants.SaveFolderName}.json") ?? new WalletData();
                    foreach (KeyValuePair<long, int> x in WalletData.PlayerWallets)
                    {
                        data.PlayerWalletsJSON[x.Key] = x.Value;
                    }
                    this.Helper.WriteJsonFile<WalletData>($"data/{Constants.SaveFolderName}.json", data);
                    hourCounter += 100;
                }

                //write some more code to handle if the game crashes before end of day so players don't keep their money + get their items back


                //shipping bin fix
                var lastItemShipped = (StardewValley.Object)Game1.getFarm().lastItemShipped;
                if (lastItemShipped != null)
                {
                    Game1.player.Money += lastItemShipped.sellToStorePrice() * lastItemShipped.Stack;
                    Game1.getFarm().lastItemShipped = null;
                }


            }
        }
    }
                
}
