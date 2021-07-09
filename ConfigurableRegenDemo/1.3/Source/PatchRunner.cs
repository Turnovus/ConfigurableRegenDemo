using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using HarmonyLib;

namespace LimitedRegen
{
    /// <summary>
    /// Responsible for running all of the Harmony patches on game start.
    /// </summary>
    [StaticConstructorOnStartup]
    static class PatchRunner
    {
        /// <summary>
        /// Constructor to run on startup, calls the method for patching.
        /// </summary>
        static PatchRunner()
        {
            DoPatchingInt("com.turnovus.configurableregen.harmonypatches");
        }

        /// <summary>
        /// Applies all Harmony patches defined in other classes.
        /// </summary>
        /// <param name="id">
        /// Package ID to help other modders identify our patches
        /// </param>
        private static void DoPatchingInt(string id)
        {
            var harmony = new Harmony(id);
            harmony.PatchAll();
        }
    }
}
