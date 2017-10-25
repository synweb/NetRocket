using System;

namespace NetRocket.Frames
{
    public abstract class RoFrame
    {
        public RoFrame()
        {
            Timestamp = DateTime.Now;
            Guid = Guid.NewGuid();
        }

        public Guid Guid { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
