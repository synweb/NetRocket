namespace NetRocket.Frames
{
    public class RoSimpleFrame: RoFrame
    {
        public RoSimpleFrame(object data)
        {
            Data = data;
        }

        public RoSimpleFrame()
        {
            
        }

        public object Data { get; set; }
    }
}
