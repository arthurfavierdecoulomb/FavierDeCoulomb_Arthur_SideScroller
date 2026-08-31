using System.Collections.Generic;
using UnityEngine;

public class BossBarkPicker : MonoBehaviour
{
    public enum PickMode
    {
        Sequential,
        SequentialThenLoopLast,
        Random
    }

    [Header("Séquences de dialogue")]
    [SerializeField] private List<string> sequenceIds = new List<string>();
    [SerializeField] private PickMode mode = PickMode.SequentialThenLoopLast;

    [Header("Filtre")]
    [SerializeField] private bool skipIfDialoguePlaying = true;

    private int index;
    private string lastPlayed;

    public int PlayCount => index;

    public void PlayNext()
    {
        if (sequenceIds.Count == 0)
            return;

        if (BossDialogueManager.Instance == null)
            return;

        if (skipIfDialoguePlaying && BossDialogueManager.Instance.IsPlaying)
            return;

        string id = PickId();

        if (string.IsNullOrEmpty(id))
            return;

        lastPlayed = id;
        index++;

        BossDialogueManager.Instance.PlaySequence(id);
    }

    public void ResetPicker()
    {
        index = 0;
        lastPlayed = null;
    }

    private string PickId()
    {
        if (mode == PickMode.Random)
            return PickRandomAvoidingLast();

        if (index < sequenceIds.Count)
            return sequenceIds[index];

        if (mode == PickMode.SequentialThenLoopLast)
            return sequenceIds[sequenceIds.Count - 1];

        return sequenceIds[index % sequenceIds.Count];
    }

    private string PickRandomAvoidingLast()
    {
        if (sequenceIds.Count == 1)
            return sequenceIds[0];

        for (int attempt = 0; attempt < 8; attempt++)
        {
            string candidate = sequenceIds[Random.Range(0, sequenceIds.Count)];

            if (candidate != lastPlayed)
                return candidate;
        }

        return sequenceIds[0];
    }
}