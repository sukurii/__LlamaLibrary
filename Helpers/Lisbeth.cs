﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Media;
using Buddy.Coroutines;
using Clio.Utilities;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Managers;
using ff14bot.Objects;
using ff14bot.Settings;
using LlamaLibrary.Logging;
#pragma warning disable CS8603
#pragma warning disable CS8602

namespace LlamaLibrary.Helpers
{
    [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1214:Readonly fields should appear before non-readonly fields")]
    public static class Lisbeth
    {
        private static readonly LLogger Log = new("LisbethHelper", Colors.MediumPurple);

        private static object? _lisbeth;
        private static MethodInfo? _orderMethod;
        private static readonly MethodInfo _travelMethod;
        public static Func<string> _getCurrentAreaName;
        private static readonly Func<Task> _stopGentlyAndWait;
        private static Func<Task> _equipOptimalGear;
        private static Func<Task> _extractMateria;
        private static Func<Task> _selfRepair;
        private static Func<Task> _selfRepairWithMenderFallback;
        private static Func<Task> _stopGently;
        private static Action<string, Func<Task>> _addHook;
        private static Action<string, Func<Task>> _addCraftCycleHook;
        private static Action<string> _removeHook;
        private static Action<string> _removeCraftCycleHook;
        private static Func<List<string>> _getHookList;
        private static Func<Task<bool>> _exitCrafting;
        private static Func<Task<bool>> _isProductKeyValid;
        private static Func<string, Vector3, Task<bool>> _travelToWithArea;
        private static Func<uint, uint, Vector3, Task<bool>> _travelTo;
        private static Func<uint, Vector3, Task<bool>> _travelToWithoutSubzone;
        private static Action _openWindow;
        private static Func<string, Task<string>> _getOrderExpansionAsJson;
        private static Func<Character, Task> _kill;

        static Lisbeth()
        {
            FindLisbeth();
        }

        internal static void FindLisbeth()
        {
            var loader = BotManager.Bots
                .FirstOrDefault(c => c.Name == "Lisbeth");

            if (loader == null)
            {
                return;
            }

            var lisbethObjectProperty = loader.GetType().GetProperty("Lisbeth");
            var lisbeth = lisbethObjectProperty?.GetValue(loader);
            var orderMethod = lisbeth?.GetType().GetMethod("ExecuteOrders");
            if (lisbeth != null)
            {
                var apiObject = lisbeth.GetType().GetProperty("Api")?.GetValue(lisbeth);
                if (orderMethod == null)
                {
                    return;
                }

                if (apiObject != null)
                {
                    var m = apiObject.GetType().GetMethod("GetCurrentAreaName");
                    if (m != null)
                    {
                        try
                        {
                            _getCurrentAreaName = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), apiObject, "GetCurrentAreaName");
                            _stopGently = (Func<Task>)Delegate.CreateDelegate(typeof(Func<Task>), apiObject, "StopGently");
                            _kill = (Func<Character, Task>)Delegate.CreateDelegate(typeof(Func<Character, Task>), apiObject, "Kill");

                            //_stopGentlyAndWait = (Func<Task>) Delegate.CreateDelegate(typeof(Func<Task>), apiObject, "StopGentlyAndWait");
                            _addHook = (Action<string, Func<Task>>)Delegate.CreateDelegate(typeof(Action<string, Func<Task>>), apiObject, "AddHook");
                            _removeHook = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), apiObject, "RemoveHook");
                            _addCraftCycleHook = (Action<string, Func<Task>>)Delegate.CreateDelegate(typeof(Action<string, Func<Task>>), apiObject, "AddCraftCycleHook");
                            _removeCraftCycleHook = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), apiObject, "RemoveCraftCycleHook");
                            _getHookList = (Func<List<string>>)Delegate.CreateDelegate(typeof(Func<List<string>>), apiObject, "GetHookList");
                            _exitCrafting = (Func<Task<bool>>)Delegate.CreateDelegate(typeof(Func<Task<bool>>), apiObject, "ExitCrafting");
                            _equipOptimalGear = (Func<Task>)Delegate.CreateDelegate(typeof(Func<Task>), apiObject, "EquipOptimalGear");
                            _extractMateria = (Func<Task>)Delegate.CreateDelegate(typeof(Func<Task>), apiObject, "ExtractMateria");
                            _selfRepair = (Func<Task>)Delegate.CreateDelegate(typeof(Func<Task>), apiObject, "SelfRepair");
                            _selfRepairWithMenderFallback = (Func<Task>)Delegate.CreateDelegate(typeof(Func<Task>), apiObject, "SelfRepairWithMenderFallback");
                            _travelTo = (Func<uint, uint, Vector3, Task<bool>>)Delegate.CreateDelegate(typeof(Func<uint, uint, Vector3, Task<bool>>), apiObject, "TravelTo");
                            _travelToWithArea = (Func<string, Vector3, Task<bool>>)Delegate.CreateDelegate(typeof(Func<string, Vector3, Task<bool>>), apiObject, "TravelToWithArea");
                            _travelToWithoutSubzone = (Func<uint, Vector3, Task<bool>>)Delegate.CreateDelegate(typeof(Func<uint, Vector3, Task<bool>>), apiObject, "TravelToWithoutSubzone");
                            _openWindow = (System.Action)Delegate.CreateDelegate(typeof(System.Action), apiObject, "OpenWindow");
                            _getOrderExpansionAsJson = (Func<string, Task<string>>)Delegate.CreateDelegate(typeof(Func<string, Task<string>>), apiObject, "GetOrderExpansionAsJson");
                            _isProductKeyValid = (Func<Task<bool>>)Delegate.CreateDelegate(typeof(Func<Task<bool>>), apiObject, "IsProductKeyValid");
                        }
                        catch (Exception e)
                        {
                            Log.Error(e.ToString());
                        }
                    }
                }
            }

            _orderMethod = orderMethod;
            _lisbeth = lisbeth;

            //_travelMethod = travelMethod;

            Log.Information("Lisbeth found.");
        }

        public static async Task<bool> HasLisbeth()
        {
            try
            {
                if (BotManager.Bots.FirstOrDefault(c => c.EnglishName == "Lisbeth") == default)
                {
                    return false;
                }

                //return await Lisbeth.GetOrderExpansionAsJson("[{\"Group\":1,\"Item\":1,\"Amount\":1,\"Enabled\":true,\"Type\":\"None\"}]") != null;
                return await Lisbeth.IsProductKeyValid();
            }
            catch
            {
                return false;
            }
        }

        public static string GetCurrentAreaName => _getCurrentAreaName.Invoke();

        public static async Task Kill(Character mob)
        {
            if (Core.Me.Distance(mob) + 1 >= RoutineManager.Current.PullRange)
            {
                Log.Information($"Outside of pull range so getting closer");
                await Navigation.FlightorMove(mob.Location, () => Core.Me.Distance(mob) < (RoutineManager.Current.PullRange / 2));
                if (Core.Me.IsMounted)
                {
                    await CommonTasks.StopAndDismount();
                }
            }

            var mount = CharacterSettings.Instance.UseMount;
            CharacterSettings.Instance.UseMount = false;
            await _kill(mob);
            CharacterSettings.Instance.UseMount = mount;
        }

        public static async Task<bool> ExecuteOrders(string json)
        {
            if (_orderMethod != null)
            {
                return await (Task<bool>)_orderMethod.Invoke(_lisbeth, new object[] { json, false });
            }

            FindLisbeth();
            if (_orderMethod == null)
            {
                return false;
            }

            return await (Task<bool>)_orderMethod.Invoke(_lisbeth, new object[] { json, false });
        }

        public static async Task<bool> ExecuteOrdersIgnoreHome(string json)
        {
            if (_orderMethod != null)
            {
                return await (Task<bool>)_orderMethod.Invoke(_lisbeth, new object[] { json, true });
            }

            FindLisbeth();
            if (_orderMethod == null)
            {
                return false;
            }

            return await (Task<bool>)_orderMethod.Invoke(_lisbeth, new object[] { json, true });
        }

        public static async Task<bool> TravelTo(string area, Vector3 position)
        {
            if (_travelToWithArea != null)
            {
                return await _travelToWithArea(area, position);
            }

            FindLisbeth();
            if (_travelToWithArea == null)
            {
                return false;
            }

            return await _travelToWithArea(area, position);
        }

        public static async Task<bool> TravelToZones(uint zoneId, uint subzoneId, Vector3 position)
        {
            return await _travelTo(zoneId, subzoneId, position);
        }

        public static async Task<bool> TravelToZones(uint zoneId, Vector3 position)
        {
            Log.Information($"Lisbeth Travel: Zone:{zoneId}  Pos:{position}");
            return await _travelToWithoutSubzone(zoneId, position);
        }

        public static async Task StopGently()
        {
            await _stopGently();
        }

        public static void OpenSettings()
        {
            _openWindow();
        }

        public static void AddHook(string name, Func<Task> function)
        {
            _addHook?.Invoke(name, function);
        }

        public static void RemoveHook(string name)
        {
            _removeHook?.Invoke(name);
        }

        public static void AddCraftCycleHook(string name, Func<Task> function)
        {
            _addCraftCycleHook?.Invoke(name, function);
        }

        public static void RemoveCraftCycleHook(string name)
        {
            _removeCraftCycleHook?.Invoke(name);
        }

        public static List<string> GetHookList()
        {
            return _getHookList?.Invoke() ?? new List<string>();
        }

        public static Task<bool> ExitCrafting()
        {
            return _exitCrafting?.Invoke();
        }

        public static async Task EquipOptimalGear()
        {
            await _equipOptimalGear?.Invoke();
        }

        public static async Task ExtractMateria()
        {
            await _extractMateria?.Invoke();
        }

        public static async Task SelfRepair()
        {
            await _selfRepair?.Invoke();
        }

        public static async Task SelfRepairWithMenderFallback()
        {
            await _selfRepairWithMenderFallback?.Invoke();
        }

        public static async Task<string> GetOrderExpansionAsJson(string orderJson)
        {
            return await _getOrderExpansionAsJson(orderJson);
        }

        public static async Task<bool> IsProductKeyValid()
        {
            return await Coroutine.ExternalTask(_isProductKeyValid());
        }
    }
}