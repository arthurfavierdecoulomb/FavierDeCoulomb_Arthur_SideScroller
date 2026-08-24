using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OngletManager : MonoBehaviour
{
    [Serializable]
    public class Tab
    {
        public Button button;
        public GameObject page;
        public GameObject activeVisual;
        public TMP_Text label;
    }

    [Header("Onglets")]
    [SerializeField] Tab[] tabs;

    [Header("Onglet ouvert par défaut")]
    [SerializeField] int defaultTab;

    [Header("Couleurs des libellés")]
    [SerializeField] Color activeLabelColor = Color.black;
    [SerializeField] Color inactiveLabelColor = Color.white;

    int selected = -1;

    void Awake()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            if (tabs[i].button != null)
                tabs[i].button.onClick.AddListener(() => Select(index));
        }
    }

    void OnEnable()
    {
        Select(defaultTab);
    }

    public void Select(int index)
    {
        if (tabs == null || tabs.Length == 0) return;

        selected = Mathf.Clamp(index, 0, tabs.Length - 1);

        for (int i = 0; i < tabs.Length; i++)
        {
            bool active = i == selected;

            if (tabs[i].page != null) tabs[i].page.SetActive(active);
            if (tabs[i].activeVisual != null) tabs[i].activeVisual.SetActive(active);
            if (tabs[i].label != null) tabs[i].label.color = active ? activeLabelColor : inactiveLabelColor;
        }
    }
}