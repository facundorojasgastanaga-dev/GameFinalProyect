using UnityEngine;

namespace IronRequiem
{
    // Executions v02 (22/11/2026): shared contract between the player side (ExecutionDirector, DisarmMotion,
    // BigBotExecutionSequence) and the robot side (Scripts/Enemies/Execution). The player raises beats through
    // EnemyExecutionSource.RaiseBeat; the robot only listens (it never needs to know the player).
    // Timeline for T-07 / SawBot (DisarmMotion clock): Begin 0 (=Grab start) -> Struggle .18 -> Rip .34 (blow 1)
    // -> Handoff .44 -> WindUp .52 -> Strike .732 (blow 2 + death) -> Recover -> End.
    // Big Bot: Begin 0 (Leap) -> Grab .40 (land + hand closes) -> Struggle .46 -> Rip .62 (blow 1) -> Handoff .80
    // -> Strike .95 (shot + death; the jump back starts on this frame: Recover .95) -> End 1.40.
    public enum ExecutionBeat { None, Begin, Grab, Struggle, Rip, Handoff, WindUp, Strike, Recover, End, Cancel }

    public enum ExecutionKind { T07, SawBot, BigBot, Other }

    public struct ExecutionBeatInfo
    {
        public ExecutionBeat Beat;
        public ExecutionKind Kind;
        // Player camera position and the flat (y = 0) direction from the player to the robot at this beat.
        public Vector3 PlayerPosition;
        public Vector3 ToRobot;
        // World position of the player's hand that grabs / punches (left hand; Big Bot: grabbing hand).
        public Vector3 HandPosition;
        // Begin only: metres the robot must be pulled toward the player during Grab (0 = none) and its duration.
        public float PullDistance;
        public float PullSeconds;
        // Seconds since Begin on the execution clock (scaled game time).
        public float Time;
    }
}
