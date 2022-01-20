using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace LadiesCanFlirtToo
{
    [StaticConstructorOnStartup]
    public static class Main
    {
        static Main()
        {
            Harmony harmony = new Harmony("ladies.can.flirt.too");
            harmony.PatchAll();
        }
    }

    // This removes gendered romance chances.
    [HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), "RandomSelectionWeight")]
    public static class InteractionWorker_RomanceAttempt__RandomSelectionWeight
    {
        private static MethodInfo _method_calculate = AccessTools.Method(typeof(InteractionWorker_RomanceAttempt__RandomSelectionWeight), "CalculateAttractionFactor");

        public static float CalculateAttractionFactor(Pawn initiator, Pawn receipient)
        {
            bool initiator_into_it;
            bool receipient_into_it;
        
            if (initiator.gender == receipient.gender)
            {
                initiator_into_it = initiator.story.traits.HasTrait(TraitDefOf.Gay) || initiator.story.traits.HasTrait(TraitDefOf.Bisexual);
                receipient_into_it = receipient.story.traits.HasTrait(TraitDefOf.Gay) || receipient.story.traits.HasTrait(TraitDefOf.Bisexual);
            }
            else
            {
                initiator_into_it = !initiator.story.traits.HasTrait(TraitDefOf.Gay) && !initiator.story.traits.HasTrait(TraitDefOf.Asexual);
                receipient_into_it = !receipient.story.traits.HasTrait(TraitDefOf.Gay) && !receipient.story.traits.HasTrait(TraitDefOf.Asexual);
            }

            return (initiator_into_it && receipient_into_it) ? 1.0f : 0.15f;
        }
    
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> il = instructions.ToList();

            // Find: return 1.15f * num5 * num6 * num7 * num4 * num8;
            // num5 (slot 4) = 1.0f (gendered attraction)
            // num8 (slot 7) = CalculateAttractionFactor() (gayed attraction)
            for (int i = il.Count - 1; i > 0; --i)
            {
                // This is the load 1.15f; afterwards we write over two of the variables.
                if (il[i].opcode == OpCodes.Ldc_R4 && (float)il[i].operand == 1.15f)
                {
                    il.InsertRange(++i, new CodeInstruction[]
                    {
                        // Remove gendered attraction.
                        new CodeInstruction(OpCodes.Ldc_R4, 1.0f),
                        new CodeInstruction(OpCodes.Stloc_S, 4),

                        // Remove weird gay attraction thing.
                        new CodeInstruction(OpCodes.Ldarg_1),
                        new CodeInstruction(OpCodes.Ldarg_2),
                        new CodeInstruction(OpCodes.Call, _method_calculate),
                        new CodeInstruction(OpCodes.Stloc_S, 7),
                    });

                    return il.AsEnumerable();
                }
            }

            throw new Exception("Could not locate the correct instruction to patch - a mod incompatibility or game update broke it.");
        }
    }

    // This equalises aged attraction.
    [HarmonyPatch(typeof(Pawn_RelationsTracker), "SecondaryLovinChanceFactor")]
    public static class Pawn_RelationsTracker__SecondaryLovinChanceFactor
    {
        private static MethodInfo _method_calculate = AccessTools.Method(typeof(Pawn_RelationsTracker__SecondaryLovinChanceFactor), "CalculateAgeFactor");

        public static float CalculateAgeFactor(float my_age, float their_age)
        {
            float min = (my_age / 2.0f) + 7.0f;
            float max = (my_age - 7.0f) * 2.0f;
            float lower = Mathf.Lerp(min, my_age, 0.6f);
            float upper = Mathf.Lerp(my_age, max, 0.4f);
            return GenMath.FlatHill(0.2f, min, lower, upper, max, 0.2f, their_age);
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> il = instructions.ToList();

            // Find: return num * num2 * num3 * num5;
            // Replace with: return CalculateAgeFactor(my_age, their_age) * num2 * num3 * num5;
            // loc0 = my age, loc1 = their age, loc2 = the age factor
            for (int i = il.Count - 1; i > 0; --i)
            {
                if (il[i].opcode == OpCodes.Ldloc_2)
                {
                    il.InsertRange(++i, new CodeInstruction[]
                    {
                        new CodeInstruction(OpCodes.Pop),
                        new CodeInstruction(OpCodes.Ldloc_0),
                        new CodeInstruction(OpCodes.Ldloc_1),
                        new CodeInstruction(OpCodes.Call, _method_calculate)
                    });

                    return il.AsEnumerable();
                }
            }

            throw new Exception("Could not locate the correct instruction to patch - a mod incompatibility or game update broke it.");
        }
    }
}
