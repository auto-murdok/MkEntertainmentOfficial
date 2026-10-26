
// The single timeline of the paired bite animation (zombie_neck_bite /
// take_bite), measured on one shared clock that starts when the grab
// starts. Both sides anchor to these numbers so the pair can never
// drift apart: there is one animation, so there is one timeline for
// every zombie archetype.
public static class BiteTimeline
{
    // His head-snap contact moment: the bite damage lands here.
    public const float ContactTime = 0.97f;

    // Her push completes: she is freed (PlayerData.takeBiteDuration must
    // equal this) and the zombie's FSM lets go.
    public const float ReleaseTime = 3.0f;
}
