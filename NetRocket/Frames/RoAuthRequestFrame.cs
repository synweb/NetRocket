namespace NetRocket.Frames
{
    public class RoAuthRequestFrame: RoRequestFrame
    {
        public RoAuthRequestFrame() : base("Authenticate", null)
        {
        }

        public RoAuthRequestFrame(Credentials credentials): base("Authenticate", credentials)
        {
        }
    }
}
