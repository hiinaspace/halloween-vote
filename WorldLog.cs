
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// in-world canvas log, which is always useful for debugging.
/// </summary>
public class WorldLog : UdonSharpBehaviour
{
    public UnityEngine.UI.Text text;
    public void Log(string message)
    {
        Debug.Log($"[HWS] {message}");
        // skip if nobody is looking
        if (!text.gameObject.activeInHierarchy) return;
        if (text.text.Split('\n').Length > 60)
        {
            // trim
            text.text = text.text.Substring(text.text.IndexOf('\n') + 1);
        }
        text.text += $"{System.DateTime.Now}: {message}\n";
    }
}
