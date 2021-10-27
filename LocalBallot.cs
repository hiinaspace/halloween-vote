
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class LocalBallot : UdonSharpBehaviour
{
    private const float UPDATE_RATE = 1.0f;

    public VoteMessenger[] voteMessengers;
    public VoteState voteState;

    private bool initialized = false;

    const int MAXSIZE = 512;
    private string[] candidateUsernames = new string[MAXSIZE];
    private int[] voteBitsets = new int[MAXSIZE];
    private int ballotSize = 0;

    public Transform ballotUiRoot;
    public GameObject prototypeRow;

    public float lastUpdate; // for watchdog timer

    public WorldLog worldLog;
    private void log(string msg)
    {
        worldLog.Log($"[LocalBallot] {msg}");
    }

    void Start()
    {
        voteMessengers = voteState.transform.GetComponentsInChildren<VoteMessenger>();
        SlowUpdate(); // kick off update loop
        log("start");
    }

    public string DisplayName(VRCPlayerApi player)
    {
        string displayName = player.displayName;
        // hack for local testing when everyone has the same name
        //displayName = $"{player.displayName}_{player.playerId}";
        //string displayName = "testLocal";

        // replace special characters just in case.
        return displayName.Replace("_", "").Replace(",", "_");
    }

    // last time we attempted to update VoteState from our local state
    // so we can periodically check that our local state is reflected properly
    private float lastMessageAttempt = 0f;

    public void SlowUpdate()
    {
        lastUpdate = Time.time;
        SendCustomEventDelayedSeconds(nameof(SlowUpdate), UPDATE_RATE);
        if (!voteState.initialized) return;

        if (!initialized)
        {
            // starting from any server value if it's there
            var localVote = voteState.LookupVote(DisplayName(Networking.LocalPlayer));
            if (localVote != "")
            {
                // we had a previous vote in this instance
                // deserialize into hashmap
                string[] newVotes = localVote.Split(',');
                foreach (string candidateVote in newVotes)
                {
                    var candidateAndVote = candidateVote.Split(':');
                    var candidate = candidateAndVote[0];
                    var vote = int.Parse(candidateAndVote[1]);
                    if (set(candidate, vote, candidateUsernames, voteBitsets))
                    {
                        ballotSize++;
                    }
                    // new row for each vote, including players not currently in the instance
                    AddNewRow(initializeCheckmarksFromState: true, candidate, vote);
                }
            }

            log($"initialized local vote to '{localVote}'");
            initialized = true;
            lastMessageAttempt = Time.time;
        }

        // copy retained mode UI state to local state, push detected changes to global VoteState.

        bool changed = false;
        // i think the foreach transform skips deactivated gameobjects.
        for (int j = 0; j < ballotUiRoot.childCount; ++j)
        {
            var t = ballotUiRoot.GetChild(j);

            // XXX i stuck the prototype row in there for ease of creation
            if (t.gameObject == prototypeRow) continue;

            var candidateUsername = t.name.Substring(4); // strip off 'row_'
            var toggles = t.GetComponentsInChildren<UnityEngine.UI.Toggle>();
            var uiVote = 0;
            for (int i = 0; i < 6; i++)
            {
                uiVote |= (toggles[i].isOn ? 1 : 0) << i;
            }

            int currentVote = lookup(candidateUsername, candidateUsernames, voteBitsets);
            if (currentVote != uiVote)
            {
                log($"{candidateUsername} changed {currentVote} -> {uiVote}");
                if (set(candidateUsername, uiVote, candidateUsernames, voteBitsets))
                {
                    ballotSize++;
                }
                changed = true;
            }

            // deactivate, then we'll reactivate in the active player loop;
            // might be expensive, but querying for active players is more annoying
            t.gameObject.SetActive(false);
        }

        // if any changed, serialize entire vote state, grab messenger, compare and set value.
        // also every so often, serialize state and check against voteState, message if it's
        // different (a past compare and set never took).
        if (changed || (Time.time - lastMessageAttempt) > 10f)
        {
            lastMessageAttempt = Time.time;

            // serialize entire state
            string[] ser = new string[ballotSize];
            var n = 0;
            for (int i = 0; i < MAXSIZE; i++)
            {
                var candidateUsername = candidateUsernames[i];
                if (candidateUsername != null)
                {
                    ser[n++] = $"{candidateUsername}:{voteBitsets[i]}";
                }
            }
            var newVote = string.Join(",", ser);
            log($"serialized new vote {newVote}");
            var currentBroadcastVote = voteState.LookupVote(DisplayName(Networking.LocalPlayer));

            if (currentBroadcastVote != newVote)
            {
                // message on random messenger
                var m = voteMessengers[UnityEngine.Random.Range(0, voteMessengers.Length)];
                log($"{m.name} cas: {currentBroadcastVote} -> {newVote}");
                Networking.SetOwner(Networking.LocalPlayer, m.gameObject);
                m.voterUsername = DisplayName(Networking.LocalPlayer);
                m.oldVote = currentBroadcastVote;
                m.newVote = newVote;
                m.RequestSerialization();
            }
        }

        // for each current player in instnace, try to find row in UI by GameObject.find.
        // set to active, so only current players have active rows. if a player rejoins
        // old row is reactivated.
        // if it's not there, insert into sorted order. yes, n^2.
        VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        VRCPlayerApi.GetPlayers(players);
        foreach (var p in players)
        {
            if (p == Networking.LocalPlayer) continue;
            var candidate = DisplayName(p);
            AddNewRow(initializeCheckmarksFromState: false, candidate, 0);

        }
    }

    /// <summary>
    /// Add a vote for the given candidate and category, e.g. from some trigger event.
    /// </summary>
    /// <param name="candidate">candidate to vote for</param>
    /// <param name="voteCategory">vote category (0-5)</param>
    public void AddVoteForCandidate(VRCPlayerApi candidate, int voteCategory)
    {
        // no self votes
        if (candidate == Networking.LocalPlayer) return;

        var ballotRowT = ballotUiRoot.Find($"row_{DisplayName(candidate)}");
        if (ballotRowT != null)
        {
            var toggles = ballotRowT.GetComponentsInChildren<UnityEngine.UI.Toggle>();
            toggles[voteCategory].isOn = true;
        }
        // else just wait for update loop to add row for user;
        // this shouldn't happen practically
    }

    private void AddNewRow(bool initializeCheckmarksFromState, string candidate, int currentVote)
    {
        string rowName = $"row_{candidate}";

        var ballotRowT = ballotUiRoot.Find(rowName);
        if (ballotRowT == null)
        {
            var ballotRow = VRCInstantiate(prototypeRow);
            ballotRow.name = rowName;
            ballotRow.GetComponentInChildren<UnityEngine.UI.Text>().text = candidate;

            ballotRow.transform.SetParent(ballotUiRoot);
            ballotRow.transform.SetAsLastSibling();
            ballotRow.SetActive(true);

            // used only during initialization, if the user rejoined and had previous state.
            // XXX weird, but the least bad way I could think to share code.
            if (initializeCheckmarksFromState)
            {
                var toggles = ballotRow.GetComponentsInChildren<UnityEngine.UI.Toggle>();
                toggles[0].isOn = (currentVote & 1) > 0;
                toggles[1].isOn = (currentVote & 2) > 0;
                toggles[2].isOn = (currentVote & 4) > 0;
                toggles[3].isOn = (currentVote & 8) > 0;
                toggles[4].isOn = (currentVote & 16) > 0;
                toggles[5].isOn = (currentVote & 32) > 0;
            }

            // vrcinstantiate seems to reset the transform to world 0 0
            var transform = ballotRow.GetComponent<RectTransform>();
            transform.anchoredPosition = Vector3.zero;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            transform.ForceUpdateRectTransforms();

            // move to sorted order (if it's not already last)
            for (int j = 0; j < ballotUiRoot.childCount; ++j)
            {
                var t = ballotUiRoot.GetChild(j);
                if (rowName.CompareTo(t.name) < 0)
                {
                    ballotRow.transform.SetSiblingIndex(j);
                    break;
                }
            }
            log($"new row for {candidate} at {ballotRow.transform.GetSiblingIndex()}");
        }
        else
        {
            //log($"found existing row for {candidate}");
            // reactivate since player is in the map
            ballotRowT.gameObject.SetActive(true);
        }
    }

    public
        int lookup(string key, string[] keys, int[] values)
    {
        var i = linearProbe(key, keys);
        var k = keys[i];
        return k == null ? 0 : values[i];
    }

    private
        int linearProbe(string key, string[] keys)
    {
        // XXX negative modulus happens sometimes. might be biased but good enough for here.
        var init = Mathf.Abs(key.GetHashCode()) % MAXSIZE;
        var i = init;
        var k = keys[i];
        while (k != null && k != key)
        {
            i = (i + 1) % MAXSIZE;
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

    public
        bool set(string key, int value, string[] keys, int[] values)
    {
        var i = linearProbe(key, keys);
        var newKey = keys[i] == null;
        keys[i] = key;
        values[i] = value;
        return newKey;
    }
}
