
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

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


    private const float UPDATE_RATE = 1.0f;

    void Start()
    {
        SlowUpdate(); // kick off update loop
    }

    private void SlowUpdate()
    {
        SendCustomEventDelayedSeconds(nameof(SlowUpdate), UPDATE_RATE);
        // if not master, we don't have to do anything (but continue looping if we ever become master);
        if (!Networking.IsOwner(gameObject)) return;

        bool changed = false;
        if (!initialized)
        {
            changed = true;
            initialized = true;
        }

        // check for new updates
        foreach (var messenger in messengers)
        {
            if (messenger.voterUsername == "") continue; // skip empty

            int i = lookup2(messenger.voterUsername);
            string currentVote = votes[i];
            if (currentVote == messenger.oldVote)
            {
                votes[i] = messenger.newVote;
                changed = true;
            }

        }
        


        if (changed)
        {
            RequestSerialization();
        }
    }

    // hash map lookup, quadratic probing.
    private int lookup2(string key)
    {
        return 0;
    }
}
