using Discord;
using HarmonyLib;
using NebulaModel.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NebulaPatcher.Patches.Dynamic
{
    [HarmonyPatch(typeof(DSPGame))]
    class DSPGame_Patch
    {

        public static Discord.Discord discord;

        [HarmonyPostfix]
        [HarmonyPatch("Start")]
        public static void Start_Postfix()
        {

            discord = new Discord.Discord(871825456586960906, (ulong)Discord.CreateFlags.Default);
            var activityManager = discord.GetActivityManager();
            var activity = new Discord.Activity
            {
                Details = "In the menu",
                State = "Playing alone"
            };
            activityManager.UpdateActivity(activity, (res) =>
            {
                if (res == Discord.Result.Ok)
                {
                    Debug.Log("Discord status set!");
                }
                else
                {
                    Debug.LogError("Discord status failed!");
                }
            });
        }

        [HarmonyPostfix]
        [HarmonyPatch("Update")]
        public static void Update_Postfix()
        {
            discord.RunCallbacks();
        }

    }
}
