namespace NetRocket.Frames
{
    public class RoRequestFrame: RoFrame
    {
        public RoRequestFrame(string methodName, object parameter)
        {
            MethodName = methodName;
            Parameter = parameter;
        }

        public RoRequestFrame()
        {
            
        }

        public string MethodName { get; set; }
        public object Parameter { get; set; }

        public override string ToString()
        {
            return $"{MethodName}:{Parameter}";
        }
    }
}
