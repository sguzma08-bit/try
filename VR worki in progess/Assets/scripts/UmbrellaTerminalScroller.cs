using System.Collections;
using UnityEngine;
using TMPro;

public class UmbrellaTerminalScroller : MonoBehaviour
{
    public TextMeshProUGUI textDisplayField;
    [TextArea(3, 10)]
    public string bootSequenceMessage = "⚠️ WARNING: ARKLAY SECTOR-4 CONTAINMENT FAILURE DETECTED.\n── AIRBORNE VIRAL PARAMETERS EXCEEDED.\n── TERMINAL SYSTEM LOCKDOWN IN EFFECT.\n\n[TASK 01]: OVERRIDE WALL SECURITY KEYPAD PANEL TO UNLOCK FIELD MED SUPPLIES.";
    public float printSpeed = 0.02f;

    private void Start()
    {
        if (textDisplayField != null)
        {
            StartCoroutine(ExecuteTerminalBoot());
        }
    }

    private IEnumerator ExecuteTerminalBoot()
    {
        textDisplayField.text = "";
        foreach (char c in bootSequenceMessage)
        {
            textDisplayField.text += c;
            yield return new WaitForSeconds(printSpeed);
        }
    }
}
