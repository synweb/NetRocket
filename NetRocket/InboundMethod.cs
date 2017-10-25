using System;
using System.Collections.Generic;

namespace NetRocket
{
    public abstract partial class RocketBase
    {
        internal class InboundMethod
        {
            public InboundMethod(string networkMethodName, Dictionary<Type, object> delegateMap, Type paramType, Type resultType)
            {
                NetworkMethodName = networkMethodName;
                DelegateMap = delegateMap;
                ParamType = paramType;
                ResultType = resultType;
            }

            public string NetworkMethodName { get; set; }
            public Dictionary<Type, object> DelegateMap { get; set; }
            public Type ParamType { get; set; }
            public Type ResultType { get; set; }
        }

    }
}
