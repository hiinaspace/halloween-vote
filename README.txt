# halloween candy voting thing

Udon system to keep track of candies (i.e. votes) given to players.

## Requirements

- Each player can give up to 6 candies/vote types to each other player in the instance.
- total vote counts broadcast to all players
- robust to players leaving/joining and master turnover
- player can adjust their own votes at any time
- export vote matrix by username, so multiple instances can be tallied without double-counting

Nice to have:

- physical pickup system for giving candy to players in addition to UI menu

## Design

The following data structure repesents the voting state:

    Map<VoterUsername, Map<VoteReceiverUsername, Set<Vote>>>

Where Vote is an enum of 6 vote types. Each voter controls their own "row"
of the state.

The VoteState UdonBehavior contains fields:

    // usernames as hash map keys
    string[512] stateKeys; 
    // serialized Map<Username, Set<Vote>> as
    // username:[0-63],username:[0-63],...
    // where the vote is a 6-bit set of the 6 vote types.
    // colons and commas are stripped from the usernames so split(":") works
    string[512] stateVotes;

The fields are manually synced (broadcast) as often as possible, so that
all players have the up to date state in case of instance master turnover.

The VoteState behavoir remains owned by the instance master. In order for a
player to update their own vote, a collection of VoteMessenger behaviors have
fields:

   // username disambiguates a VoteMessenger initially owned by instance master
   string pendingUsername; 
   string pendingVote;

The LocalBallot behavior keeps track of the local player's vote state
(serialized). In a periodic loop, the LocalBallot checks if the player's local
state is reflected in the global VoteState. If the VoteState is out of date, the
LocalBallot behavior chooses a VoteMessanger at random, takes ownership, sets
`pendingUsername` and `pendingVote` to the local state, and the waits a random
amount of time.

In a periodic loop, the VoteState owner checks all VoteMessenger behaviors,
copies the pending state to the global state, and requests serialization. Thus,
players are able to update the VoteState without having to contend ownership of
the single VoteState behavior. The amount of VoteMessenger behaviors should
be adjusted to allow players to update their states relatively quickly. I think
10 or so behaviors should be fine.

If a player rejoins an instance where they already voted, the global VoteState
should overwrite their empty LocalBallot. to check for this case, a boolean
`initialized` variable on the VoteState is checked (for when the first VoteState
sync is broadcast to the player). Once the VoteState is initialized, then
the LocalBallot state is copied from the VoteState if found, and the LocalBallot
is initialized.

### Ballot Canvas UI

The BallotUi behavior controls a menu for manually changing votes. An Update()
loop checks for a keyboard input "I" key for desktop players, or for VR players
to have held down both triggers for a short period of time in order to display
the UI canvas in front of the player. This is how vket5's menu worked, and I
think it's the least bad way to do custom menus in Udon currently.

On a periodic loop, the BallotUi behavior populates the UI canvas with a row
for each player in the instance, as well as any usernames reflected in the
user's LocalBallot state that aren't currently in the instance (for robustness
to players crashing temporarily). Each row also has 6 Toggle components in a
Toggle group for the 6 vote types. In the loop, if the toggle state is
different than the LocalBallot state (i.e. the user clicked on the UI), the
LocalBallot state is updated to match.

to handle the player rejoining, the BallotUi only is updated once the LocalBallot
is initialized and possibly copied from the global VoteState. That way, the
BallotUi doesn't have to disambiguate between the player updating the UI state vs
the LocalBallot being changed. The UI state always drives the LocalBallot state.

### Vote tally display

The TotalVoteUi behavior periodically tallies the votes from the VoteState and
displays them on scoreboard(s) somewhere in the map.

The ExportVote behavior serializes the vote state into a JSON string on a
textInput component in a canvas, visible only to certain admin usernames. That
way, the full vote state can be exported and deduplicated between multiple instances
without double counting (a player goes to both instances and votes for the
same other player in both instances), using the following jq script:

```sh
cat vote1.josn vote2.json | jq \
'[.] | map(.merge by bitwise OR) |
 for each [1,3,7,15,31,63] , bitwise and, sum, order by, take top 3]'
```

You can also use https://jqplay.org/ (TODO snippet link)

## Physical Candy

The map contains a collection of globally synced VRCPickups of the 6 different
vote/candy types. Each pickup has a VoteCandy behavior. If the local player
holds holds the trigger (OnPickupuser) on the candy by another player (detected
by OnPlayerTriggerEnter) the candy enables a small UI showing the player name
currently colliding. If the local player keeps the candy colliding with the
other player (simulating giving the candy to the other player), the behavior
updates the BallotUi checkbox for the local player reflecitng the vote, then
drops itself and resets the position of the pickup.

Thus the candy giving serves as an alternative to maniupulating the BallotUi.
The short confirmation delay (inspired to the handshake gesture to add friends
in Rec Room) makes the gesture robust to players trying to snipe candies meant
for others by crowding the candy giver.

I _think_ vrchat should be able to handle ~60 or so synced pickups (enough for 10
houses), as long as their RigidBodies are kinematic (no throwing candies at
people). 
