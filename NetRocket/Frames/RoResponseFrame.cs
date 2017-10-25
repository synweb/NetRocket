using System;

namespace NetRocket.Frames
{
    public class RoResponseFrame:RoFrame
    {
        public RoResponseFrame(Guid requestGuid, object result, ResponseStatusCode statusCode)
        {
            RequestGuid = requestGuid;
            Result = result;
            StatusCode = statusCode;
        }
        public RoResponseFrame(Guid requestGuid, ResponseStatusCode statusCode)
        {
            RequestGuid = requestGuid;
            StatusCode = statusCode;
        }

        public RoResponseFrame()
        {
        }

        public Guid RequestGuid { get; set; }
        public object Result { get; set; }
        public ResponseStatusCode StatusCode { get; set; }

    }
}
