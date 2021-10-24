
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// check last updated timestamps on other scripts to see if they crashed
/// and display big message
/// </summary>
public class Watchdog : UdonSharpBehaviour
{
    public VoteState voteState;
    public LocalBallot localBallot;
    public TotalVoteUi totalVoteUi;
    public GameObject errorMessage;

    void Start()
    {
        voteState = GameObject.Find("VoteState").GetComponent<VoteState>();
        localBallot = GameObject.Find("LocalBallot").GetComponent<LocalBallot>();
        SendCustomEventDelayedSeconds(nameof(SlowUpdate), 10f);
    }

    public void SlowUpdate()
    {
        SendCustomEventDelayedSeconds(nameof(SlowUpdate), 10f);
        if ((Time.time - voteState.lastUpdate) > 10f ||
            (Time.time - totalVoteUi.lastUpdate) > 10f ||
            (Time.time - localBallot.lastUpdate) > 10f)
        {
            errorMessage.SetActive(true);
        }
    }
}
