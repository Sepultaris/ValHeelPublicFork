using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.ValheelMods
{
    public static class ValHeelCurrencyMarket
    {
        private const long InitialPyrealValue = 1;
        private const long InitialMMDValue = 250_000;
        private const long InitialCarnageTokensValue = 1_250_000;
        private const long InitialAshCoinValue = 12_500_000;
        private const long ACVolumeCap = 800_000_000;
        private const long CTVolumeCap = 800_000_000;

        private static int PyrealValue;
        private static long MMDValue;
        public static long CTValue;
        public static long ACValue;
        private static int ACUsers;
        private static int CTUsers;
        private static int Pop;
        private static long ACCount;
        private static long CTCount;
        private static double ACToCTExchangeRate = 0;
        private static double CTToACExchangeRate = 0;

        public static long CTInCirculation;
        public static long ACInCirculation;

        public static void CalculateACValue(long circulation)
        {
            double demandFactor = Math.Min(1.0, ACUsers / (double)Pop);

            double supplyFactor = 1.0 - Math.Min(1.0, circulation / (double)ACVolumeCap);

            if (circulation > ACVolumeCap)
            {
                double circulationOverCap = circulation - ACVolumeCap;
                supplyFactor = Math.Max(0.0, 1.0 - circulationOverCap / (double)ACVolumeCap);
            }

            long acValue = InitialAshCoinValue + (long)(InitialAshCoinValue * demandFactor * supplyFactor);

            acValue = Math.Max(acValue, 1);

            ACValue = acValue;
            QueryACCirculation();
        }

        public static void CalculateCTValue(long circulation)
        {
            double demandFactor = Math.Min(1.0, CTUsers / (double)Pop);

            double supplyFactor = 1.0 - Math.Min(1.0, circulation / (double)CTVolumeCap);

            if (circulation > CTVolumeCap)
            {
                double circulationOverCap = circulation - CTVolumeCap;
                supplyFactor = Math.Max(0.0, 1.0 - circulationOverCap / (double)CTVolumeCap);
            }

            long ctValue = InitialCarnageTokensValue + (long)(InitialCarnageTokensValue * demandFactor * supplyFactor);

            ctValue = Math.Max(ctValue, 1);

            CTValue = ctValue;
            QueryCTCirculation();
        }

        public static void QueryACCirculation()
        {
            ACUsers = 0;
            ACCount = 0;
            
            List<Player> online = new List<Player>();
            List<OfflinePlayer> offline = new List<OfflinePlayer>();

            foreach (var p in PlayerManager.GetAllOnline())
            {
                if (p.BankedAshcoin != null)
                    online.Add(p);
            }

            foreach (var i in PlayerManager.GetAllOffline())
            {
                if (i.BankedAshcoin != null)
                    offline.Add(i);
            }

            List<IPlayer> allPlayers = new List<IPlayer>();
            allPlayers.AddRange(online);
            allPlayers.AddRange(offline);

            foreach (var p in allPlayers)
            {
                ACCount += (long)p.BankedAshcoin;
                ACUsers++;
            }

            List<IPlayer> population = new List<IPlayer>();

            foreach (var p in PlayerManager.GetAllOnline())
            {
                population.Add(p);
            }

            foreach (var i in PlayerManager.GetAllOffline())
            {
                population.Add(i);
            }

            foreach (var p in population)
            {
                Pop++;
            }

            ACInCirculation = ACCount;
        }

        public static void QueryCTCirculation()
        {
            CTUsers = 0;
            CTCount = 0;
            
            List<Player> online = new List<Player>();
            List<OfflinePlayer> offline = new List<OfflinePlayer>();

            foreach (var p in PlayerManager.GetAllOnline())
            {
                if (p.BankedCarnageTokens != null)
                    online.Add(p);
            }

            foreach (var i in PlayerManager.GetAllOffline())
            {
                if (i.BankedCarnageTokens != null)
                    offline.Add(i);
            }

            List<IPlayer> allPlayers = new List<IPlayer>();
            allPlayers.AddRange(online);
            allPlayers.AddRange(offline);

            foreach (var p in allPlayers)
            {
                CTCount += (long)p.BankedCarnageTokens;
                CTUsers++;
            }

            List<IPlayer> population = new List<IPlayer>();

            foreach (var p in PlayerManager.GetAllOnline())
            {
                population.Add(p);
            }

            foreach (var i in PlayerManager.GetAllOffline())
            {
                population.Add(i);
            }

            CTInCirculation = CTCount;
        }

        public static void InitializeCurrencyValues()
        {
            if (System.IO.File.Exists("MarketData.json"))
            {
                string json = System.IO.File.ReadAllText("MarketData.json");
                dynamic values = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                PyrealValue = values.PyrealValue;
                MMDValue = values.MMDValue;
                CTValue = values.CTValue;
                ACValue = values.ACValue;
                CTInCirculation = values.CTInCirculation;
                ACInCirculation = values.ACInCirculation;
                ACToCTExchangeRate = ACValue / CTValue;
                CTToACExchangeRate = CTValue / ACValue;

                QueryACCirculation();
                QueryCTCirculation();
                CalculateACValue(ACInCirculation);
                CalculateCTValue(CTInCirculation);
            }
            else
            {
                PyrealValue = 1;
                MMDValue = 250_000;
                CTValue = 1_250_000;
                ACValue = 12_500_000;
                CTInCirculation = 0;
                ACInCirculation = 0;
                ACToCTExchangeRate = ACValue / CTValue;
                CTToACExchangeRate = CTValue / ACValue;

                QueryACCirculation();
                QueryCTCirculation();
                CalculateACValue(ACInCirculation);
                CalculateCTValue(CTInCirculation);
            }
        }

        public static void BuyAshCoins(Player player, long amount)
        {
            long totalCost = amount * ACValue;

            if (player.BankedPyreals >= totalCost)
            {
                player.BankedAshcoin += amount;
                player.BankedPyreals = player.BankedPyreals - totalCost;
                ACInCirculation += amount;

                CalculateACValue(ACInCirculation);
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have purchased {amount} AshCoin at a cost of {totalCost} Pyreals.", ChatMessageType.System));
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"New AshCoin Value {ACValue}.", ChatMessageType.System));
            }
            else
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough Pyreals to purchase {amount} AshCoin. Cost: {totalCost}Py.", ChatMessageType.System));
            }
        }

        public static void ExchangeCtForAc(Player player, long amount)
        {
            long exchangeRate = ACValue / CTValue;
            long costInCt = exchangeRate * amount;
            long amountOfAc = amount * exchangeRate;

            if (player.BankedCarnageTokens >= costInCt)
            {
                player.BankedAshcoin += amountOfAc;
                player.BankedCarnageTokens = player.BankedCarnageTokens - costInCt;
                CTInCirculation -= costInCt;
                ACInCirculation += amountOfAc;

                CalculateACValue(ACInCirculation);
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have exhchanged {costInCt} Carnage Tokens for {amountOfAc} AshCoin.", ChatMessageType.System));
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"New AshCoin Value {ACValue}.", ChatMessageType.System));
            }
            else
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough Carnage Tokens to purchase AshCoin. Exchange Rate: {exchangeRate}Ct", ChatMessageType.System));
            }
        }

        public static void SellAshCoins(Player player, long amount)
        {
            long totalCost = amount * ACValue;

            if (player.BankedAshcoin >= amount)
            {
                player.BankedAshcoin -= amount;
                player.BankedPyreals = player.BankedPyreals + totalCost;
                ACInCirculation -= amount;

                CalculateACValue(ACInCirculation);
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have sold {amount} AshCoin at a cost of {totalCost} Pyreals.", ChatMessageType.System));
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"New AshCoin Value {ACValue}.", ChatMessageType.System));
            }
            else
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough AshCoin to sell {amount} AshCoin. Cost: {totalCost}Ac", ChatMessageType.System));
            }
        }

        public static void BuyCarnageTokens(Player player, long amount)
        {
            long totalCost = amount * CTValue;

            if (player.BankedPyreals >= totalCost)
            {
                player.BankedCarnageTokens += amount;
                player.BankedPyreals = player.BankedPyreals - totalCost;
                CTInCirculation += amount;

                CalculateCTValue(CTInCirculation);
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have purchased {amount} Carnage Tokens at a cost of {totalCost} Pyreals.", ChatMessageType.System));
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"New Carnage Token Value {CTValue}.", ChatMessageType.System));
            }
            else
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough Pyreals to purchase {amount} Carnage Tokens. Cost: {totalCost}Py", ChatMessageType.System));
            }   
        }

        public static void ExchangeAcForCt(Player player, long amount)
        {
            var amountOfCt = (ACValue / CTValue) * amount;

            if (player.BankedAshcoin >= amount)
            {
                player.BankedCarnageTokens += amountOfCt;
                player.BankedAshcoin = player.BankedAshcoin - amount;
                ACInCirculation -= amount;
                CTInCirculation += amountOfCt;

                CalculateCTValue(CTInCirculation);
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have exchanged {amount} AshCoin for {amountOfCt} Carnage Tokens.", ChatMessageType.System));
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"New Carnage Token Value {CTValue}.", ChatMessageType.System));
            }
            else
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough AshCoin to purchase {amount} Carnage Tokens.", ChatMessageType.System));
            }
        }

        public static void SellCarnageTokens(Player player, long amount)
        {
            long totalCost = amount * CTValue;

            if (player.BankedCarnageTokens >= amount)
            {
                player.BankedCarnageTokens -= amount;
                player.BankedPyreals = player.BankedPyreals + totalCost;
                CTInCirculation -= amount;

                CalculateCTValue(CTInCirculation);
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have sold {amount} Carnage Tokens at a cost of {totalCost} Pyreals.", ChatMessageType.System));
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"New Carnage Token Value {CTValue}.", ChatMessageType.System));
            }
            else
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough Carnage Tokens to sell {amount} Carnage Tokens. Cost: {totalCost}Ct", ChatMessageType.System));
            }
        }

        public static void AddCTToCirculation(long amount)
        {
            CTInCirculation += amount;
            CalculateCTValue(CTInCirculation);
        }

        public static void RemoveCTFromCirculation(long amount)
        {
            CTInCirculation -= amount;
            CalculateCTValue(CTInCirculation);
        }

        public static void AddACToCirculation(long amount)
        {
            ACInCirculation += amount;
            CalculateACValue(ACInCirculation);
        }

        public static void RemoveACFromCirculation(long amount)
        {
            ACInCirculation -= amount;
            CalculateACValue(ACInCirculation);
        }

        public static void SaveCurrencyValues()
        {
            string json = "{\n";
            json += "\t\"PyrealValue\": " + PyrealValue + ",\n";
            json += "\t\"MMDValue\": " + MMDValue + ",\n";
            json += "\t\"CTValue\": " + CTValue + ",\n";
            json += "\t\"ACValue\": " + ACValue + ",\n";
            json += "\t\"CTInCirculation\": " + CTInCirculation + ",\n";
            json += "\t\"ACInCirculation\": " + ACInCirculation + "\n";
            json += "}";

            System.IO.File.WriteAllText("MarketData.json", json);
        }

        public static string MarketDataGenerator()
        {
            QueryACCirculation();
            QueryCTCirculation();
            CalculateACValue(ACInCirculation);
            CalculateCTValue(CTInCirculation);

            string marketData = "\n";
            marketData = "Current Market Data:\n";
            marketData += "Pyreal Value: " + PyrealValue + "\n";
            marketData += "MMD Value: " + MMDValue + "\n";
            marketData += "Carnage Token Value: " + CTValue + "\n";
            marketData += "AshCoin Value: " + ACValue + "\n";
            marketData += "Carnage Tokens in Circulation: " + CTInCirculation + "\n";
            marketData += "AshCoins in Circulation: " + ACInCirculation + "\n";

            return marketData;
        }
    }
}
