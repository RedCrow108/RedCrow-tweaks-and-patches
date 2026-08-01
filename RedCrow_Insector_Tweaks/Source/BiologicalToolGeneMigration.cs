using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class BiologicalToolGeneMigration
    {
        private const string LogPrefix =
            "[RedCrow Biological Tools]";
        private const string CanonicalDefName =
            "RC_Mutation_BiologicalSickle";

        private static readonly HashSet<string> LegacyDefNames =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "RC_Mutation_BiologicalHandaxe",
                "RC_Mutation_BiologicalDiggingTools",
                "RC_Mutation_BiologicalHammer"
            };

        static BiologicalToolGeneMigration()
        {
            try
            {
                MethodInfo target = AccessTools.Method(
                    typeof(Game),
                    "FinalizeInit");
                MethodInfo postfixMethod = AccessTools.Method(
                    typeof(BiologicalToolGeneMigration),
                    "GameFinalizeInitPostfix");
                if (target == null || postfixMethod == null)
                {
                    Log.Error(
                        LogPrefix + " Game.FinalizeInit could not be patched.");
                    return;
                }

                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.BiologicalToolGeneMigration");
                HarmonyMethod postfix = new HarmonyMethod(postfixMethod);
                postfix.priority = Priority.Last;
                harmony.Patch(target, postfix: postfix);
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Migration patch failed:\n" + exception);
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void GameFinalizeInitPostfix()
        {
            GeneDef canonical =
                DefDatabase<GeneDef>.GetNamedSilentFail(CanonicalDefName);
            if (canonical == null)
            {
                Log.Error(LogPrefix + " Canonical mutation was not found.");
                return;
            }

            MethodInfo addGene = AccessTools.Method(
                typeof(Pawn_GeneTracker),
                "AddGene",
                new[] { typeof(GeneDef), typeof(bool) });
            MethodInfo removeGene = AccessTools.Method(
                typeof(Pawn_GeneTracker),
                "RemoveGene",
                new[] { typeof(Gene) });
            if (addGene == null || removeGene == null)
            {
                Log.Error(LogPrefix + " Gene migration methods were not found.");
                return;
            }

            int replaced = 0;
            List<Pawn> pawns =
                PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead;
            for (int pawnIndex = 0; pawnIndex < pawns.Count; pawnIndex++)
            {
                Pawn pawn = pawns[pawnIndex];
                if (pawn == null || pawn.genes == null)
                {
                    continue;
                }

                List<Gene> snapshot =
                    new List<Gene>(pawn.genes.GenesListForReading);
                List<Gene> legacy = new List<Gene>();
                bool hasCanonical = false;
                bool xenogene = false;

                for (int geneIndex = 0; geneIndex < snapshot.Count; geneIndex++)
                {
                    Gene gene = snapshot[geneIndex];
                    if (gene == null || gene.def == null)
                    {
                        continue;
                    }

                    if (gene.def.defName == CanonicalDefName)
                    {
                        hasCanonical = true;
                    }
                    else if (LegacyDefNames.Contains(gene.def.defName))
                    {
                        legacy.Add(gene);
                        xenogene = xenogene || IsXenogene(gene);
                    }
                }

                if (legacy.Count == 0)
                {
                    continue;
                }

                if (!hasCanonical)
                {
                    addGene.Invoke(
                        pawn.genes,
                        new object[] { canonical, xenogene });
                }

                for (int index = 0; index < legacy.Count; index++)
                {
                    removeGene.Invoke(
                        pawn.genes,
                        new object[] { legacy[index] });
                    replaced++;
                }
            }

            if (replaced > 0)
            {
                Log.Message(
                    LogPrefix + " Replaced legacy biological tool genes: " +
                    replaced + ".");
            }
        }

        private static bool IsXenogene(Gene gene)
        {
            PropertyInfo property = AccessTools.Property(
                gene.GetType(),
                "Xenogene");
            if (property != null && property.PropertyType == typeof(bool))
            {
                return (bool)property.GetValue(gene, null);
            }

            FieldInfo field = AccessTools.Field(
                gene.GetType(),
                "xenogene");
            return field != null &&
                field.FieldType == typeof(bool) &&
                (bool)field.GetValue(gene);
        }
    }
}
