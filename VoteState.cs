
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

/// <summary>
/// Global vote state, synced manually.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class VoteState : UdonSharpBehaviour
{
    [UdonSynced] public bool initialized = false;

    private const int MAPSIZE = 512;
    [UdonSynced] public string[] voterUsernames = new string[MAPSIZE];
    [UdonSynced] public string[] votes = new string[MAPSIZE];

    public VoteMessenger[] messengers;

    public WorldLog worldLog;

    private const float UPDATE_RATE = 1.0f;
    public float lastUpdate; // for watchdog timer

    void Start()
    {
        messengers = GetComponentsInChildren<VoteMessenger>();

        // XXX i don't think udon synced can deal with null strings
        for (int i = 0; i < MAPSIZE; i++)
        {
            voterUsernames[i] = "";
            votes[i] = "";
        }

        SlowUpdate(); // kick off update loop
        log("start");
    }

    private void log(string msg)
    {
        worldLog.Log($"[VoteState] {msg}");
    }

    private float lastBroadcast = 0f;

    public void SlowUpdate()
    {
        lastUpdate = Time.time;
        SendCustomEventDelayedSeconds(nameof(SlowUpdate), UPDATE_RATE);
        // if not master, we don't have to do anything (but continue looping if we ever become master);
        if (!Networking.IsOwner(gameObject)) return;

        bool changed = false;
        if (!initialized)
        {
            changed = true;
            log("initialized");
            initialized = true;
        }

        // check for new updates
        foreach (var messenger in messengers)
        {
            if (messenger.voterUsername == "") continue; // skip empty

            int i = linearProbe(messenger.voterUsername, voterUsernames);
            string currentVote = votes[i];
            if (currentVote == messenger.oldVote)
            {
                log($"{messenger.name} new vote for {messenger.voterUsername}");
                votes[i] = messenger.newVote;

                // take ownership and clear messenger, lest there be two or more messengers
                // with a compare and set loop between them. LocalBallot chooses a random
                // messenger so it's possible to have e.g. a -> b and b -> a, then those
                // updates loop until overwritten.
                Networking.SetOwner(Networking.LocalPlayer, messenger.gameObject);
                messenger.voterUsername = "";
                messenger.oldVote = "";
                messenger.newVote = "";
                messenger.RequestSerialization();

                changed = true;
            }
        }

        if (changed || (Time.time - lastBroadcast) > 10f)
        {
            lastBroadcast = Time.time;
            RequestSerialization();
        }
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        // send state for new player
        RequestSerialization();
    }

    public override void OnPostSerialization(SerializationResult result)
    {
        if (!result.success)
        {
            log($"onpostserialization failed bytes={result.byteCount}");
        }
    }

    private
        int linearProbe(string key, string[] keys)
    {
        // XXX negative modulus happens sometimes. might be biased but good enough for here.
        var init = Mathf.Abs(key.GetHashCode()) % MAPSIZE;
        var i = init;
        var k = keys[i];
        // XXX no nulls in this map because of the serialization bug.
        while (k != "" && k != key)
        {
            i = (i + 1) % MAPSIZE;
            // I think this won't happen if the population is always less than the size
            if (i == init)
            {
                log("uhoh wrapped around linear probe");
                return -1;
            }
            k = keys[i];
        }
        return i;
    }

    public string LookupVote(string voterUsername)
    {
        return votes[linearProbe(voterUsername, voterUsernames)];
    }
}
