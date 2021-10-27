
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// Export VoteState as JSON so you can sum multiple instances without double counting.
/// </summary>
public class JsonExport : UdonSharpBehaviour
{
    public VoteState voteState;
    public UnityEngine.UI.InputField output;

    // called from button
    public void DoExport()
    {
        // only allow some people to do this
        //if (Networking.LocalPlayer.displayName != "hiina") return;

        var s = "{";
        bool hadOne = false;
        for (int i = 0; i < voteState.voterUsernames.Length; i++)
        {
            string voter = voteState.voterUsernames[i];
            if (voter == "") continue;

            if (hadOne) s += ",";
            s += $"\"{voter}\":" + "{";

            var splits = voteState.votes[i].Split(',');
            bool hadTwo = false;
            for (int j = 0; j < splits.Length; j++)
            {
                var vc = splits[j].Split(':');
                var candidate = vc[0];
                var bitset = vc[1];
                if (hadTwo) s += ",";
                s += $"\"{candidate}\":{bitset}";
                hadTwo = true;
            }
            s += "}";
            hadOne = true;
        }
        output.text = s + "}";
        Debug.Log(output.text);
    }
}
