using UnityEngine;

[System.Serializable]
public class ElementalGauge
{
    public ElementType elementType { get; private set; }
    public float maxGaugeValue { get; private set; }
    public float currentGaugeValue { get; private set; }

    public ElementalGauge(ElementType element, float maxVal)
    {
        elementType = element;
        maxGaugeValue = maxVal;
        currentGaugeValue = 0f;
    }

    /// <summary>
    /// Adds amount to the gauge. Returns true if a reaction is triggered (reaches max).
    /// </summary>
    public bool AddGauge(float amount)
    {
        if (maxGaugeValue <= 0f) return false;
        if (currentGaugeValue >= maxGaugeValue) return false;

        currentGaugeValue = Mathf.Min(currentGaugeValue + amount, maxGaugeValue);
        return currentGaugeValue >= maxGaugeValue;
    }

    public void ResetGauge()
    {
        currentGaugeValue = 0f;
    }

    public float GetProgressRatio()
    {
        return maxGaugeValue > 0f ? currentGaugeValue / maxGaugeValue : 0f;
    }
}
