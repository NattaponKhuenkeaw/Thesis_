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

            bool isPercent = statKey == "percent"
                || statKey.Contains("crit")
                || statKey.Contains("mastery") 
                || statKey == "cm"
                || statKey == "dm"
                || statKey == "di"
                || statKey == "adi"
                || statKey == "aer"
                || statKey.Contains("resist") 
                || statKey.Contains("ae");

            bool isBaseFlatBonusPercent = statKey.Contains("hp") 
                || statKey.Contains("atk") 
                || statKey.Contains("def");

            if (isSingleValue)
            {
                // For a single value, baseValue is the value to format
                if (isPercent || isBaseFlatBonusPercent)
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
            if (isPercent)
            {
                float totalValue = baseValue + bonusValue;
                string formattedTotal = totalValue.ToString("0.#");
                if (useRichTextColor)
                {
                    if (Mathf.Abs(bonusValue) > 0.001f)
                    {
                        string colorTag = bonusValue > 0.001f ? "#4ADE80" : "#EF4444";
                        string sign = bonusValue >= -0.001f ? "+" : "-";
                        string formattedBonus = FormatFloat(bonusValue);
                        return $"{formattedTotal}% (<color={colorTag}>{sign}{formattedBonus}%</color>)";
                    }
                    return $"{formattedTotal}%";
                }
                else
                {
                    if (Mathf.Abs(bonusValue) > 0.001f)
                    {
                        string sign = bonusValue >= -0.001f ? "+" : "-";
                        string formattedBonus = FormatFloat(bonusValue);
                        return $"{formattedTotal}%({sign}{formattedBonus}%)";
                    }
                    return $"{formattedTotal}%";
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
                float totalValue = baseValue + bonusValue;
                string formattedTotal = Mathf.RoundToInt(totalValue).ToString();
                if (useRichTextColor)
                {
                    if (Mathf.Abs(bonusValue) > 0.001f)
                    {
                        string colorTag = bonusValue > 0.001f ? "#4ADE80" : "#EF4444";
                        string sign = bonusValue >= 0f ? "+" : "";
                        string formattedBonus = Mathf.RoundToInt(bonusValue).ToString();
                        return $"{formattedTotal} (<color={colorTag}>{sign}{formattedBonus}</color>)";
                    }
                    return $"{formattedTotal}";
                }
                else
                {
                    if (Mathf.Abs(bonusValue) > 0.001f)
                    {
                        string sign = bonusValue >= 0f ? "+" : "";
                        string formattedBonus = Mathf.RoundToInt(bonusValue).ToString();
                        return $"{formattedTotal}({sign}{formattedBonus})";
                    }
                    return $"{formattedTotal}";
                }
            }
        }

        public static string FormatResolved(float baseValue, float finalValue, string statName, bool useRichTextColor = false)
        {
            string statKey = statName != null ? statName.ToLowerInvariant() : "";
            bool isPercent = statKey == "percent"
                || statKey.Contains("crit")
                || statKey.Contains("mastery")
                || statKey == "cm"
                || statKey == "dm"
                || statKey == "di"
                || statKey == "adi"
                || statKey == "aer"
                || statKey.Contains("resist")
                || statKey.Contains("ae");

            float delta = finalValue - baseValue;
            if (Mathf.Abs(delta) <= 0.001f)
            {
                return Format(baseValue, 0f, statName, useRichTextColor);
            }

            if (isPercent)
            {
                string totalText = $"{finalValue:0.#}%";
                string sign = delta >= 0f ? "+" : "-";
                string bonusText = $"{sign}{FormatFloat(Mathf.Abs(delta))}%";
                if (!useRichTextColor)
                {
                    return $"{totalText}({bonusText})";
                }

                string color = delta >= 0f ? "#4ADE80" : "#EF4444";
                return $"{totalText} (<color={color}>{bonusText}</color>)";
            }

            string finalText = Mathf.RoundToInt(finalValue).ToString();
            string deltaSign = delta >= 0f ? "+" : "-";
            string deltaText = $"{deltaSign}{Mathf.RoundToInt(Mathf.Abs(delta))}";
            if (!useRichTextColor)
            {
                return $"{finalText}({deltaText})";
            }

            string colorTag = delta >= 0f ? "#4ADE80" : "#EF4444";
            return $"{finalText} (<color={colorTag}>{deltaText}</color>)";
        }
    }
}
