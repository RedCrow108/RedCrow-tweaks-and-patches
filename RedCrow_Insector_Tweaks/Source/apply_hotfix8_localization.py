#!/usr/bin/env python3
"""Apply Hotfix 8 Russian presentation fixes without changing balance or pools."""

from __future__ import annotations

import re
import xml.etree.ElementTree as ET
from pathlib import Path

SOURCE_DIR = Path(__file__).resolve().parent
MOD_ROOT = SOURCE_DIR.parent
BALANCE_SOURCE = SOURCE_DIR / "PherocoreBalanceIntegration.cs"
LEGACY_PROJECT = SOURCE_DIR / "RedCrow.InsectorTweaks.csproj"
RUSSIAN_DIR = (
    MOD_ROOT
    / "Languages"
    / "Russian"
    / "DefInjected"
    / "GenelineGeneDef"
)
LOCALIZATION_PATH = RUSSIAN_DIR / "Hotfix8.xml"
RUNTIME_PATH = SOURCE_DIR / "Hotfix8GenePresentation.cs"

PRESENTATIONS = {
    "VRE_SwarmSynapse": (
        "синапс роя",
        "Нервные системы носителей соединяются в общий синаптический рой. Каждый колонист с этим геном усиливает скорость работы остальных носителей, а состав сети периодически пересчитывается.",
    ),
    "VRE_RoyalJellyInjector": (
        "инъектор королевского желе",
        "Специализированный ротовой инъектор позволяет вводить королевское желе другому существу. Процедура оставляет небольшую рану, но временно улучшает настроение и иммунитет цели; повторное применение требует восстановления органа.",
    ),
    "VRE_Microsized": (
        "микроразмер",
        "Тело носителя становится значительно меньше. Снижаются выход мяса и кожи и вместимость желудка, зато уменьшается потребность в пище; небольшая масса увеличивает длительность ошеломления.",
    ),
    "VRE_Colossal": (
        "колоссальный размер",
        "Тело носителя вырастает до колоссальных размеров. Увеличиваются выход мяса и кожи, вместимость желудка и способность использовать тяжёлое вооружение, но возрастают потребности в пище и снижаются скорость движения и уклонение в ближнем бою.",
    ),
    "VRE_PyroResistantChitin": (
        "огнестойкий хитин",
        "Панцирь становится устойчивым к огню: носитель значительно хуже воспламеняется, получает меньше урона пламенем и не впадает в панику, оказавшись в огне.",
    ),
    "VRE_FlameGlands": (
        "огненные железы",
        "Особые железы и резервуар производят горючую жидкость. Носитель может выплёвывать её, поджигая цели и поверхность в месте попадания.",
    ),
    "VRE_ChemfuelSacks": (
        "мешки химтоплива",
        "Хвостовые мешки накапливают химтопливо и периодически позволяют его извлекать. Они резко повышают уязвимость к огню и могут вызвать огненный взрыв при сильном возгорании или смерти носителя.",
    ),
    "VRE_Pyrophiliac": (
        "пирофилия",
        "Носитель испытывает болезненную тягу к огню и приобретает черту пиромана. Его комфортный температурный диапазон смещается в сторону более высокой температуры.",
    ),
    "VRE_LocustWings": (
        "крылья саранчи",
        "Крупные крылья саранчи позволяют носителю совершать дальний прыжок, перелетая через препятствия к незакрытой крышей точке.",
    ),
    "VRE_InsectRostrum": (
        "насекомый хоботок",
        "Большой острый хоботок многократно ускоряет приём пищи и служит мощным колющим оружием ближнего боя с коротким временем восстановления.",
    ),
    "VRE_InsectVolatile": (
        "взрывной нрав",
        "Носитель становится крайне вспыльчивым: чаще начинает социальные драки, склонен к агрессивным нервным срывам и побегам из заключения и использует оружие в социальных драках.",
    ),
    "VRE_EcdysoneOverdrive": (
        "экдизоновый разгон",
        "Переизбыток экдизона создаёт постоянную потребность в насыщении убийством. Долгое отсутствие побед в ближнем бою вызывает всё более тяжёлые штрафы настроения.",
    ),
    "VRE_AcidGlands": (
        "кислотные железы",
        "Развитые кислотные железы позволяют выплёвывать едкую жидкость, поражающую существ, предметы и постройки в выбранной области.",
    ),
    "VRE_InfraredSensors": (
        "инфракрасные сенсоры",
        "Инфракрасные сенсоры позволяют видеть в темноте без обычных штрафов к работе и движению и немного повышают точность стрельбы на средней и дальней дистанции.",
    ),
    "VRE_AcidBurstSack": (
        "кислотный взрывной мешок",
        "При получении сильного удара мешок выбрасывает вокруг носителя облако едкой кислоты, покрывая область коррозионной жидкостью и нанося продолжительный урон всем попавшим в выброс.",
    ),
    "VRE_SolidGreyMatter": (
        "затвердевшее серое вещество",
        "Затвердевшая ткань мозга полностью лишает носителя способности обучаться и ускоряет потерю навыков, причём деградация затрагивает даже навыки ниже десятого уровня.",
    ),
    "VRE_MineralRichInsectskin": (
        "минерализованная кожа насекомого",
        "Минеральные отложения формируют толстую естественную броню. Панцирь хорошо защищает от тупого и острого урона, пока накопленные повреждения не ослабят его.",
    ),
    "VRE_ChargerClaws": (
        "штурмовые клешни",
        "Массивные клешни позволяют совершать разрушительный рывок к цели, сметая существ и оборонительные сооружения на пути, но мешают тонкой работе.",
    ),
    "VRE_HardLockedJoints": (
        "заклинившие суставы",
        "Жёстко зафиксированные суставы сильно ограничивают сгибание конечностей и быстрые движения, заметно снижая скорость передвижения.",
    ),
    "VRE_PassiveInsect": (
        "пассивность",
        "Носитель полностью утрачивает интерес к насилию и не способен выполнять боевую работу или охотиться.",
    ),
    "RC_Mutation_BiologicalSickle": (
        "биологические рабочие инструменты",
        "Комплекс хитиновых серпов, лопат, топоров и ударных пластин превращает тело носителя в универсальный рабочий инструмент улья. Скорость работы с растениями, добычи и обрезки повышается на 30%, строительства — на 35%, кузнечного дела — на 30%. Постоянное питание органов увеличивает скорость голода на 0,2.",
    ),
}


def write_if_changed(path: Path, content: str) -> None:
    old = path.read_text(encoding="utf-8-sig") if path.exists() else None
    if old != content:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")


def xml_escape(value: str) -> str:
    return (
        value.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
    )


def build_localization() -> str:
    lines = ['<?xml version="1.0" encoding="utf-8"?>', "<LanguageData>"]
    for def_name, (label, description) in PRESENTATIONS.items():
        lines.append(
            f"  <{def_name}.label>{xml_escape(label)}</{def_name}.label>"
        )
        lines.append(
            f"  <{def_name}.description>{xml_escape(description)}</{def_name}.description>"
        )
    lines.append("</LanguageData>")
    return "\n".join(lines) + "\n"


def csharp_string(value: str) -> str:
    return value.replace("\\", "\\\\").replace('"', '\\"')


def build_runtime_source() -> str:
    entries = []
    for def_name, (label, description) in PRESENTATIONS.items():
        entries.append(
            "                    {\n"
            f'                        "{csharp_string(def_name)}",\n'
            "                        new Presentation(\n"
            f'                            "{csharp_string(label)}",\n'
            f'                            "{csharp_string(description)}")\n'
            "                    }"
        )
    dictionary = ",\n".join(entries)
    return f'''using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{{
    [StaticConstructorOnStartup]
    public static class Hotfix8GenePresentation
    {{
        private const string LogPrefix = "[RedCrow Hotfix 8]";

        private sealed class Presentation
        {{
            public readonly string Label;
            public readonly string Description;

            public Presentation(string label, string description)
            {{
                Label = label;
                Description = description;
            }}
        }}

        private static readonly Dictionary<string, Presentation>
            RussianPresentations =
                new Dictionary<string, Presentation>(StringComparer.Ordinal)
                {{
{dictionary}
                }};

        static Hotfix8GenePresentation()
        {{
            try
            {{
                Apply("static startup");
                LongEventHandler.ExecuteWhenFinished(
                    delegate {{ Apply("long-event completion"); }});

                MethodInfo target = AccessTools.Method(
                    typeof(Game),
                    "FinalizeInit");
                MethodInfo postfixMethod = AccessTools.Method(
                    typeof(Hotfix8GenePresentation),
                    "GameFinalizeInitPostfix");
                if (target != null && postfixMethod != null)
                {{
                    Harmony harmony = new Harmony(
                        "RedCrow.InsectorTweaks.Hotfix8GenePresentation");
                    HarmonyMethod postfix = new HarmonyMethod(postfixMethod);
                    postfix.priority = Priority.Last;
                    harmony.Patch(target, postfix: postfix);
                }}
            }}
            catch (Exception exception)
            {{
                Log.Error(
                    LogPrefix + " Installation failed:\\n" + exception);
            }}
        }}

        [HarmonyPriority(Priority.Last)]
        public static void GameFinalizeInitPostfix()
        {{
            Apply("Game.FinalizeInit");
        }}

        private static void Apply(string source)
        {{
            if (!IsRussianLanguage())
            {{
                return;
            }}

            int found = 0;
            int updated = 0;
            foreach (KeyValuePair<string, Presentation> pair in
                RussianPresentations)
            {{
                GeneDef gene = DefDatabase<GeneDef>.GetNamedSilentFail(
                    pair.Key);
                if (gene == null)
                {{
                    continue;
                }}

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
                {{
                    gene.label = presentation.Label;
                    gene.description = presentation.Description;
                    updated++;
                }}
            }}

            ClearGeneListCache();
            Log.Message(
                LogPrefix + " Russian gene presentation synchronized at " +
                source + ": found=" + found + "/" +
                RussianPresentations.Count + ", updated=" + updated + ".");
        }}

        private static bool IsRussianLanguage()
        {{
            object language = null;
            PropertyInfo property = AccessTools.Property(
                typeof(LanguageDatabase),
                "activeLanguage");
            if (property != null && property.GetIndexParameters().Length == 0)
            {{
                language = property.GetValue(null, null);
            }}

            if (language == null)
            {{
                FieldInfo field = AccessTools.Field(
                    typeof(LanguageDatabase),
                    "activeLanguage");
                if (field != null)
                {{
                    language = field.GetValue(null);
                }}
            }}

            if (language == null)
            {{
                return false;
            }}

            string[] memberNames =
            {{
                "folderName",
                "FolderName",
                "FriendlyNameEnglish",
                "friendlyNameEnglish",
                "FriendlyNameNative",
                "friendlyNameNative"
            }};

            Type type = language.GetType();
            for (int index = 0; index < memberNames.Length; index++)
            {{
                string name = memberNames[index];
                PropertyInfo languageProperty = AccessTools.Property(type, name);
                object value = languageProperty != null &&
                    languageProperty.GetIndexParameters().Length == 0
                        ? languageProperty.GetValue(language, null)
                        : null;
                if (value == null)
                {{
                    FieldInfo languageField = AccessTools.Field(type, name);
                    value = languageField == null
                        ? null
                        : languageField.GetValue(language);
                }}

                string text = value as string;
                if (!string.IsNullOrEmpty(text) && IsRussianName(text))
                {{
                    return true;
                }}
            }}

            return IsRussianName(language.ToString());
        }}

        private static bool IsRussianName(string value)
        {{
            string normalized =
                (value ?? string.Empty).Trim().ToLowerInvariant();
            return normalized == "russian" ||
                normalized.Contains("russian") ||
                normalized.Contains("русск");
        }}

        private static void ClearGeneListCache()
        {{
            Type utilsType = AccessTools.TypeByName(
                "VanillaRacesExpandedInsector.Utils");
            FieldInfo cacheField = utilsType == null
                ? null
                : AccessTools.Field(utilsType, "cachedGeneDefsInOrder");
            if (cacheField != null &&
                cacheField.IsStatic &&
                !cacheField.IsInitOnly)
            {{
                cacheField.SetValue(null, null);
            }}
        }}
    }}
}}
'''


def update_legacy_project() -> None:
    text = LEGACY_PROJECT.read_text(encoding="utf-8")
    compile_line = '    <Compile Include="Hotfix8GenePresentation.cs" />'
    if compile_line not in text:
        anchor = '    <Compile Include="HiveInsectFilthPatch.cs" />'
        if anchor not in text:
            raise RuntimeError("Legacy project compile anchor was not found")
        text = text.replace(anchor, anchor + "\n" + compile_line, 1)
    write_if_changed(LEGACY_PROJECT, text)


def validate() -> None:
    if len(PRESENTATIONS) != 21:
        raise RuntimeError(
            f"Expected 21 Hotfix 8 presentations, found {len(PRESENTATIONS)}"
        )

    tree = ET.parse(LOCALIZATION_PATH)
    root = tree.getroot()
    expected_tags = {
        f"{def_name}.{field}"
        for def_name in PRESENTATIONS
        for field in ("label", "description")
    }
    actual_tags = {child.tag for child in root}
    if actual_tags != expected_tags:
        missing = sorted(expected_tags - actual_tags)
        extra = sorted(actual_tags - expected_tags)
        raise RuntimeError(
            "Hotfix 8 localization mismatch. Missing="
            + ", ".join(missing)
            + "; extra="
            + ", ".join(extra)
        )

    balance = BALANCE_SOURCE.read_text(encoding="utf-8")
    if balance.count("new BalanceEntry") != 115:
        raise RuntimeError("Hotfix 8 unexpectedly changed the balance entry count")

    runtime = RUNTIME_PATH.read_text(encoding="utf-8")
    for def_name in PRESENTATIONS:
        if f'"{def_name}"' not in runtime:
            raise RuntimeError(
                f"Runtime presentation is missing {def_name}"
            )

    for xml_path in MOD_ROOT.rglob("*.xml"):
        ET.parse(xml_path)

    print(
        "Hotfix 8 prepared: 20 restored HSK Insector genes and the merged "
        "biological tools receive stable Russian labels and descriptions; "
        "balance and pherocore pools remain unchanged."
    )


def main() -> int:
    write_if_changed(LOCALIZATION_PATH, build_localization())
    write_if_changed(RUNTIME_PATH, build_runtime_source())
    update_legacy_project()
    validate()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
