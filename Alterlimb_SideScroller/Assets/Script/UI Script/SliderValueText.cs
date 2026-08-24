using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SliderValueText
    : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] Slider slider;
    [SerializeField] TMP_Text label;

    [Header("Format")]
    [SerializeField] string suffix = " %";

    int lastPercent = -1;

    void Reset()
    {
        slider = GetComponentInParent<Slider>();
        label = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        if (slider != null)
            slider.onValueChanged.AddListener(OnSliderChanged);
        Refresh();
    }

    void OnDisable()
    {
        if (slider != null)
            slider.onValueChanged.RemoveListener(OnSliderChanged);
    }

    void OnSliderChanged(float value)
    {
        Refresh();
    }

    void Refresh()
    {
        if (slider == null || label == null) return;

        int percent = Mathf.RoundToInt(slider.normalizedValue * 100f);
        if (percent == lastPercent) return;
        lastPercent = percent;

        label.text = percent.ToString("000") + suffix;
        
    }
}