
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

/// <summary>
/// Synced gameobject for VoteState updates, so players don't have to contend on
/// the single VoteState behavior. Multiple VoteMessengers are checked by
/// VoteState, and other players take ownership of a random Messenger to send updates.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class VoteMessenger : UdonSharpBehaviour
{
    // voter username, to disambiguate 
    [UdonSynced] public string voterUsername = "";
    // expected vote value from which to update, to prevent out of order updates from
    // clobbering eachother, i.e. compare-and-set.
    [UdonSynced] public string oldVote = "";
    // new vote to replace old vote.
    [UdonSynced] public string newVote = "";

    public WorldLog worldLog;
    private void log(string msg)
    {
        worldLog.Log($"[{name}] {msg}");
    }

    public override void OnPreSerialization()
    {
        log("onpreserialization");
    }

    public override void OnDeserialization()
    {
        log("ondeserialize");
    }

    public override void OnPostSerialization(SerializationResult result)
    {
        log($"onpostserialization success={result.success} bytes={result.byteCount}");
    }
}
