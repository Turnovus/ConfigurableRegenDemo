﻿using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;


namespace LimitedRegen
{
    /*
     * Class LimitedRegenUtility is a static class used to filter out specific
     * Hediffs from a Pawn's health, and to cure those Hediffs while applying
     * optional side-effects.
     */
    /// <summary>
    /// Class <c>LimitedRegenUtility</c> is a static class used to filter out
    /// specific <c>Hediff</c>s from a <c>Pawn</c>'s health, and to cure those 
    /// <c>Hediff</c>s while applying optional side-effects.
    /// </summary>
    public static class ConfigurableRegenUtility
    {
        /// <summary>
        /// Tries to cure chronic <c>Hediff</c>s, and applies side-effects
        /// randomly if defined.
        /// </summary>
        /// <param name="pawn">
        /// The <c>Pawn</c> that will have hediffs added/removed
        /// </param>
        /// <param name="cause">
        /// The <c>Hediff</c> that is causing regeneration, used to notify the
        /// player, and to ensure that <c>Hediffs</c> cannot cure themselves.
        /// </param>
        /// <param name="blacklist">
        /// List of chronic <c>HediffDef</c>s to ignore. Leave null to ignore
        /// none.
        /// </param>
        /// <param name="whitelist">
        /// <c>List</c> containing the only chronic <c>HediffDefs</c>s that
        /// will be healed. Leave null to accept all chronic <c>HediffDef</c>s.
        /// </param>
        /// <param name="sideEffects">
        /// A <c>List</c> of <c>RegenSideEffect</c>s to randomly apply after
        /// successful regeneration.
        /// </param>
        /// <param name="canHealDestroyed">
        /// If <c>false</c>, injuries that are destroyed parts will be ignored.
        /// </param>
        /// <param name="injuryMode">
        /// Dictates whether <c>Hediff</c>s that use the <c>Hediff_Injury</c>
        /// class should automatically be treated as whitelisted, blacklisted,
        /// or neither.
        /// </param>
        public static void TryHealRandomPermanentWoundFor(
            Pawn pawn,
            HediffWithComps cause,
            List<HediffDef> blacklist = null,
            List<HediffDef> whitelist = null,
            List<RegenSideEffect> sideEffects = null,
            bool canHealDestroyed = false,
            InjuryRegenListMode injuryMode = InjuryRegenListMode.None)
        {
            Random random = new Random(); //RNG utility
            Hediff result; //The hediff to be healed
            //List of hediffs to be added to the regenerated part. Hediffs need
            //to be added at the end, as hediffs can't be added to destroyed
            //parts.
            List<Hediff> addedHediffs = new List<Hediff>();
            
            //Try to find a valid hediff that meets all of the parameters, stop
            //execution if none found. The following code block is mostly one
            //search function and its parameters.
            IEnumerable<Hediff> curableHediffs = GetCurablePermanentHediffs(
                pawn, blacklist, whitelist, canHealDestroyed, injuryMode)
                //Hediffs should never be able to cure themselves.
                .Where(hd => hd != cause);
            //If curableHediffs contains empty enumerations, accessing it will
            //throw a null reference exception, so we need to catch that.
            try
            {

                if (curableHediffs == null ||
                    !curableHediffs.TryRandomElement(out result)
                )
                    //If the pawn has no curable Hediffs, stop here.
                    return;
            }
            catch (NullReferenceException)
            {
                return;
            }
            //Throw unexpected exceptions because those are actually a problem.
            catch (Exception e)
            {
                throw e;
            }
            //Iterate through each side-effect, then try to apply it to the 
            //cured hediff's body part.
            if (sideEffects != null)
            {
                foreach (RegenSideEffect sideEffect in sideEffects)
                {
                    //If the random roll says the hediff should be applied...
                    if (sideEffect.percentChance >= 1.0f ||
                        sideEffect.percentChance >= (float)random.NextDouble())
                    {
                        //Make the side-effect hediff
                        Hediff newHediff = HediffMaker.MakeHediff(
                            sideEffect.hediffDef,
                            pawn,
                            //Only attach hediff to the cured part if it is not
                            //a global hediff.
                            sideEffect.isGlobalHediff ? null : result.Part);
                        //Set the new hediff's severity to something between 
                        //the minimum and maximum specified severity.
                        float newHediffSeverity = ((float)random.NextDouble() *
                            (sideEffect.severity.max -
                                sideEffect.severity.min)) +
                            sideEffect.severity.min;
                        //If the new hediff's severity is meant to be 
                        //multipliedby the old hediff's severity, do so now. If
                        //the cured part was destroyed, the multiplier is 
                        //ignored, so the full effect is always applied.
                        if (sideEffect.useInjurySeverityMult &&
                            !(result is Hediff_MissingPart))
                        {
                            if (HediffIsInjury(result))
                                newHediffSeverity *=
                                    result.Severity /
                                    result.Part.def.hitPoints;
                            else
                                newHediffSeverity *= result.Severity;
                        }

                        newHediff.Severity = newHediffSeverity;
                        addedHediffs.Add(newHediff);
                    }
                }
            }
            //Cure the original target hediff.
            if (!(result is Hediff_MissingPart))
                //If it's a normal Hediff, do it the normal way.
                TryCureHediffInt(result);
            else
                //if it's a missing part, use the method for missing parts.
                RestorePartInt(pawn, result.Part);
            //Apply the side-effect hediffs to the newly cured part.
            foreach (Hediff addedHediff in addedHediffs)
                pawn.health.AddHediff(addedHediff);

            //Notify the player that an injury has been healed.
            if (!PawnUtility.ShouldSendNotificationAbout(pawn))
                return;
            Messages.Message(
                "MessagePermanentWoundHealed".Translate(
                    cause.LabelCap, pawn.LabelShort, result.LabelCap,
                    pawn.Named("PAWN")
                ),
                pawn,
                MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>
        /// Returns a list of all <c>Hediff</c>s applied to a <c>Pawn</c> that
        /// meet a specified list of criteria.
        /// </summary>
        /// <param name="pawn">
        /// The <c>Pawn</c> whose <c>Hediff</c>s are being checked
        /// </param>
        /// <param name="blacklist"></param>
        /// List of chronic <c>HediffDef</c>s to ignore. Leave null to ignore
        /// none.
        /// <param name="whitelist">
        /// List containing the only chronic <c>HediffDefs</c>s that will be
        /// healed. Leave null to accept all <c>HediffDef</c>s.
        /// </param>
        /// <param name="canHealDestroyed">
        /// If <c>false</c>, injuries that are destroyed parts will be ignored.
        /// </param>
        /// <param name="injuryMode">
        /// Dictates whether <c>Hediff</c>s that use the <c>Hediff_Injury</c>
        /// class should automatically be treated as whitelisted, blacklisted,
        /// or neither.
        /// </param>
        /// <returns>
        /// An <c>IEnumerable</c> containing all of the <c>Hediff</c>s that
        /// meet the specified criteria.
        /// </returns>
        public static IEnumerable<Hediff> GetCurableHediffs(
            Pawn pawn,
            List<HediffDef> blacklist = null,
            List<HediffDef> whitelist = null,
            bool canHealDestroyed = false,
            InjuryRegenListMode injuryMode = InjuryRegenListMode.None)
        {
            IEnumerable<Hediff> result;

            result = pawn.health.hediffSet.hediffs
                //Whitelist and blacklist should allow condition.
                .Where(hd =>
                    IsHediffAllowed(hd, whitelist, blacklist, injuryMode)
                )
                //Injury shouldn't be a destroyed part, unless specified
                //otherwise.
                .Where(hd =>
                    //Always accept injury if it is not a destroyed part, or...
                    !(hd is Hediff_MissingPart) ||
                    //Always accept injury if canHealDestroyed is true.
                    canHealDestroyed
                )
                //Missing parts must have a non-destroyed parent, so 
                //that we don't have problems like armless hands.
                .Where(hd =>
                    !(hd is Hediff_MissingPart) ||
                    !pawn.health.hediffSet.PartIsMissing(hd.Part.parent)
                );
                
            return result;
        }

        /// <summary>
        /// Returns a list of permanent <c>Hediff</c>s applied to a <c>Pawn</c>
        /// that meet a specified list of criteria.
        /// </summary>
        /// <param name="pawn">
        /// The <c>Pawn</c> whose <c>Hediff</c>s are being checked
        /// </param>
        /// <param name="blacklist"></param>
        /// List of chronic <c>HediffDef</c>s to ignore. Leave null to ignore
        /// none.
        /// <param name="whitelist">
        /// List containing the only chronic <c>HediffDefs</c>s that will be
        /// healed. Leave null to accept all <c>HediffDef</c>s.
        /// </param>
        /// <param name="canHealDestroyed">
        /// If <c>false</c>, injuries that are destroyed parts will be ignored.
        /// </param>
        /// <param name="injuryMode">
        /// Dictates whether <c>Hediff</c>s that use the <c>Hediff_Injury</c>
        /// class should automatically be treated as whitelisted, blacklisted,
        /// or neither.
        /// </param>
        /// <returns>
        /// An <c>IEnumerable</c> containing all of the Permanent
        /// <c>Hediff</c>s that meet the specified criteria.
        /// </returns>
        public static IEnumerable<Hediff> GetCurablePermanentHediffs(
            Pawn pawn,
            List<HediffDef> blacklist = null,
            List<HediffDef> whitelist = null,
            bool canHealDestroyed = false,
            InjuryRegenListMode injuryMode = InjuryRegenListMode.None)
        {
            //Reuse the method for finding non-permanent hediffs
            IEnumerable<Hediff> curableHediffs =
                GetCurableHediffs(
                    pawn, blacklist, whitelist, canHealDestroyed, injuryMode
                );

            //Hediff must be chronic, permanent, or destroyed.
            return curableHediffs.Where(hd =>
                (hd.IsPermanent() || hd.def.chronic ||
                hd is Hediff_MissingPart)
            );
        }

        /// <summary>
        /// Used to check if a <c>Hediff</c> is inherited from
        /// <c>Hediff_Injury</c> or from <c>Hediff_MissingPart</c>.
        /// </summary>
        /// <param name="hediff">
        /// The <c>Hediff</c> to check.
        /// </param>
        /// <returns>
        /// <c>true</c>, if the specified <c>Hediff</c> is some form of injury
        /// or missing part.
        /// </returns>
        public static bool HediffIsInjury(Hediff hediff) =>
            hediff is Hediff_Injury || hediff is Hediff_MissingPart;

        /// <summary>
        /// Recursively restores each part of a given limb.
        /// </summary>
        /// <param name="pawn">
        /// The <c>Pawn</c> who owns the part.
        /// </param>
        /// <param name="part">
        /// The <c>BodyPartRecord</c> of the part to restore.
        /// </param>
        /// <param name="alreadyWarned">
        /// If <c>true</c>, the player will not be warned about possible errors
        /// caused by unsafely restoring parts during <c>HealthTick</c>.
        /// </param>
        private static void RestorePartRecursiveInt(
            Pawn pawn,
            BodyPartRecord part,
            ref bool alreadyWarned)
        {
            //hediff list must be instantiated, because the original list will
            //be modified by this process
            List<Hediff> hediffs =
                new List<Hediff>(pawn.health.hediffSet.hediffs);
            foreach (HediffWithComps hd in hediffs)
            {
                if (hd.Part == part && !hd.def.keepOnBodyPartRestoration)
                {
                    //Mark Hediff for removable if possible
                    alreadyWarned = TryCureHediffInt(hd, alreadyWarned);
                }
            }
            foreach (BodyPartRecord child in part.parts)
            {
                RestorePartRecursiveInt(pawn, child, ref alreadyWarned);
            }
        }

        /// <summary>
        /// Recursively restores each part of a given limb.
        /// </summary>
        /// <param name="pawn">
        /// The <c>Pawn</c> who owns the part.
        /// </param>
        /// <param name="part">
        /// The <c>BodyPartRecord</c> of the part to restore.
        /// </param>
        private static void RestorePartRecursiveInt(
            Pawn pawn,
            BodyPartRecord part)
        {
            bool warn = false;
            RestorePartRecursiveInt(pawn, part, ref warn);
        }

        /// <summary>
        /// Used to initiate a recursive restoration of a specified part. When
        /// done, it marks the <c>Pawn</c>'s health records to be re-cached.
        /// </summary>
        /// <param name="pawn">
        /// The <c>Pawn</c> who owns the part.
        /// </param>
        /// <param name="part">
        /// The <c>BodyPartRecord</c> of the part to restore.
        /// </param>
        private static void RestorePartInt(
            Pawn pawn,
            BodyPartRecord part
            )
        {
            if (part == null)
            {
                Log.Error(
                    "ConfigurableRegenUtility: Tried to restore null part");
                return;
            }
            RestorePartRecursiveInt(pawn, part);
            pawn.health.hediffSet.DirtyCache();
        }

        /// <summary>
        /// Used to safely cure <c>Hediff</c>s during <c>HealthTick</c> using
        /// <c>HediffComp_ShouldRemove</c>, so as not to cause index array
        /// exceptions due to a shrinking <c>Hediff</c> list. If the
        /// <c>Hediff</c> does not have <c>HediffComp</c>s, it won't be
        /// possible to cure it the safe way, so a warning will be provided to
        /// the player.
        /// </summary>
        /// <param name="hediff">
        /// The <c>Hediff</c> to be cured.
        /// </param>
        /// <param name="alreadyWarned">
        /// If <c>true</c>, the player will not be warned about possible errors
        /// caused by unsafely restoring parts during <c>HealthTick</c>.
        /// </param>
        /// <returns>
        /// <c>true</c> if a warning was issued to the player, <c>false</c>
        /// otherwise.
        /// </returns>
        private static bool TryCureHediffInt(
            Hediff hediff,
            bool alreadyWarned)
        {
            //If the Hediff uses comps, mark it for safe removal.
            if (hediff is HediffWithComps withComps)
            {
                withComps.comps.Add(new HediffComp_ShouldRemove());
            }
            //If the Hediff does not use comps, we'll have to do it the dirty
            //way. We'll also inform the player, so as not to cause alarm.
            else
            {
                if (!alreadyWarned)
                    Log.Warning("ConfigurableRegenUtility: " +
                        "Attempting to cure Hediff during HealthTick. " +
                        "This may cause a harmless error.");
                HealthUtility.Cure(hediff);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Used to safely cure <c>Hediff</c>s during <c>HealthTick</c> using
        /// <c>HediffComp_ShouldRemove</c>, so as not to cause index array
        /// exceptions due to a shrinking <c>Hediff</c> list. If the
        /// <c>Hediff</c> does not have <c>HediffComp</c>s, it won't be
        /// possible to cure it the safe way, so a warning will be provided to
        /// the player.
        /// </summary>
        /// <param name="hediff">
        /// The <c>Hediff</c> to be cured.
        /// </param>
        private static void TryCureHediffInt(Hediff hediff)
        {
            TryCureHediffInt(hediff, false);
        }

        /// <summary>
        /// Method for determining if a given <c>Hediff</c> matches a given
        /// whitelist, blacklist, or both.
        /// </summary>
        /// <remarks>
        /// Bear in mind that, while it is
        /// possible to use both types of lists, doing so will lead to
        /// unexpected behavior. If an automatic injury list is used, however,
        /// you can override it by adding specific injuries to the opposite
        /// list.
        /// </remarks>
        /// <param name="hediff">
        /// The <c>Hediff</c> to check against the lists.
        /// </param>
        /// <param name="whitelist">
        /// A <c>List</c> of <c>Hediff</c>s that will only be accepted.
        /// </param>
        /// <param name="blacklist">
        /// A <c>List</c> of <c>Hediff</c>s that will never be accepted.
        /// </param>
        /// <param name="mode">
        /// The automatic injury listing mode, if any.
        /// </param>
        /// <returns>
        /// <c>true</c> if the <c>Hediff</c> fits into the specified lists,
        /// <c>false</c> otherwise.
        /// </returns>
        public static bool IsHediffAllowed(
            Hediff hediff,
            List<HediffDef> whitelist = null,
            List<HediffDef> blacklist = null,
            InjuryRegenListMode mode = InjuryRegenListMode.None)
        {
            //Never accept Hediffs that are explicitly blacklisted, even if
            //it's an injury and auto-whitelist is on.
            if (blacklist != null && blacklist.Contains(hediff.def))
                return false;
            //If any whitelist is being used...
            if (whitelist != null || mode == InjuryRegenListMode.Whitelist)
            {
                //Always accept Hediffs that are explicitly whitelisted.
                //Checking the manual whitelist before the auto-blacklist means
                //we can exclude specific injuries from the auto-blacklist.
                if (whitelist != null && whitelist.Contains(hediff.def))
                    return true;
                else
                {
                    //Account for auto-whitelisted injuries.
                    if (mode == InjuryRegenListMode.Whitelist &&
                        HediffIsInjury(hediff))
                        return true;
                }
                //If any whitelist is in use, but neither whitelist accepts the
                //injury, then the injury is not acceptable.
                return false;
            }
            //Don't allow any injuries that have made it this far if injuries
            //are auto-blacklisted.
            if (mode == InjuryRegenListMode.Blacklist &&
                HediffIsInjury(hediff))
                return false;
            return true;
        }
    }

    
}
