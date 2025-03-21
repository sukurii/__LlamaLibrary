﻿using System;
using System.Threading.Tasks;
using System.Windows.Media;
using Buddy.Coroutines;
using Clio.Utilities;
using ff14bot.Behavior;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.Objects;
using ff14bot.RemoteWindows;
using LlamaLibrary.Enums;
using LlamaLibrary.Helpers.Housing;
using LlamaLibrary.Helpers.Ping;
using LlamaLibrary.Logging;
using LlamaLibrary.RemoteAgents;
using LlamaLibrary.RemoteWindows;

namespace LlamaLibrary.Helpers.WorldTravel
{
    public static class WorldTravel
    {
        private static readonly LLogger Log = new(nameof(WorldTravel), Colors.Chocolate);

        private const TravelCity DefaultStart = TravelCity.Limsa;

        public static async Task OpenWorldTravelMenu(TravelCity travelCity = DefaultStart)
        {
            GameObject? AE;

            travelCity = (TravelCity)WorldManager.ZoneId switch
            {
                TravelCity.Limsa    => TravelCity.Limsa,
                TravelCity.Uldah    => TravelCity.Uldah,
                TravelCity.Gridania => TravelCity.Gridania,
                _                   => travelCity
            };

            /*
            switch (travelCity)
            {
                case TravelCity.Limsa:
                    await Navigation.GetTo(129, new Vector3(-89.30112f, 18.80033f, -2.019181f));
                    break;
                case TravelCity.Uldah:
                    await Navigation.GetTo(130, new Vector3(-147.672f, -3.154888f, -167.1899f));
                    break;
                case TravelCity.Gridania:
                    await Navigation.GetTo(132, new Vector3(31.95396f, 2.200001f, 33.20696f));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(travelCity), travelCity, null);
            }
            */

            AE = await GetAE(travelCity);
            if (AE == default)
            {
                switch (travelCity)
                {
                    case TravelCity.Limsa:
                        await Navigation.GetTo(129, new Vector3(-89.30112f, 18.80033f, -2.019181f));
                        break;
                    case TravelCity.Uldah:
                        await Navigation.GetTo(130, new Vector3(-147.672f, -3.154888f, -167.1899f));
                        break;
                    case TravelCity.Gridania:
                        await Navigation.GetTo(132, new Vector3(31.95396f, 2.200001f, 33.20696f));
                        break;
                    case TravelCity.Cheapest:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(travelCity), travelCity, null);
                }

                AE = await GetAE(travelCity);
            }

            if (AE == default)
            {
                return;
            }

            if (!AE.IsWithinInteractRange)
            {
                await Navigation.OffMeshMoveInteract(AE);
            }

            AE.Interact();

            if (!await Coroutine.Wait(5000, () => Conversation.IsOpen))
            {
                Log.Information($"No Conversation");
                AE.Interact();
                if (await Coroutine.Wait(5000, () => Conversation.IsOpen))
                {
                    return;
                }
            }

            SelectWorldVisit();

            await Coroutine.Wait(5000, () => WorldTravelSelect.Instance.IsOpen);
            await Coroutine.Sleep(1000);
        }

        public static async Task<bool> GetTo(WorldLocation worldLocation, TravelCity travelCity = TravelCity.Uldah)
        {
            if (WorldHelper.CurrentWorldId != (int)worldLocation.World && !await GoToWorld(worldLocation.World, travelCity))
            {
                return false;
            }

            if (WorldHelper.CurrentWorldId == (int)worldLocation.World && Navigator.AtLocation(worldLocation.Location.Coordinates))
            {
                return true;
            }

            return await Navigation.GetTo(worldLocation.Location);
        }

        public static async Task<bool> GoToWorld(World world, TravelCity travelCity = DefaultStart)
        {
            return await GoToWorld((ushort)world, travelCity);
        }

        public static async Task<bool> GoToWorld(ushort worldId, TravelCity travelCity = DefaultStart)
        {
            if (!WorldHelper.CheckDC((World)worldId))
            {
                return false;
            }

            if (WorldHelper.CurrentWorldId == worldId)
            {
                return true;
            }

            if (PartyManager.IsInParty && !PartyManager.CrossRealm)
            {
                Log.Information("Getting out of party");
                ChatManager.SendChat("/pcmd leave");
                if (!await Coroutine.Wait(5000, () => SelectYesno.IsOpen))
                {
                    Log.Error("Could not leave party...RIP");
                    return false;
                }

                SelectYesno.Yes();

                if (!await Coroutine.Wait(5000, () => PartyManager.IsInParty && !PartyManager.CrossRealm))
                {
                    Log.Error("Could not leave party...RIP");
                    return false;
                }
            }

            if (travelCity == TravelCity.Cheapest)
            {
            }

            await OpenWorldTravelMenu(travelCity);

            if (WorldTravelSelect.Instance.IsOpen)
            {
                var Choices = AgentWorldTravelSelect.Instance.Choices;

                if (AgentWorldTravelSelect.Instance.CurrentWorld != worldId)
                {
                    for (var i = 0; i < Choices.Length; i++)
                    {
                        if (Choices[i].WorldID != worldId)
                        {
                            continue;
                        }

                        Log.Information($"Going to: {((World)Choices[i].WorldID).WorldName()}");
                        WorldTravelSelect.Instance.SelectWorld(i);
                        await Coroutine.Wait(5000, () => SelectYesno.IsOpen);
                        if (SelectYesno.IsOpen)
                        {
                            SelectYesno.Yes();
                            await Coroutine.Wait(1_200_000, () => WorldTravelFinderReady.Instance.IsOpen);
                            if (WorldTravelFinderReady.Instance.IsOpen)
                            {
                                await Coroutine.Wait(-1, () => !WorldTravelFinderReady.Instance.IsOpen);
                                await Coroutine.Sleep(2000);
                                if (CommonBehaviors.IsLoading)
                                {
                                    await Coroutine.Wait(-1, () => !CommonBehaviors.IsLoading);
                                }

                                await Coroutine.Sleep(1000);
                                await PingChecker.UpdatePing();
                                Log.Information($"CurrentWorld: {WorldHelper.CurrentWorld.WorldName()}");
                            }
                        }

                        break;
                    }
                }

                if (WorldTravelSelect.Instance.IsOpen)
                {
                    WorldTravelSelect.Instance.Close();
                    await Coroutine.Sleep(500);
                }
            }

            if (WorldHelper.IsOnHomeWorld)
            {
                HousingHelper.UpdateResidenceArray();
            }

            await Coroutine.Sleep(500);
            return WorldHelper.CurrentWorldId == worldId;
        }

        public static async Task<bool> MakeSureHome(TravelCity travelCity = DefaultStart)
        {
            if (WorldHelper.CurrentWorldId == WorldHelper.HomeWorldId)
            {
                return true;
            }

            return await GoToWorld(WorldHelper.HomeWorldId, travelCity);
        }

        public static async Task<GameObject?> GetAE(TravelCity travelCity = DefaultStart)
        {
            uint id = 0;
            id = travelCity switch
            {
                TravelCity.Limsa    => 8,
                TravelCity.Uldah    => 9,
                TravelCity.Gridania => 2,
                _                   => throw new ArgumentOutOfRangeException(nameof(travelCity), travelCity, null),
            };
            return await Navigation.GetToAE(id);
        }

        public static bool SelectWorldVisit()
        {
            var test = 0;
            foreach (var line in Conversation.GetConversationList)
            {
                if (line.Contains(Translator.VisitAnotherWorldServer))
                {
                    break;
                }

                test++;
            }

            if (test != Conversation.GetConversationList.Count)
            {
                Conversation.SelectLine((uint)test);
                return true;
            }

            return false;
        }
    }
}