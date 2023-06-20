using UnityEngine;

public static class HashAnimParam
{
    public static int PlayerVelocity = Animator.StringToHash("Velocity");
    public static int PlayerIsJump = Animator.StringToHash("IsJump");
    public static int PlayerIsRoll = Animator.StringToHash("IsRoll");
    public static int PlayerIsLeaveStableGround = Animator.StringToHash("IsLeaveStableGround");
    public static int PlayerIsLanded = Animator.StringToHash("IsLanded");
    public static int PlayerOnAir = Animator.StringToHash("OnAir");
}
