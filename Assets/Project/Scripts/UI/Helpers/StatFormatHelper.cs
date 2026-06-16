using UnityEngine;

namespace Project.UI
{
    public static class StatFormatHelper
    {
        public static string Format(float baseValue, float bonusValue, bool useRichTextColor = false)
        {
            return Format(baseValue, bonusValue, "", useRichTextColor, false);
        }

        private static string FormatFloat(float value)
        {
            float absVal = Mathf.Abs(value);
            return absVal.ToString("0.#");
        }

        public static string Format(float baseValue, float bonusValue, string statName, bool useRichTextColor = false, bool isSingleValue = false)
        {
            string statKey = statName != null ? statName.ToLowerInvariant() : "";

            bool isCDR = statKey.Contains("cdr") || statKey.Contains("cooldown");
            bool isPercent = isCDR 
                || statKey.Contains("crit") 
                || statKey.Contains("mastery") 
                || statKey.Contains("resist") 
                || statKey.Contains("elemental") 
                || statKey.Contains("ae")
                || statKey.Contains("impact");

            bool isBaseFlatBonusPercent = statKey.Contains("hp") 
                || statKey.Contains("atk") 
                || statKey.Contains("def");

            if (isSingleValue)
            {
                // For a single value, baseValue is the value to format
                if (isCDR)
                {
                    if (useRichTextColor)
                    {
                        string colorTag = baseValue > 0.001f ? "#4ADE80" : "#EF4444";
                        string sign = baseValue >= 0f ? "-" : "+";
                        string formattedVal = FormatFloat(baseValue);
                        return $"<color={colorTag}>{sign}{formattedVal}%</color>";
                    }
                    else
                    {
                        string sign = baseValue >= 0f ? "-" : "+";
                        string formattedVal = FormatFloat(baseValue);
                        return $"{sign}{formattedVal}%";
                    }
                }
                else if (isPercent || isBaseFlatBonusPercent)
                {
                    if (useRichTextColor)
                    {
                        string colorTag = baseValue > 0.001f ? "#4ADE80" : "#EF4444";
                        string sign = baseValue >= -0.001f ? "+" : "-";
                        string formattedVal = FormatFloat(baseValue);
                        return $"<color={colorTag}>{sign}{formattedVal}%</color>";
                    }
                    else
                    {
                        string sign = baseValue >= -0.001f ? "+" : "-";
                        string formattedVal = FormatFloat(baseValue);
                        return $"{sign}{formattedVal}%";
                    }
                }
                else
                {
                    if (useRichTextColor)
                    {
                        string colorTag = baseValue > 0.001f ? "#4ADE80" : "#EF4444";
                        string sign = baseValue >= 0f ? "+" : "";
                        string formattedVal = Mathf.RoundToInt(baseValue).ToString();
                        return $"<color={colorTag}>{sign}{formattedVal}</color>";
                    }
                    else
                    {
                        string sign = baseValue >= 0f ? "+" : "";
                        string formattedVal = Mathf.RoundToInt(baseValue).ToString();
                        return $"{sign}{formattedVal}";
                    }
                }
            }

            // Standard base + bonus formatting
            if (isCDR)
            {
                if (useRichTextColor)
                {
                    if (Mathf.Abs(bonusValue) > 0.001f)
                    {
                        string colorTag = bonusValue > 0.001f ? "#4ADE80" : "#EF4444";
                        string sign = bonusValue >= 0f ? "-" : "+";
                        string formattedBonus = FormatFloat(bonusValue);
                        string formattedBase = baseValue.ToString("0.#");
                        return $"-{formattedBase}% (<color={colorTag}>{sign}{formattedBonus}%</color>)";
                    }
                    return $"-{baseValue.ToString("0.#")}%";
                }
                else
                {
                    if (Mathf.Abs(bonusValue) > 0.001f)
                    {
                        string sign = bonusValue >= 0f ? "-" : "+";
                        string formattedBonus = FormatFloat(bonusValue);
                        string formattedBase = baseValue.ToString("0.#");
                        return $"-{formattedBase}%({sign}{formattedBonus}%)";
                    }
                    return $"-{baseValue.ToString("0.#")}%";
                }
            }
            else if (isPercent)
            {
                if (useRichTextColor)
                {
                    if (Mathf.Abs(bonusValue) > 0.001f)
                    {
                        string colorTag = bonusValue > 0.001f ? "#4ADE80" : "#EF4444";
                        string sign = bonusValue >= -0.001f ? "+" : "-";
                        string formattedBonus = FormatFloat(bonusValue);
                        string formattedBase = baseValue.ToString("0.#");
                        return $"{formattedBase}% (<color={colorTag}>{sign}{formattedBonus}%</color>)";
                    }
                    return $"{baseValue.ToString("0.#")}%";
                }
                else
                {
                    if (Mathf.Abs(bonusValue) > 0.001f)
                    {
                        string sign = bonusValue >= -0.001f ? "+" : "-";
                        string formattedBonus = FormatFloat(bonusValue);
                        string formattedBase = baseValue.ToString("0.#");
                        return $"{formattedBase}%({sign}{formattedBonus}%)";
                    }
                    return $"{baseValue.ToString("0.#")}%";
                }
            }
            else if (isBaseFlatBonusPercent)
            {
                float additionAmount = baseValue * (bonusValue / 100f);
                float totalValue = baseValue + additionAmount;
                string formattedTotal = Mathf.RoundToInt(totalValue).ToString();
                string formattedAddition = Mathf.RoundToInt(Mathf.Abs(additionAmount)).ToString();

                if (useRichTextColor)
                {
                    if (Mathf.Abs(bonusValue) > 0.001f)
                    {
                        string colorTag = additionAmount > 0.001f ? "#4ADE80" : "#EF4444";
                        string sign = additionAmount >= 0f ? "+" : "-";
                        return $"{formattedTotal} (<color={colorTag}>{sign}{formattedAddition}</color>)";
                    }
                    return $"{formattedTotal}";
                }
                else
                {
                    if (Mathf.Abs(bonusValue) > 0.001f)
                    {
                        string sign = additionAmount >= 0f ? "+" : "-";
                        return $"{formattedTotal} ({sign}{formattedAddition})";
                    }
                    return $"{formattedTotal}";
                }
            }
            else
            {
                string formattedBase = Mathf.RoundToInt(baseValue).ToString();
                if (useRichTextColor)
                {
                    if (Mathf.Abs(bonusValue) > 0.001f)
                    {
                        string colorTag = bonusValue > 0.001f ? "#4ADE80" : "#EF4444";
                        string sign = bonusValue >= 0f ? "+" : "";
                        string formattedBonus = Mathf.RoundToInt(bonusValue).ToString();
                        return $"{formattedBase} (<color={colorTag}>{sign}{formattedBonus}</color>)";
                    }
                    return $"{formattedBase}";
                }
                else
                {
                    if (Mathf.Abs(bonusValue) > 0.001f)
                    {
                        string sign = bonusValue >= 0f ? "+" : "";
                        string formattedBonus = Mathf.RoundToInt(bonusValue).ToString();
                        return $"{formattedBase}({sign}{formattedBonus})";
                    }
                    return $"{formattedBase}";
                }
            }
        }
    }
}
