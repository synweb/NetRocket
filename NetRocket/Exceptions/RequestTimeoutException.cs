using System;

namespace NetRocket.Exceptions
{
    public class RequestTimeoutException : Exception
    {
        public RequestTimeoutException(Guid requestGuid)
        {
            RequestGuid = requestGuid;
        }

        public RequestTimeoutException()
        {
            
        }

        public Guid RequestGuid { get; set; }
    }
}
