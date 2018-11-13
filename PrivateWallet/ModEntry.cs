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
            __result = ModEntry.localMoney;  // this variable is being broadcast and shared between all clients and host, change it one place updates everywhere
        }
    }
    class SetMoney : Patch.Patch
    {
        protected override PatchDescriptor GetPatchDescriptor() => new PatchDescriptor(typeof(Farmer), "set_money");

        public static bool Prefix(int value, Farmer __instance)
        {
            //if game1.isclient || mastergame
            ModEntry.localMoney = value;
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
        public Dictionary<long, int> PlayerWallets { get; set; } = new Dictionary<long, int>();
    }

    /// <summary>
    /// SMAPI Code
    /// </summary>
    public class ModEntry : Mod
    {
        public static int localMoney = 501;
        public int moneyChecker = -5; // to compare to see if money amount in local money has changed
        private bool hasHostBeenAskedForWalletYet = false;
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
                
                if (hasHostBeenAskedForWalletYet == true)
                {
                    //send deposit to wallet
                    if (moneyChecker != localMoney)
                    {
                        
                        moneyChecker = localMoney;
                        if (!Game1.IsClient)
                        {
                            var data = this.Helper.ReadJsonFile<WalletData>($"data/{Constants.SaveFolderName}.json") ?? new WalletData();
                            data.PlayerWallets[Game1.player.UniqueMultiplayerID] = localMoney;
                            this.Helper.WriteJsonFile<WalletData>($"data/{Constants.SaveFolderName}.json", data);
                        }
                        if (Game1.IsClient)
                        {
                            string myDepositMessage = $"!DEPOSIT{Game1.player.UniqueMultiplayerID}:{localMoney}";
                            Game1.client.sendMessage((byte)18, myDepositMessage);
                            
                        }

                    }
                }
                //deposit client messages into wallets
                if (!Game1.IsClient)
                {
                    if (DepositMessages.Count() > 0)
                    {
                        foreach (string message in DepositMessages)
                        {
                            var data = this.Helper.ReadJsonFile<WalletData>($"data/{Constants.SaveFolderName}.json") ?? new WalletData();
                            long playerIDforDeposit = Convert.ToInt64(message.Split(new char[] { ':' })[0]);
                            int money = Convert.ToInt32(message.Split(new char[] { ':' })[1]);
                            data.PlayerWallets[playerIDforDeposit] = money;
                            this.Helper.WriteJsonFile<WalletData>($"data/{Constants.SaveFolderName}.json", data);
                            
                        }
                        DepositMessages.Clear();
                    }
                }


                //get wallet ammount
                if (hasHostBeenAskedForWalletYet == false)
                {
                    if (!Game1.IsClient)
                    {
                        var data = this.Helper.ReadJsonFile<WalletData>($"data/{Constants.SaveFolderName}.json") ?? new WalletData();
                        if (!data.PlayerWallets.Keys.Contains(Game1.player.UniqueMultiplayerID))
                        {
                            data.PlayerWallets[Game1.player.UniqueMultiplayerID] = 501;
                            this.Helper.WriteJsonFile<WalletData>($"data/{Constants.SaveFolderName}.json", data);
                            return;
                        }
                        localMoney = data.PlayerWallets[Game1.player.UniqueMultiplayerID];
                        hasHostBeenAskedForWalletYet = true;
                    }
                    if (Game1.IsClient)
                    {
                        string myGetWalletMessage = $"!GETWALLET{Game1.player.UniqueMultiplayerID}";
                        Game1.client.sendMessage((byte)18, myGetWalletMessage);
                        this.Monitor.Log($"{localMoney}");
                    }
                }

                //send wallet message to client 
                if (!Game1.IsClient)
                {
                    if (GetWalletMessages.Count() > 0)
                    {
                        foreach (string message in GetWalletMessages)
                        {
                            var data = this.Helper.ReadJsonFile<WalletData>($"data/{Constants.SaveFolderName}.json") ?? new WalletData();
                            long playerIDforWithdrawal = Convert.ToInt64(message);
                            if (!data.PlayerWallets.Keys.Contains(playerIDforWithdrawal))
                            {
                                data.PlayerWallets[playerIDforWithdrawal] = 501;
                                this.Helper.WriteJsonFile<WalletData>($"data/{Constants.SaveFolderName}.json", data);
                                return;
                            }
                            int playerAmountWithdrawal = data.PlayerWallets[playerIDforWithdrawal];
                            string sendWalletMessage = $"!SENDWALLET{playerIDforWithdrawal}:{playerAmountWithdrawal}";
                            OutgoingMessage myServerMessage = new OutgoingMessage((byte)18, Game1.player.UniqueMultiplayerID, sendWalletMessage);
                            Game1.server.sendMessage(Game1.player.UniqueMultiplayerID, myServerMessage); 
                            //this message isn't going through
                            
                        }
                        GetWalletMessages.Clear();
                    }
                }

                //add received wallet amount to local money
                if (!Game1.IsClient)
                {
                    if (WithdrawalMessages.Count > 0)
                    {
                        foreach (string message in WithdrawalMessages)
                        {
                            long playerIDforWithdrawal = Convert.ToInt64(message.Split(new char[] { ':' })[0]);
                            if (playerIDforWithdrawal == Game1.player.UniqueMultiplayerID)
                            {
                                localMoney = Convert.ToInt32(message.Split(new char[] { ':' })[1]);
                                hasHostBeenAskedForWalletYet = true;
                            }
                            
                        }
                        WithdrawalMessages.Clear();
                    }
                }

            }
            if(Game1.activeClickableMenu is TitleMenu || Game1.activeClickableMenu is CoopMenu)
            {
                hasHostBeenAskedForWalletYet = false;
                localMoney = 501;
                moneyChecker = -5;
                GetWalletMessages.Clear();
                DepositMessages.Clear();
                WithdrawalMessages.Clear();
            }

            if (Context.IsWorldReady)
            {
                var lastItemShipped = (StardewValley.Object)Game1.getFarm().lastItemShipped;
                if (lastItemShipped != null)
                {
                    Game1.player.Money += lastItemShipped.sellToStorePrice() * lastItemShipped.Stack;
                    Game1.getFarm().lastItemShipped = null;
                }

            }
            //set up backup dictionary at start of day, if day doesn't save load backup dictionary
        }


         
        
    }
}
