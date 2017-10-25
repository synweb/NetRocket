using System;
using System.Collections.Generic;
using System.Text;

namespace NetRocket.Exceptions
{
    public class MethodDoesNotExistException: Exception
    {
        public MethodDoesNotExistException(string methodName)
        {
            MethodName = methodName;
        }

        public string MethodName { get; }
    }
}
