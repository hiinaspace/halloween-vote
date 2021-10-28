
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class TotalVoteUi : UdonSharpBehaviour
{
    public VoteState voteState;
    public WorldLog worldLog;
    public UnityEngine.UI.Text text;
    public UnityEngine.UI.Text[] topUsernames;
    const int MAXSIZE = 512;
    private void log(string msg)
    {
        worldLog.Log($"[TotalVoteUi] {msg}");
    }

    void Start()
    {
        SlowUpdate();
        log("start");
    }

    public float lastUpdate;

    public void SlowUpdate()
    {
        lastUpdate = Time.time;
        SendCustomEventDelayedSeconds(nameof(SlowUpdate), 5f);

        if (!voteState.initialized) return;

        var candidateUsernames = new string[MAXSIZE];
        var candidateCount = 0;
        var tally = new int[MAXSIZE][];
        for (int i = 0; i < MAXSIZE; i++)
        {
            var serVote = voteState.votes[i];
            if (serVote == "") continue;

            var splits = serVote.Split(',');
            for (int j = 0; j < splits.Length; j++)
            {
                var vc = splits[j].Split(':');
                var candidate = vc[0];
                var bitset = int.Parse(vc[1]);
                int k = linearProbe(candidate, candidateUsernames);
                candidateUsernames[k] = candidate;
                int[] cTally = tally[k];
                if (cTally == null)
                {
                    cTally = new int[6];
                    candidateCount++;
                }

                cTally[0] += ((bitset & 1) > 0 ? 1 : 0);
                cTally[1] += ((bitset & 2) > 0 ? 1 : 0);
                cTally[2] += ((bitset & 4) > 0 ? 1 : 0);
                cTally[3] += ((bitset & 8) > 0 ? 1 : 0);
                cTally[4] += ((bitset & 16) > 0 ? 1 : 0);
                cTally[5] += ((bitset & 32) > 0 ? 1 : 0);
                tally[k] = cTally;
            }
        }
        var tallyLines = new string[candidateCount];
        var n = 0;

        for (int i = 0; i < MAXSIZE; i++)
        {
            if (candidateUsernames[i] != null)
            {
                var t = tally[i];
                // XXX no format alignment in usharp
                tallyLines[n++] = $"{candidateUsernames[i].PadLeft(20)}: {t[0]} {t[1]} {t[2]} {t[3]} {t[4]} {t[5]}";
            }
        }

        // sort tally alphabetically
        for (int i = 1; i < n; ++i)
        {
            var s = tallyLines[i];
            var j = i - 1;
            while (j >= 0 && tallyLines[j].CompareTo(s) > 0)
            {
                tallyLines[j + 1] = tallyLines[j];
                j--;
            }
            tallyLines[j + 1] = s;
        }

        text.text = $"Vote Tally:\n{string.Join("\n", tallyLines)}";

        // populate top lists
        for (int category = 0; category < 6; category++)
        {
            var candidates = new string[MAXSIZE];
            var score = new int[MAXSIZE];
            int m = 0;
            for (int i = 0; i < MAXSIZE; i++)
            {
                if (candidateUsernames[i] != null)
                {
                    var t = tally[i];
                    // XXX no format alignment in usharp
                    candidates[m] = candidateUsernames[i];
                    score[m] = t[category];
                    m++;
                }
            }
            // sort descending order
            for (int i = 1; i < m; ++i)
            {
                var s = score[i];
                var user = candidates[i];
                var j = i - 1;
                while (j >= 0 && score[j].CompareTo(s) < 0)
                {
                    score[j + 1] = score[j];
                    candidates[j + 1] = candidates[j];
                    j--;
                }
                score[j + 1] = s;
                candidates[j + 1] = user;
            }
            var textLines = new string[m];
            for (int i = 0; i < m; i++)
            {
                if (score[i] > 0)
                {
                    textLines[i] = $"{i+1}. {candidates[i]} ({score[i]})\n";
                }
                else
                {
                    textLines[i] = "";
                }
            }

            topUsernames[category].text = string.Join("", textLines);
        }
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

}
