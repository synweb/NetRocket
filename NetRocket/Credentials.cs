using System;

namespace NetRocket
{
    public class Credentials
    {
        public Credentials(string login, string key)
        {
            Login = login;
            Key = key;
        }

        public string Login { get; }
        public string Key { get; }

        public override bool Equals(object obj)
        {
            if (!(obj is Credentials))
                return false;
            return this.Equals(obj as Credentials);
        }

        protected bool Equals(Credentials other)
        {
            return string.Equals(Login, other.Login, StringComparison.CurrentCultureIgnoreCase) && string.Equals(Key, other.Key);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Login.GetHashCode() * 397) ^ Key.GetHashCode();
            }
        }

        public static bool operator ==(Credentials left, Credentials right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Credentials left, Credentials right)
        {
            return !Equals(left, right);
        }
    }
}
