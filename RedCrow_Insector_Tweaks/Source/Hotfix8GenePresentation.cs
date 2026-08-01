using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class Hotfix8GenePresentation
    {
        private const string LogPrefix = "[RedCrow Hotfix 8]";

        private sealed class Presentation
        {
            public readonly string Label;
            public readonly string Description;

            public Presentation(string label, string description)
            {
                Label = label;
                Description = description;
            }
        }

        private static readonly Dictionary<string, Presentation>
            RussianPresentations =
                new Dictionary<string, Presentation>(StringComparer.Ordinal)
                {
                    {
                        "VRE_SwarmSynapse",
                        new Presentation(
                            "синапс роя",
                            "Нервные системы носителей соединяются в общий синаптический рой. Каждый колонист с этим геном усиливает скорость работы остальных носителей, а состав сети периодически пересчитывается.")
                    },
                    {
                        "VRE_RoyalJellyInjector",
                        new Presentation(
                            "инъектор королевского желе",
                            "Специализированный ротовой инъектор позволяет вводить королевское желе другому существу. Процедура оставляет небольшую рану, но временно улучшает настроение и иммунитет цели; повторное применение требует восстановления органа.")
                    },
                    {
                        "VRE_Microsized",
                        new Presentation(
                            "микроразмер",
                            "Тело носителя становится значительно меньше. Снижаются выход мяса и кожи и вместимость желудка, зато уменьшается потребность в пище; небольшая масса увеличивает длительность ошеломления.")
                    },
                    {
                        "VRE_Colossal",
                        new Presentation(
                            "колоссальный размер",
                            "Тело носителя вырастает до колоссальных размеров. Увеличиваются выход мяса и кожи, вместимость желудка и способность использовать тяжёлое вооружение, но возрастают потребности в пище и снижаются скорость движения и уклонение в ближнем бою.")
                    },
                    {
                        "VRE_PyroResistantChitin",
                        new Presentation(
                            "огнестойкий хитин",
                            "Панцирь становится устойчивым к огню: носитель значительно хуже воспламеняется, получает меньше урона пламенем и не впадает в панику, оказавшись в огне.")
                    },
                    {
                        "VRE_FlameGlands",
                        new Presentation(
                            "огненные железы",
                            "Особые железы и резервуар производят горючую жидкость. Носитель может выплёвывать её, поджигая цели и поверхность в месте попадания.")
                    },
                    {
                        "VRE_ChemfuelSacks",
                        new Presentation(
                            "мешки химтоплива",
                            "Хвостовые мешки накапливают химтопливо и периодически позволяют его извлекать. Они резко повышают уязвимость к огню и могут вызвать огненный взрыв при сильном возгорании или смерти носителя.")
                    },
                    {
                        "VRE_Pyrophiliac",
                        new Presentation(
                            "пирофилия",
                            "Носитель испытывает болезненную тягу к огню и приобретает черту пиромана. Его комфортный температурный диапазон смещается в сторону более высокой температуры.")
                    },
                    {
                        "VRE_LocustWings",
                        new Presentation(
                            "крылья саранчи",
                            "Крупные крылья саранчи позволяют носителю совершать дальний прыжок, перелетая через препятствия к незакрытой крышей точке.")
                    },
                    {
                        "VRE_InsectRostrum",
                        new Presentation(
                            "насекомый хоботок",
                            "Большой острый хоботок многократно ускоряет приём пищи и служит мощным колющим оружием ближнего боя с коротким временем восстановления.")
                    },
                    {
                        "VRE_InsectVolatile",
                        new Presentation(
                            "взрывной нрав",
                            "Носитель становится крайне вспыльчивым: чаще начинает социальные драки, склонен к агрессивным нервным срывам и побегам из заключения и использует оружие в социальных драках.")
                    },
                    {
                        "VRE_EcdysoneOverdrive",
                        new Presentation(
                            "экдизоновый разгон",
                            "Переизбыток экдизона создаёт постоянную потребность в насыщении убийством. Долгое отсутствие побед в ближнем бою вызывает всё более тяжёлые штрафы настроения.")
                    },
                    {
                        "VRE_AcidGlands",
                        new Presentation(
                            "кислотные железы",
                            "Развитые кислотные железы позволяют выплёвывать едкую жидкость, поражающую существ, предметы и постройки в выбранной области.")
                    },
                    {
                        "VRE_InfraredSensors",
                        new Presentation(
                            "инфракрасные сенсоры",
                            "Инфракрасные сенсоры позволяют видеть в темноте без обычных штрафов к работе и движению и немного повышают точность стрельбы на средней и дальней дистанции.")
                    },
                    {
                        "VRE_AcidBurstSack",
                        new Presentation(
                            "кислотный взрывной мешок",
                            "При получении сильного удара мешок выбрасывает вокруг носителя облако едкой кислоты, покрывая область коррозионной жидкостью и нанося продолжительный урон всем попавшим в выброс.")
                    },
                    {
                        "VRE_SolidGreyMatter",
                        new Presentation(
                            "затвердевшее серое вещество",
                            "Затвердевшая ткань мозга полностью лишает носителя способности обучаться и ускоряет потерю навыков, причём деградация затрагивает даже навыки ниже десятого уровня.")
                    },
                    {
                        "VRE_MineralRichInsectskin",
                        new Presentation(
                            "минерализованная кожа насекомого",
                            "Минеральные отложения формируют толстую естественную броню. Панцирь хорошо защищает от тупого и острого урона, пока накопленные повреждения не ослабят его.")
                    },
                    {
                        "VRE_ChargerClaws",
                        new Presentation(
                            "штурмовые клешни",
                            "Массивные клешни позволяют совершать разрушительный рывок к цели, сметая существ и оборонительные сооружения на пути, но мешают тонкой работе.")
                    },
                    {
                        "VRE_HardLockedJoints",
                        new Presentation(
                            "заклинившие суставы",
                            "Жёстко зафиксированные суставы сильно ограничивают сгибание конечностей и быстрые движения, заметно снижая скорость передвижения.")
                    },
                    {
                        "VRE_PassiveInsect",
                        new Presentation(
                            "пассивность",
                            "Носитель полностью утрачивает интерес к насилию и не способен выполнять боевую работу или охотиться.")
                    },
                    {
                        "RC_Mutation_BiologicalSickle",
                        new Presentation(
                            "биологические рабочие инструменты",
                            "Комплекс хитиновых серпов, лопат, топоров и ударных пластин превращает тело носителя в универсальный рабочий инструмент улья. Скорость работы с растениями, добычи и обрезки повышается на 30%, строительства — на 35%, кузнечного дела — на 30%. Постоянное питание органов увеличивает скорость голода на 0,2.")
                    }
                };

        static Hotfix8GenePresentation()
        {
            try
            {
                Apply("static startup");
                LongEventHandler.ExecuteWhenFinished(
                    delegate { Apply("long-event completion"); });

                MethodInfo target = AccessTools.Method(
                    typeof(Game),
                    "FinalizeInit");
                MethodInfo postfixMethod = AccessTools.Method(
                    typeof(Hotfix8GenePresentation),
                    "GameFinalizeInitPostfix");
                if (target != null && postfixMethod != null)
                {
                    Harmony harmony = new Harmony(
                        "RedCrow.InsectorTweaks.Hotfix8GenePresentation");
                    HarmonyMethod postfix = new HarmonyMethod(postfixMethod);
                    postfix.priority = Priority.Last;
                    harmony.Patch(target, postfix: postfix);
                }
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Installation failed:\n" + exception);
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void GameFinalizeInitPostfix()
        {
            Apply("Game.FinalizeInit");
        }

        private static void Apply(string source)
        {
            if (!IsRussianLanguage())
            {
                return;
            }

            int found = 0;
            int updated = 0;
            foreach (KeyValuePair<string, Presentation> pair in
                RussianPresentations)
            {
                GeneDef gene = DefDatabase<GeneDef>.GetNamedSilentFail(
                    pair.Key);
                if (gene == null)
                {
                    continue;
                }

                found++;
                Presentation presentation = pair.Value;
                if (!string.Equals(
                        gene.label,
                        presentation.Label,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        gene.description,
                        presentation.Description,
                        StringComparison.Ordinal))
                {
                    gene.label = presentation.Label;
                    gene.description = presentation.Description;
                    updated++;
                }
            }

            ClearGeneListCache();
            Log.Message(
                LogPrefix + " Russian gene presentation synchronized at " +
                source + ": found=" + found + "/" +
                RussianPresentations.Count + ", updated=" + updated + ".");
        }

        private static bool IsRussianLanguage()
        {
            object language = null;
            PropertyInfo property = AccessTools.Property(
                typeof(LanguageDatabase),
                "activeLanguage");
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                language = property.GetValue(null, null);
            }

            if (language == null)
            {
                FieldInfo field = AccessTools.Field(
                    typeof(LanguageDatabase),
                    "activeLanguage");
                if (field != null)
                {
                    language = field.GetValue(null);
                }
            }

            if (language == null)
            {
                return false;
            }

            string[] memberNames =
            {
                "folderName",
                "FolderName",
                "FriendlyNameEnglish",
                "friendlyNameEnglish",
                "FriendlyNameNative",
                "friendlyNameNative"
            };

            Type type = language.GetType();
            for (int index = 0; index < memberNames.Length; index++)
            {
                string name = memberNames[index];
                PropertyInfo languageProperty = AccessTools.Property(type, name);
                object value = languageProperty != null &&
                    languageProperty.GetIndexParameters().Length == 0
                        ? languageProperty.GetValue(language, null)
                        : null;
                if (value == null)
                {
                    FieldInfo languageField = AccessTools.Field(type, name);
                    value = languageField == null
                        ? null
                        : languageField.GetValue(language);
                }

                string text = value as string;
                if (!string.IsNullOrEmpty(text) && IsRussianName(text))
                {
                    return true;
                }
            }

            return IsRussianName(language.ToString());
        }

        private static bool IsRussianName(string value)
        {
            string normalized =
                (value ?? string.Empty).Trim().ToLowerInvariant();
            return normalized == "russian" ||
                normalized.Contains("russian") ||
                normalized.Contains("русск");
        }

        private static void ClearGeneListCache()
        {
            Type utilsType = AccessTools.TypeByName(
                "VanillaRacesExpandedInsector.Utils");
            FieldInfo cacheField = utilsType == null
                ? null
                : AccessTools.Field(utilsType, "cachedGeneDefsInOrder");
            if (cacheField != null &&
                cacheField.IsStatic &&
                !cacheField.IsInitOnly)
            {
                cacheField.SetValue(null, null);
            }
        }
    }
}
